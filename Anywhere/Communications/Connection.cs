using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace AnywhereNET
{
    /// <summary>
    /// Represents a single bidirectional communications connection between a server and client.
    /// </summary>
    public class Connection : IDisposable
    {
        /// <summary>
        /// Signature for a method that is invoked when a frame is transmitted or received. 
        /// </summary>
        /// <param name="frame"></param>
        internal delegate void FrameMonitor(Frame frame);

        /// <summary>
        /// How long to wait between sending hearbeat frames.
        /// </summary>
        public static int DefaultHeartbeatPeriodInMs = 60000; // one minute

        /// <summary>
        /// The optional name for the connection.
        /// </summary>
        public string Name { get; private set; } = "";

        /// <summary>
        /// Indicates whether the connection is connected to the remote endpoint.
        /// </summary>
        public bool IsConnected
        {
            get { return Interlocked.Read(ref Connected) == 1; }
        }

        /// <summary>
        /// Internal handler for unit tests to monitor received frames.
        /// </summary>
        internal FrameMonitor? UnitTestReceiveFrameMonitor;

        /// <summary>
        /// Internal handler for unit tests to monitor transmitted frames.
        /// </summary>
        internal FrameMonitor? UnitTestTransmitFrameMonitor;

        /// <summary>
        /// The underlying connection endpoint.
        /// </summary>
        private readonly TcpClient EndPoint;

        /// <summary>
        /// The underlying secure socket stream.
        /// </summary>
        private readonly SslStream Stream;

        /// <summary>
        /// A queue of frames to write to the underlying connection endpoint.
        /// </summary>
        private readonly ConcurrentQueue<Frame> WriteFrameQueue = new ConcurrentQueue<Frame>();

        /// <summary>
        /// A thread responsible for reading data from the underlying connection endpoint.
        /// </summary>
        private Thread? ReadThread;

        /// <summary>
        /// A thread responsible for writing queued data to the underlying connection endpoint.
        /// </summary>
        private Thread? WriteThread;

        /// <summary>
        /// A timer to periodically write a HeartbeatFrame to maintain an active connection.
        /// </summary>
        private Timer? HeartbeatTimer;

        /// <summary>
        /// Any exception thrown by the ReadThread, to be held and re-thrown when the thread joins.
        /// </summary>
        private Exception? ReadThreadException;

        /// <summary>
        /// Any exception thrown by the WriteThread, to be held and re-thrown when the thread joins.
        /// </summary>
        private Exception? WriteThreadException;

        /// <summary>
        /// A queue of data received by the connection that is held until it can be
        /// read and processed as a complete Frame.
        /// </summary>
        private QueueBufferStream ReadBuffer = new QueueBufferStream(false);

        /// <summary>
        /// Contains the timestamp of the last data received from the remote connection.
        /// </summary>
        private DateTimeOffset? LastRemoteTraffic;

        /// <summary>
        /// Used with Interlocked to indicate whether the object instance is connected
        /// and the read and write threads should run.
        /// </summary>
        private long Connected = 0;

        /// <summary>
        /// Used with Interlocked to indicate whether the object instance is disconnecting,
        /// to prevent any new channels from being created.
        /// </summary>
        private long IsDisconnecting = 0;

        /// <summary>
        /// Used with Interlocked to indicate whether a heartbeat frame should be sent ASAP.
        /// </summary>
        private long HeartbeatPending = 0;

        /// <summary>
        /// Fulfills IDispose to indicate whether the object is disposed.
        /// </summary>
        private bool IsDisposed = false;

        /// <summary>
        /// The heartbeat period the remote end of this connection is using.
        /// (Each side of the connection may have different heartbeat periods).
        /// </summary>
        private int RemoteHeartbeatPeriodInMs = DefaultHeartbeatPeriodInMs;

        /// <summary>
        /// A thread-safe collection of all Channels.
        /// </summary>
        private ConcurrentDictionary<ushort, Channel> Channels = new ConcurrentDictionary<ushort, Channel>();

        /// <summary>
        /// Create a new secure connection in a server role using the provided endpoint 
        /// and certificate for encryption.
        /// </summary>
        /// <param name="endpoint">The TcpClient connected to the remote client endpoint.</param>
        /// <param name="serverCertificate">The certificate to encrypt the connection.</param>
        /// <param name="name">The optional name for the connection.</param>
        public Connection(TcpClient endpoint, X509Certificate2 serverCertificate, string? name = null)
        {
            Name = name ?? "";
            EndPoint = endpoint;

            Log("creating sslstream as server");
            // if a certificate is supplied, the connection is implied to be a server
            Stream = new SslStream(endpoint.GetStream(), false);

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
        /// Create a new secure connection in a client role using the provided endpoint
        /// and connected to the provided host server.
        /// </summary>
        /// <param name="endpoint">The TcpClient connected to the remote server endpoint.</param>
        /// <param name="targetHost">The hostname of the remote server which is used to authenticate the connection.</param>
        /// <param name="name">The optional name for the connection.</param>
        public Connection(TcpClient endpoint, string targetHost, string? name = null)
        {
            Name = name ?? "";
            EndPoint = endpoint;

            Log("creating sslstream as client");
            // otherwise the connection is implied to be a client
            Stream = new SslStream(
                EndPoint.GetStream(),
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

        /// <summary>
        /// Cleans up and disposes all managed and unmanaged resources.
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        public void Dispose()
        {
            if (Interlocked.Read(ref Connected) == 1)
            {
                Disconnect();
            }

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
                ReadBuffer.Dispose();
                HeartbeatTimer?.Dispose();
            }

            GC.SuppressFinalize(this);

            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
        }

        /// <summary>
        /// Disconnect the remote endpoint and terminate the connection.
        /// </summary>
        public void Disconnect()
        {
            if (Interlocked.Read(ref Connected) == 1)
            {
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
            }
            // gracefully attempt to shut down the other side of the connection by sending a disconnect frame
            try
            {
                WriteFrame(new DisconnectFrame());
            }
            catch (Exception ex)
            {
                // swallow and ignore any errors: the disconnect is just to be polite.
                // (if the other side already disconnected, the write will fail)
            }
        }

        /// <summary>
        /// Transmit a debug message to the remote endpoint.
        /// </summary>
        /// <param name="message"></param>
        public void Debug(string message)
        {
            WriteFrameQueue.Enqueue(new DebugFrame(message));
        }

        /// <summary>
        /// Gets or creates a reference to the indicated channel.
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

        /// <summary>
        /// Disposes the given channel, freeing its resources.
        /// </summary>
        /// <param name="channel"></param>
        internal void CloseChannel(Channel channel)
        {
            if (Channels.TryRemove(channel.ChannelNumber, out var c))
            {
                c.Dispose();
            }
        }

        /// <summary>
        /// Enqueue the given frame for transmission to the remote endpoint.
        /// </summary>
        /// <param name="frame"></param>
        internal void EnqueueFrame(Frame frame)
        {
            WriteFrameQueue.Enqueue(frame);
        }

        /// <summary>
        /// Enqueue the given frames for transmission to the remote endpoint.
        /// </summary>
        /// <param name="frames"></param>
        internal void EnqueueFrames(IEnumerable<Frame> frames)
        {
            foreach (var frame in frames)
            {
                EnqueueFrame(frame);
            }
        }

        /// <summary>
        /// Blocks until all queued frames are transmitted.
        /// <para/>Note if another thread is continually enqueuing frames this method
        /// may not return, so use with care.
        /// </summary>
        internal void Flush()
        {
            // block until all frames are sent
            while (WriteFrameQueue.Count > 0 && IsConnected)
            {
                ThreadHelpers.Yield();
            }
        }

        /// <summary>
        /// Send a heartbeat frame to the remote endpoint.
        /// </summary>
        internal void SendHeartbeat()
        {
            // signal to send a heartbeat frame as soon as possible
            Interlocked.Exchange(ref HeartbeatPending, 1);
        }

        /// <summary>
        /// Finish internal bookkeeping once the remote connection is authenticated.
        /// </summary>
        private void FinishConnecting()
        {
            Interlocked.Exchange(ref Connected, 1);

            var remote = (IPEndPoint)EndPoint.Client.RemoteEndPoint;
            var local = (IPEndPoint)EndPoint.Client.LocalEndPoint;
            Log($"connected. Local = {local.Address}:{local.Port}. Remote = {remote.Address}:{remote.Port}");

            // start separate threads to read and write data
            ReadThread = new Thread(() => ReadLoop());
            ReadThread.Start();
            WriteThread = new Thread(() => WriteLoop());
            WriteThread.Start();

            // send a heartbeat frame periodically to keep the connection active
            HeartbeatTimer = new Timer((arg) => SendHeartbeat(), null, 0, DefaultHeartbeatPeriodInMs);

            Interlocked.Exchange(ref IsDisconnecting, 0);
        }

        /// <summary>
        /// Continuously transmits queued frames until the connection is closed.
        /// </summary>
        private void WriteLoop()
        {
            try
            {
                while (Interlocked.Read(ref Connected) == 1)
                {
                    // if a heartbeat is pending, send it immediately
                    if (Interlocked.Read(ref HeartbeatPending) == 1)
                    {
                        var frame = new HeartbeatFrame(DefaultHeartbeatPeriodInMs);
                        Interlocked.Exchange(ref HeartbeatPending, 0);
                        WriteFrame(frame);
                    }

                    // send all pending frames
                    while (WriteFrameQueue.TryDequeue(out Frame? frame))
                    {
                        UnitTestTransmitFrameMonitor?.Invoke(frame);
                        WriteFrame(frame);
                    }

                    ThreadHelpers.Yield();
                }
            }
            catch (Exception e)
            {
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
            // exit the loop when either this object or the remote side disconnects.
            Stream.ReadTimeout = 100;
            try
            {
                // TODO: how big should this buffer be?
                // empirically it appears that 32k is standard for the underlying network classes
                byte[] data = new byte[32 * 1024];
                while (Interlocked.Read(ref Connected) == 1)
                {
                    try
                    {
                        // read as much data as available and append to the read buffer
                        int read = Stream.Read(data, 0, data.Length);
                        if (read > 0)
                        {
                            ReadBuffer.Write(data, 0, read);
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
                            // NOTE it is an antipattern to use exception handling to control 
                            // "normal" expected program flow, but in this case there is no way to
                            // cleanly terminate a thread that is blocking indefinitely on
                            // a Stream.Read.
                        }
                        else
                        {
                            throw;
                        }
                    }

                    // if no traffic is seen from the remote side for more than
                    // twice the remote's heartbeat period (ie RemoteHeartbeatPeriodInMs),
                    // force a disconnect
                    TimeSpan remoteIdlePeriod = LastRemoteTraffic.HasValue
                        ? DateTimeOffset.UtcNow - LastRemoteTraffic.Value
                        : TimeSpan.FromMilliseconds(0);
                    if (remoteIdlePeriod.TotalMilliseconds > 2 * RemoteHeartbeatPeriodInMs)
                    {
                        Interlocked.Exchange(ref Connected, 0);
                        Log($"Forcing disconnect: remote connection was idle for more than {RemoteHeartbeatPeriodInMs}ms");
                        break;
                    }

                    // try to read a frame from the buffer
                    var frame = TryReadFrame();
                    if (frame == null)
                    {
                        continue;
                    }

                    if (frame is HeartbeatFrame)
                    {
                        // remember the heartbeat period the other side is using
                        // so it can be used to determine if the other end becomes unresponsive
                        RemoteHeartbeatPeriodInMs = (frame as HeartbeatFrame)!.PeriodInMs;
                    }
                    else if (frame is DisconnectFrame)
                    {
                        // signal a disconnect to all threads
                        Interlocked.Exchange(ref Connected, 0);
                    }
                    else if (frame is DebugFrame)
                    {
                        Log($"DEBUG: {(frame as DebugFrame)!.Message}");
                        // TODO: add a configurable handler to process the received debug message
                        UnitTestReceiveFrameMonitor?.Invoke(frame);
                    }
                    else if (frame is ChannelDataFrame)
                    {
                        var channel = GetChannel(frame.Channel);
                        channel.Receive(frame.Payload);
                        UnitTestReceiveFrameMonitor?.Invoke(frame);
                    }
                    else
                    {
                        // should be impossible but just in case...
                        throw new InvalidOperationException($"Unknown or unhandled frame received: '{frame.GetType()}'");
                    }
                }
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

        /// <summary>
        /// Try to read a frame from the read buffer, returning null if a full frame is not yet
        /// present in the buffer, else the strongly typed frame instance.
        /// </summary>
        /// <returns></returns>
        private Frame? TryReadFrame()
        {
            // remember the current buffer position
            long position = ReadBuffer.Position;

            // try to read a frame from the buffer
            int length = -1;
            var payload = new byte[0];
            if (ReadBuffer.TryReadByte(out var type)
                && ReadBuffer.TryReadUShortBE(out var channel)
                && ReadBuffer.TryReadIntBE(out length)
                && (length == 0 || ReadBuffer.TryReadBytes(length, out payload))
                )
            {
                // a frame was read successfully. clear/consume the buffer up to the current position...
                ReadBuffer.Truncate();
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
                // a new frame is not yet available.
                // restore the buffer position to its previous value and return null
                ReadBuffer.Position = position;
                return null;
            }
        }

        /// <summary>
        /// Write the provided frame to the underlying SslStream.
        /// </summary>
        /// <param name="frame"></param>
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
        }

        // TODO: add a configurable ILogger
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

        /// <summary>
        /// Invoked by the RemoteCertificateValidationDelegate to authenticate the server endpoint.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
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