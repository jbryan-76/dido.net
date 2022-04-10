using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace AnywhereNET
{
    /// <summary>
    /// <para/>Note this implementation is not thread-safe.
    /// </summary>
    internal class RingBuffer : Stream
    {
        private List<byte[]> Segments = new List<byte[]>();

        private int CurrentSegmentOffset = 0;

        private int CurrentSegmentIndex = 0;

        private long CurrentReadPosition = 0;

        //private long CanReadLength = 0;

        private long TotalLength = 0;

        public override long Length { get { return TotalLength; } }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        /// <summary>
        /// Indicates the position from which reading will commence.
        /// Writes are always appended to the end of the stream.
        /// <para/>Note this value can decrease after a Truncate().
        /// </summary>
        public override long Position
        {
            // TODO: this is breaking things: Stream.Position and Stream.Length for TryReadBytes assumes position is from the absolute beginning
            get { return CurrentReadPosition; }
            set { Seek(value, SeekOrigin.Begin); }
        }

        public void Clear()
        {
            Segments.Clear();
            //CanReadLength = 0;
            CurrentReadPosition = 0;
            CurrentSegmentOffset = 0;
            CurrentSegmentIndex = 0;
            TotalLength = 0;
        }

        /// <summary>
        /// Delete all segments before the current position.
        /// </summary>
        public void Truncate()
        {
            for (int i = 0; i < CurrentSegmentIndex; i++)
            {
                var segment = Segments[0];
                CurrentReadPosition -= segment.Length;
                TotalLength -= segment.Length;
                Segments.RemoveAt(0);
            }
            CurrentSegmentIndex = 0;
            Console.WriteLine($"truncated:  {Segments.Count} segments, @ {CurrentReadPosition} ({Length} length)");
        }

        public override void Flush() { }

        // Reads from the current position, but DOES NOT AUTO-CONSUME the data. You must call Truncate() to discard.
        // returns 0 and does not advance the position if there is not enough data in the buffer to complete the read
        public override int Read(byte[] buffer, int offset, int count)
        {
            // if there is not enough data to read, return 0
            if (Length - CurrentReadPosition < count || Segments.Count == 0)
            {
                Console.WriteLine($"==Asked to read {count} but not enough: @{CurrentReadPosition}, {Length} length, {Segments.Count} segments");
                return 0;
            }

            // iteratively copy from the current position
            int read = 0;
            var remaining = count;
            while (remaining > 0)
            {
                var segment = Segments[CurrentSegmentIndex];
                // how many bytes are left to read in the current segment
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
                CurrentReadPosition += size;
                //CanReadLength -= size;
                // if the entire segment has been read, advance to the next one
                if (remainingInSegment == 0)
                {
                    // TODO: make it a configuration option whether to auto-discard (consume) segments, or make it manual
                    if (remaining > 0 && CurrentSegmentIndex + 1 >= Segments.Count)
                    {
                        // this should be impossible, but keep it to be robust
                        throw new EndOfStreamException();
                    }
                    CurrentSegmentIndex++;
                    CurrentSegmentOffset = 0;
                    Console.WriteLine($"read complete segment: current segment = {CurrentSegmentIndex}, {Length - CurrentReadPosition} bytes left in stream");
                }
            }
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var newPosition = CurrentReadPosition;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.End:
                    newPosition = Length - offset;
                    break;
                case SeekOrigin.Current:
                    newPosition += offset;
                    break;
            }
            if (newPosition == CurrentReadPosition)
            {
                return CurrentReadPosition;
            }
            else if (newPosition < 0 || newPosition > Length)
            {
                throw new IndexOutOfRangeException($"Position {Position} is outside the bounds of the buffer.");
            }

            // update the tracking fields
            //CanReadLength -= (newPosition - Position);
            CurrentReadPosition = newPosition;

            // determine the new segment offset and index
            long bytesFromFront = 0;
            CurrentSegmentOffset = 0;
            for (CurrentSegmentIndex = 0; CurrentSegmentIndex < Segments.Count; CurrentSegmentIndex++)
            {
                var segment = Segments[CurrentSegmentIndex];
                if (CurrentReadPosition < bytesFromFront + segment.Length)
                {
                    CurrentSegmentOffset = (int)(CurrentReadPosition - bytesFromFront);
                    break;
                }
                bytesFromFront += segment.Length;
            }
            Console.WriteLine($"Seeked to {CurrentReadPosition} ({Length} length)");
            return CurrentReadPosition;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // copy the indicated byterange and append to the segments list
            var bytes = new byte[count];
            Array.Copy(buffer, offset, bytes, 0, count);
            Segments.Add(bytes);
            //CanReadLength += count;
            TotalLength += count;
            Console.WriteLine($"added segment ({count} bytes): {Segments.Count} segments, @ {CurrentReadPosition} ({Length} length)");
        }
    }

    public class Connection : IDisposable
    {
        // TODO: should this be configurable?
        internal static readonly int HeartbeatPeriodInMs = 60000; // one minute

        internal delegate void FrameMonitor(Frame frame);

        internal FrameMonitor? UnitTestReceiveFrameMonitor;

        internal FrameMonitor? UnitTestTransmitFrameMonitor;

        private readonly TcpClient Client;

        private readonly SslStream Stream;

        private readonly ConcurrentQueue<Frame> FrameQueue = new ConcurrentQueue<Frame>();

        private Thread? ReadThread;

        private Thread? WriteThread;

        private Timer? HeartbeatTimer;

        private Exception? ReadThreadException;

        private Exception? WriteThreadException;

        private MemoryStream /*ReadBu*/ffer = new MemoryStream();

        private RingBuffer ReadBuffer2 = new RingBuffer();

        private DateTimeOffset? LastRemoteTraffic;

        private long Connected = 0;

        private long IsDisconnecting = 0;

        private long HeartbeatPending = 0;

        private bool IsDisposed = false;

        private ConcurrentDictionary<ushort, Channel> Channels = new ConcurrentDictionary<ushort, Channel>();

        public string Name { get; private set; } = "";

        /// <summary>
        /// Indicates whether the connection is active and healthy.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return Interlocked.Read(ref Connected) == 1;
            }
        }

        /// <summary>
        /// Create a new secure connection as a server using the provided client and certificate.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="serverCertificate"></param>
        public Connection(TcpClient client, X509Certificate2 serverCertificate, string? name = null)
        {
            Name = name ?? "";
            Client = client;

            Log("creating sslstream as server");
            // if a certificate is supplied, the connection is implied to be a server
            Stream = new SslStream(client.GetStream(), false);

            try
            {
                Log("stream created - authenticating");
                // Authenticate the server but don't require the client to authenticate.
                Stream.AuthenticateAsServer(serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: true);
                Log("authenticated");

                FinishConnecting();
            }
            catch (AuthenticationException e)
            {
                Log($"Exception: {e.Message}");
                if (e.InnerException != null)
                {
                    Log($"  Inner exception: {e.InnerException.Message}");
                }
                Log("Authentication failed");
                throw;
            }
        }

        /// <summary>
        /// Create a new secure connection using the provided client and connected to the provided host server.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="targetHost"></param>
        public Connection(TcpClient client, string targetHost, string? name = null)
        {
            Name = name ?? "";
            Client = client;

            Log("creating sslstream as client");
            // otherwise the connection is implied to be a client
            Stream = new SslStream(
                Client.GetStream(),
                false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate),
                null
                );

            try
            {
                Log($"Authenticating to host {targetHost}.");
                Stream.AuthenticateAsClient(targetHost);

                FinishConnecting();
            }
            catch (AuthenticationException e)
            {
                Log($"Exception: {e.Message}");
                if (e.InnerException != null)
                {
                    Log($"  Inner exception: {e.InnerException.Message}");
                }
                Log("Authentication failed");
                throw;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Read(ref Connected) == 1)
            {
                Disconnect();
            }

            Log("Starting dispose...");
            var exceptions = new List<Exception>();
            if (!IsDisposed)
            {
                IsDisposed = true;

                if (ReadThreadException != null)
                {
                    exceptions.Add(ReadThreadException);
                }
                if (WriteThreadException != null)
                {
                    exceptions.Add(WriteThreadException);
                }

                Stream.Dispose();
                ReadBuffer2.Dispose();
                HeartbeatTimer?.Dispose();
            }

            GC.SuppressFinalize(this);

            Log("...Disposed");
            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
        }

        public void Disconnect()
        {
            if (Interlocked.Read(ref Connected) == 1)
            {
                Log("Starting disconnect...");

                // prevent any new channels from being created while disconnecting
                Interlocked.Exchange(ref IsDisconnecting, 1);

                // remove and dispose all channels to force them to flush any remaining data
                ushort[] keys;
                while ((keys = Channels.Keys.ToArray()).Length > 0)
                {
                    foreach (var key in keys)
                    {
                        if (Channels.TryRemove(key, out var channel))
                        {
                            channel.Dispose();
                        }
                    }
                }

                Flush();

                // signal all threads to stop
                Interlocked.Exchange(ref Connected, 0);

                // wait for the threads to finish
                ReadThread!.Join(1000);
                WriteThread!.Join(1000);

                Log("...Disconnected");
            }
            // gracefully attempt to shut down the other side of the connection by sending a disconnect frame
            try
            {
                var frame = new DisconnectFrame();
                UnitTestTransmitFrameMonitor?.Invoke(frame);
                WriteFrame(frame);
            }
            catch (Exception ex)
            {
                // swallow and ignore any errors: the disconnect is just to be polite.
                // (if the other side already disconnected, the write will fail)
            }
        }

        public void Debug(string message)
        {
            FrameQueue.Enqueue(new DebugFrame(message));
        }

        /// <summary>
        /// Gets a reference to the indicated channel.
        /// <para/>Note: DO NOT Dispose() the channel. Since the channel is owned by the Connection,
        /// Connection.Dispose() will dispose it implicitly.
        /// </summary>
        /// <param name="channelNumber"></param>
        /// <returns></returns>
        public Channel GetChannel(ushort channelNumber)
        {
            if (Interlocked.Read(ref Connected) != 1
                || Interlocked.Read(ref IsDisconnecting) == 1)
            {
                throw new InvalidOperationException("Can't create channel: not connected.");
            }
            Channels.TryAdd(channelNumber, new Channel(this, channelNumber));
            return Channels[channelNumber];
        }

        internal void EnqueueFrame(Frame frame)
        {
            Log($"enqueuing frame: {frame}");
            FrameQueue.Enqueue(frame);
        }

        internal void EnqueueFrames(IEnumerable<Frame> frames)
        {
            foreach (var frame in frames)
            {
                EnqueueFrame(frame);
            }
        }

        internal void Flush()
        {
            // block until all frames are sent
            while (FrameQueue.Count > 0 && IsConnected)
            {
                Thread.Sleep(1);
            }
        }

        internal void SendHeartbeat()
        {
            // signal to send a heartbeat frame as soon as possible
            Interlocked.Exchange(ref HeartbeatPending, 1);
        }

        private void FinishConnecting()
        {
            Interlocked.Exchange(ref Connected, 1);

            var remote = (IPEndPoint)Client.Client.RemoteEndPoint;
            var local = (IPEndPoint)Client.Client.LocalEndPoint;
            Log($"connected. Local = {local.Address}:{local.Port}. Remote = {remote.Address}:{remote.Port}");

            // start separate threads to read and write data
            ReadThread = new Thread(() => ReadLoop());
            ReadThread.Start();
            WriteThread = new Thread(() => WriteLoop());
            WriteThread.Start();

            // send a heartbeat frame once a minute to keep the connection active
            HeartbeatTimer = new Timer((arg) => SendHeartbeat(), null, HeartbeatPeriodInMs, HeartbeatPeriodInMs);

            Interlocked.Exchange(ref IsDisconnecting, 0);
        }

        /// <summary>
        /// Continuously transmits queued frames until the connection is closed.
        /// </summary>
        private void WriteLoop()
        {
            try
            {
                Log("starting write loop");
                while (Interlocked.Read(ref Connected) == 1)
                {
                    Thread.Sleep(1);

                    // if a heartbeat is pending, send it immediately
                    if (Interlocked.Read(ref HeartbeatPending) == 1)
                    {
                        Interlocked.Exchange(ref HeartbeatPending, 0);
                        WriteFrame(new HeartbeatFrame());
                    }

                    // send all pending frames
                    while (FrameQueue.TryDequeue(out Frame? frame))
                    {
                        UnitTestTransmitFrameMonitor?.Invoke(frame);
                        WriteFrame(frame);
                    }
                }
                Log("exiting write loop");
            }
            catch (Exception e)
            {
                Log($"WriteLoop terminated: Unhandled Exception: {e.GetType()} {e.Message}");
                WriteThreadException = e;
                // force all thread termination on any error
                Interlocked.Exchange(ref Connected, 0);
            }
        }

        /// <summary>
        /// Continously receives frames until the connection is closed.
        /// </summary>
        private void ReadLoop()
        {
            // TODO: expore async read optimization strategies
            // https://devblogs.microsoft.com/pfxteam/awaiting-socket-operations/

            // set a timeout so read methods do not block too long.
            // this allows for periodically checking the Connected status to 
            // exit the loop when this object disconnects
            Stream.ReadTimeout = 100;
            Log("starting read loop");
            try
            {
                // TODO: how big should this buffer be?
                byte[] data = new byte[1024 * 1024];
                while (Interlocked.Read(ref Connected) == 1)
                {
                    try
                    {
                        // read as much data as available and append to the read buffer
                        int read = Stream.Read(data, 0, data.Length);
                        if (read > 0)
                        {
                            ReadBuffer2.Write(data, 0, read);
                            Log($"Filled read buffer with {read} bytes (@{ReadBuffer2.Position} [{ReadBuffer2.Length} bytes])");
                            LastRemoteTraffic = DateTimeOffset.UtcNow;
                        }
                    }
                    catch (IOException e)
                    {
                        // https://stackoverflow.com/questions/37177401/how-to-repeatedly-read-from-net-sslstream-with-a-timeout
                        // if the stream read times out, just ignore:
                        // that is expected if the other end of the connection is not sending any data
                        var socketException = e.InnerException as SocketException;
                        if (socketException != null && socketException.SocketErrorCode == SocketError.TimedOut)
                        {
                            // NOTE it is bad practice to use exception handling to control 
                            // "normal" expected program flow, but in this case there is no way to
                            // cleanly terminate a thread that is blocking indefinitely on
                            // a Stream.Read.
                        }
                        else
                        {
                            throw;
                        }
                    }

                    // try to read a frame from the buffer
                    var frame = TryReadFrame();
                    if (frame == null)
                    {
                        continue;
                    }

                    UnitTestReceiveFrameMonitor?.Invoke(frame);

                    Log($"Received: {frame.ToString()}");

                    if (frame is HeartbeatFrame)
                    {
                        // simply discard the frame:
                        // the heartbeat is used just to prevent connection timeouts
                        // and maintain an active status on the connection
                        Log("discarding heartbeat");
                        continue;
                    }
                    else if (frame is DisconnectFrame)
                    {
                        Log("disconnecting");
                        // signal a disconnect to all threads
                        Interlocked.Exchange(ref Connected, 0);
                    }

                    else if (frame is DebugFrame)
                    {
                        Log($"DEBUG: {(frame as DebugFrame).Message}");
                    }

                    else if (frame is ChannelDataFrame)
                    {
                        var channel = GetChannel(frame.Channel);
                        channel.Receive(frame.Payload);
                    }
                    else
                    {
                        // should be impossible but just in case...
                        throw new InvalidOperationException($"Unknown or unhandled frame received: '{frame.GetType()}'");
                    }
                }
                Log("exiting read loop");
            }
            catch (IOException e)
            {
                Log($"Exception ({e.GetType()}): {e.Message}");
                Log("Client disconnected - closing the connection...");
                // force all thread termination on any error
                Interlocked.Exchange(ref Connected, 0);
            }
            catch (Exception e)
            {
                Log($"ReadLoop terminated: Unhandled Exception: {e.GetType()} {e.Message}");
                ReadThreadException = e;
                // force all thread termination on any error
                Interlocked.Exchange(ref Connected, 0);
            }
        }

        private Frame? TryReadFrame()
        {
            // remember the current buffer position and reset it back to the start
            long position = ReadBuffer2.Position;
            //ReadBuffer.Position = 0;

            // try to read a frame from the buffer
            int length = -1;
            var payload = new byte[0];
            if (ReadBuffer2.TryReadByte(out var type)
                && ReadBuffer2.TryReadUShortBE(out var channel)
                && ReadBuffer2.TryReadIntBE(out length)
                && (length == 0 || ReadBuffer2.TryReadBytes(length, out payload))
                )
            {
                Log($"Read frame from {position} to {ReadBuffer2.Position} ({ReadBuffer2.Position - position} bytes)");
                // a frame was read successfully. clear the buffer up to the current position
                ReadBuffer2.Truncate();
                //ReadBuffer.Position = 0;
                //ReadBuffer.SetLength(0); // <== this is the problem: we're deleting the whole buffer
                // ..and return the frame
                var frame = FrameFactory.Decode(new Frame
                {
                    Type = type,
                    Channel = channel,
                    Length = length,
                    Payload = payload!
                });
                return frame;
            }
            else
            {
                Log($"Could not read frame of payload {length} - returning ring buffer to {position}");
                // a new frame is not yet available. restore the buffer position
                // to its previous value and return null
                ReadBuffer2.Position = position;
                return null;
            }
        }

        private void WriteFrame(Frame frame)
        {
            Stream.WriteByte(frame.Type);
            Stream.WriteUInt16BE(frame.Channel);
            Stream.WriteInt32BE(frame.Length);
            if (frame.Length > 0)
            {
                Stream.Write(frame.Payload, 0, frame.Payload.Length);
            }
            Stream.Flush();
            Log($"Wrote frame {frame}");
        }

        private void Log(string message)
        {
            if (!string.IsNullOrWhiteSpace(Name))
            {
                Console.WriteLine($"[{DateTimeOffset.UtcNow.ToString("o")}] ({Name}) {message}");
            }
            else
            {
                Console.WriteLine($"[{DateTimeOffset.UtcNow.ToString("o")}] {message}");
            }
        }

        // The following method is invoked by the RemoteCertificateValidationDelegate.
        private static bool ValidateServerCertificate(
              object sender,
              X509Certificate? certificate,
              X509Chain? chain,
              SslPolicyErrors sslPolicyErrors)
        {
            Console.WriteLine("Validating server certificate...");
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                Console.WriteLine("   No errors");
                return true;
            }

            Console.WriteLine("   Certificate error: {0}", sslPolicyErrors);

#if DEBUG
            // for self-signed, just return true
            // TODO: make all this better for production use
            Console.WriteLine("   OVERRIDE: ACCEPTING CERTIFICATE AS VALID ANYWAY");
            return true;
#else
            // Do not allow this client to communicate with unauthenticated servers.
            return false;
#endif
        }
    }

}