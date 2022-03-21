using System.Collections.Concurrent;

namespace AnywhereNET
{
    public class Channel : Stream
    {
        public delegate void DataAvailableHandler(Channel channel);

        public event DataAvailableHandler? OnDataAvailable;

        private long IsDisposed = 0;

        private MemoryStream WriteBuffer = new MemoryStream();
        
        private Thread? WriteThread;
        
        private Exception? WriteThreadException;

        private ConcurrentQueue<byte[]> DataQueue = new ConcurrentQueue<byte[]>();
        
        private int CurrentSegmentOffset = 0;

        public ushort ChannelNumber { get; private set; }

        public Connection Connection { get; private set; }

        public bool IsDataAvailable { get { return !DataQueue.IsEmpty; } }

        public bool IsConnected { get { return Connection != null && Connection.IsConnected; } }

        internal Channel(Connection connection, ushort channelNumber)
        {
            Connection = connection;
            ChannelNumber = channelNumber;
            WriteThread = new Thread(() => WriteLoop());
            WriteThread.Start();
        }

        /// <summary>
        /// Finalizer is necessary when inheriting from Stream.
        /// </summary>
        ~Channel()
        {
            Dispose(false);
        }

        public Task WaitForDataAsync()
        {
            var source = new TaskCompletionSource();
            Task.Run(() =>
            {
                while (!IsDataAvailable)
                {
                    Thread.Sleep(1);

                    if (!IsConnected)
                    {
                        source.SetException(new InvalidOperationException("Channel disconnected"));
                    }
                }
                source.SetResult();
            });
            return source.Task;
        }

        public override bool CanRead => true;

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = 0;
            int remaining = count;
            while (remaining > 0)
            {
                // peek at the next available segment
                if (DataQueue.TryPeek(out byte[] segment))
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
                        DataQueue.TryDequeue(out segment);
                        CurrentSegmentOffset = 0;
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

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (WriteBuffer)
            {
                WriteBuffer.Write(buffer, offset, count);
            }
        }

        internal void Receive(byte[] bytes)
        {
            // when the connection receives data for this channel, enqueue it to be later read by a consumer
            DataQueue.Enqueue(bytes);
            // notify all event handlers new data is available
            OnDataAvailable?.Invoke(this);
        }

        /// <summary>
        /// Override of Stream implementation to dispose resources.
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (Interlocked.Read(ref IsDisposed) == 0)
            {
                // signal the write thread to terminate
                Interlocked.Exchange(ref IsDisposed, 1);
                // wait for the write thread to finish
                WriteThread!.Join();
                // propagate any exceptions
                if (WriteThreadException != null)
                {
                    throw WriteThreadException;
                }
            }
            if (disposing)
            {
                // dispose any managed objects
                WriteBuffer.Dispose();
            }
            GC.SuppressFinalize(this);
        }

        private void WriteLoop()
        {
            try
            {
                // loop forever until the object is disposed
                while (Interlocked.Read(ref IsDisposed) == 0)
                {
                    Thread.Sleep(1);
                    lock (WriteBuffer)
                    {
                        int offset = 0;
                        int remaining = (int)WriteBuffer.Length;
                        while (remaining > 0)
                        {
                            int size = Math.Min(remaining, Frame.MaxFrameSize);
                            var bytes = new byte[size];
                            Buffer.BlockCopy(WriteBuffer.GetBuffer(), offset, bytes, 0, size);
                            var frame = new Frame
                            {
                                FrameType = FrameTypes.ChannelData,
                                Channel = ChannelNumber,
                                Length = size,
                                Payload = bytes
                            };
                            Connection.EnqueueFrame(frame);
                            remaining -= size;
                            offset += size;
                        }
                        // reset the buffer
                        WriteBuffer.Position = 0;
                        WriteBuffer.SetLength(0);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Channel Unhandled Exception: {e.GetType()} {e.Message}");
                WriteThreadException = e;
            }
        }
    }

}