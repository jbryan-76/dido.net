using System.Collections.Concurrent;

namespace DidoNet
{
    /// <summary>
    /// Represents a single unique bidirectional communications stream on a Connection.
    /// This allows multiple independent channels to communicate using a single Connection
    /// by multiplexing their data.
    /// <para/>NOTE While this class is thread-safe and reading and writing can be done on separate threads,
    /// never use more than 1 thread per direction (read or write), as it may result in data interleaving.
    /// </summary>
    public class Channel : Stream
    {
        /// <summary>
        /// Signature for a method that handles when new data is available on a channel.
        /// </summary>
        /// <param name="channel"></param>
        public delegate void ChannelDataAvailableHandler(Channel channel);

        /// <summary>
        /// An event handler that is triggered when any amount of new data is available.
        /// The handler should consume as much data as possible from the channel.
        /// <para/>Note: The handler is run in a separate thread and any thrown exception
        /// will not be available until the channel is disposed, where it will be wrapped
        /// in an AggregateException.
        /// </summary>
        public ChannelDataAvailableHandler? OnDataAvailable = null;

        public string Name { get; set; }

        /// <summary>
        /// The unique id for the channel.
        /// </summary>
        public ushort ChannelNumber { get; private set; }

        /// <summary>
        /// The Connection the channel is using for data tranmission.
        /// </summary>
        public Connection Connection { get; private set; }

        /// <summary>
        /// Indicates whether data is available to Read().
        /// </summary>
        public bool IsDataAvailable { get { return !ReadBuffer.IsEmpty; } }

        /// <summary>
        /// Indicates whether the channel has any pending or in-flight data (true)
        /// or is effectively empty (false).
        /// </summary>
        public bool InUse { get { return WriteBuffer.Length != 0 || IsDataAvailable; } }

        /// <summary>
        /// Indicates whether the underlying Connection exists and is connected.
        /// </summary>
        public bool IsConnected { get { return Connection != null && Connection.IsConnected; } }

        /// <summary>
        /// Indicates whether Read() will block until some data is available.
        /// </summary>
        public bool BlockingReads { get; set; } = false;

        /// <summary>
        /// Used with Interlocked as part of IDispose to indicate whether the object instance is disposed.
        /// </summary>
        private long IsDisposed = 0;

        /// <summary>
        /// A thread-safe memory buffer to queue data writes for transmission.
        /// </summary>
        private MemoryStream WriteBuffer = new MemoryStream();

        /// <summary>
        /// A thread responsible for writing queued data to the underlying connection.
        /// </summary>
        private Thread? WriteThread = null;

        /// <summary>
        /// Any exception thrown by the WriteThread, to be held and re-thrown when the thread joins.
        /// </summary>
        private Exception? WriteThreadException = null;

        /// <summary>
        /// A thread-safe queue of data received by the channel that is ready to be Read().
        /// </summary>
        private ConcurrentQueue<byte[]> ReadBuffer = new ConcurrentQueue<byte[]>();

        /// <summary>
        /// A thread responsible for reading and buffering data from the underlying connection.
        /// </summary>
        private Thread? ReadThread = null;

        /// <summary>
        /// Any exception thrown by the ReadThread, to be held and re-thrown when the thread joins.
        /// </summary>
        private Exception? ReadThreadException = null;

        /// <summary>
        /// Indicates where in the ReadBuffer the next read should start.
        /// </summary>
        private int CurrentSegmentOffset = 0;

        /// <summary>
        /// Returns a task that yields true when data is available to read,
        /// else false if the underlying connection closes. If 'throwIfClosed' is true,
        /// an IOException will be thrown instead of returning true.
        /// <para/>Note: since this call utilizes a thread in the thread pool, only use in
        /// situations where the caller does not expect to wait very long for data to arrive,
        /// (ie no more than a few seconds) otherwise the call will utilize an entire thread while 
        /// waiting, which may block other work.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public Task<bool> WaitForDataAsync(bool throwIfClosed = false)
        {
            var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task.Run(() =>
            {
                while (!IsDataAvailable)
                {
                    ThreadHelpers.Yield();

                    if (!IsConnected)
                    {
                        if (throwIfClosed)
                        {
                            throw new IOException("Connection closed.");
                        }
                        source.SetResult(false);
                        return;
                    }
                }
                source.SetResult(true);
            });
            return source.Task;
        }

        /// <summary>
        /// Indicates the stream can read. Always true.
        /// </summary>
        public override bool CanRead => true;

        /// <summary>
        /// Indicates the stream can read. Always false.
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// Indicates the stream can write. Always true.
        /// </summary>
        public override bool CanWrite => true;

        /// <summary>
        /// Not implemented. Throws NotImplementedException.
        /// </summary>
        public override long Length => throw new NotImplementedException();

        /// <summary>
        /// Not implemented. Throws NotImplementedException.
        /// </summary>
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// Blocks until all pending data is written to the underlying stream.
        /// </summary>
        public override void Flush()
        {
            // block until the write buffer clears
            while (WriteBuffer.Length > 0 && IsConnected)
            {
                ThreadHelpers.Yield();
            }
        }

        /// <summary>
        /// Not implemented. Throws NotImplementedException.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not implemented. Throws NotImplementedException.
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="NotImplementedException"></exception>
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Reads a sequence of bytes received from the underlying connection into the provided buffer
        /// and returns the number of bytes read.
        /// If BlockingReads is enabled, will block forever until at least 1 byte is read.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = 0;
            int remaining = count;
            while (remaining > 0 && IsConnected)
            {
                // TODO: explore using QueueBufferStream instead?
                // peek at the next available segment
                if (ReadBuffer.TryPeek(out byte[] segment))
                {
                    // how many bytes remain to be read in the current segment?
                    int remainingInSegment = segment.Length - CurrentSegmentOffset;
                    // how many bytes can be copied in this loop iteration?
                    int size = Math.Min(remaining, remainingInSegment);
                    // copy the bytes from the segment to the buffer
                    Buffer.BlockCopy(segment, CurrentSegmentOffset, buffer, offset, size);
                    // update counters
                    offset += size;
                    read += size;
                    remaining -= size;
                    remainingInSegment -= size;
                    CurrentSegmentOffset += size;
                    // if the entire segment has been read, remove it
                    if (remainingInSegment == 0)
                    {
                        ReadBuffer.TryDequeue(out _);
                        CurrentSegmentOffset = 0;
                    }
                }
                else if (BlockingReads)
                {
                    if (read > 0)
                    {
                        // at least some data was read. return control to caller
                        break;
                    }
                    else
                    {
                        // otherwise continue blocking until data is available
                        ThreadHelpers.Yield();
                    }
                }
                else
                {
                    // no more data is available
                    break;
                }
            }
            return read;
        }

        /// <summary>
        /// Writes the provided buffer to the internal queue for subsequent
        /// transmission on the underlying connection.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (WriteBuffer)
            {
                WriteBuffer.Write(buffer, offset, count);
            }
        }

        /// <summary>
        /// Create a new channel using the given connection and with the given (unique) channel number.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="channelNumber"></param>
        internal Channel(Connection connection, ushort channelNumber)
        {
            Connection = connection;
            ChannelNumber = channelNumber;
            WriteThread = new Thread(() => WriteLoop());
            WriteThread.Start();
            ReadThread = new Thread(() => ReadLoop());
            ReadThread.Start();
        }

        /// <summary>
        /// Append the given data to the end of the read buffer to be later consumed via Read().
        /// </summary>
        /// <param name="bytes"></param>
        internal void Receive(byte[] bytes)
        {
            // when the connection receives data for this channel, enqueue it to be later read by a consumer
            ReadBuffer.Enqueue(bytes);
            ThreadHelpers.Debug($"Channel {ChannelNumber} {Name} received {bytes.Length} bytes Q={ReadBuffer.Count}");
        }

        /// <summary>
        /// A finalizer is necessary when inheriting from Stream.
        /// </summary>
        ~Channel()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (Interlocked.Read(ref IsDisposed) == 0)
            {
                // make sure all pending data is sent before stopping the write thread
                Flush();
                ThreadHelpers.Debug($"Disposing Channel {ChannelNumber} {Name}");
                // signal the threads to stop
                Interlocked.Exchange(ref IsDisposed, 1);
                // wait for the threads to finish
                WriteThread!.Join();
                ReadThread!.Join();
            }

            GC.SuppressFinalize(this);

            Connection.RemoveChannel(this);

            if (disposing)
            {
                // dispose any managed objects
                WriteBuffer.Dispose();

                // propagate any exceptions
                var exceptions = new List<Exception>();
                if (ReadThreadException != null)
                {
                    exceptions.Add(ReadThreadException);
                }
                if (WriteThreadException != null)
                {
                    exceptions.Add(WriteThreadException);
                }
                if (exceptions.Count > 0)
                {
                    throw new AggregateException(exceptions);
                }
            }
        }

        /// <summary>
        /// Continuously monitors for new channel data to trigger its consumption.
        /// </summary>
        private void ReadLoop()
        {
            try
            {
                // loop forever until the object is disposed or the connection is closed
                while (Interlocked.Read(ref IsDisposed) == 0 && IsConnected)
                {
                    if (IsDataAvailable)
                    {
                        ThreadHelpers.Debug($"Channel {ChannelNumber} {Name} data available");
                        // NOTE by design this will block: the handler is meant to process
                        // the incoming data serially in a single thread
                        OnDataAvailable?.Invoke(this);
                    }
                    ThreadHelpers.Yield();
                }
                ThreadHelpers.Debug($"channel {ChannelNumber} {Name} read loop stopping readbuffer={ReadBuffer.Count}");
            }
            catch (Exception e)
            {
                ReadThreadException = e;
            }
        }

        /// <summary>
        /// Continuously monitors for new channel data to write to the underlying connection.
        /// </summary>
        private void WriteLoop()
        {
            try
            {
                // loop forever until the object is disposed or the connection is closed
                while (Interlocked.Read(ref IsDisposed) == 0 && IsConnected)
                {
                    lock (WriteBuffer)
                    {
                        // write the contents of the buffer to the underlying connection
                        // as one or more channel data frames
                        int offset = 0;
                        int remaining = (int)WriteBuffer.Length;
                        while (remaining > 0 && IsConnected)
                        {
                            int size = Math.Min(remaining, Frame.MaxFrameSize);
                            var bytes = new byte[size];
                            Buffer.BlockCopy(WriteBuffer.GetBuffer(), offset, bytes, 0, size);
                            Connection.EnqueueFrame(new ChannelDataFrame(ChannelNumber, bytes));
                            remaining -= size;
                            offset += size;
                        }
                        // reset the buffer
                        WriteBuffer.Position = 0;
                        WriteBuffer.SetLength(0);
                    }
                    ThreadHelpers.Yield();
                }
                ThreadHelpers.Debug($"channel {ChannelNumber} {Name} write loop stopping writebuffer={WriteBuffer.Length}");
            }
            catch (Exception e)
            {
                WriteThreadException = e;
            }
        }
    }
}