using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace AnywhereNET
{
    public class Connection : IDisposable
    {
        internal delegate void FrameMonitor(Frame frame);

        internal FrameMonitor? UnitTestReceiveFrameMonitor;

        internal FrameMonitor? UnitTestTransmitFrameMonitor;

        private readonly TcpClient Client;

        private readonly SslStream Stream;

        private readonly ConcurrentQueue<Frame> FrameQueue = new ConcurrentQueue<Frame>();

        private Thread? ReadThread;

        private Thread? WriteThread;

        private Exception? ReadThreadException;

        private Exception? WriteThreadException;

        private MemoryStream ReadBuffer = new MemoryStream();

        private long Connected = 0;

        private bool IsDisposed = false;

        private Dictionary<ushort, Channel> Channels = new Dictionary<ushort, Channel>();

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

            if (!IsDisposed)
            {
                IsDisposed = true;

                ReadThread!.Join(1000);
                WriteThread!.Join(1000);

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

                Stream.Dispose();
                ReadBuffer.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        public void Disconnect()
        {
            if (Interlocked.Read(ref Connected) == 1)
            {
                // signal all threads to stop
                Interlocked.Exchange(ref Connected, 0);

                // gracefully attempt to shut down the other side of the connection
                // by sending a disconnect frame
                var frame = new DisconnectFrame();
                if (UnitTestTransmitFrameMonitor != null)
                {
                    UnitTestTransmitFrameMonitor(frame);
                }
                WriteFrame(frame);
            }
        }

        public void Debug(string message)
        {
            FrameQueue.Enqueue(new DebugFrame(message));
        }

        public Channel GetChannel(ushort channelNumber)
        {
            if (!Channels.ContainsKey(channelNumber))
            {
                Channels.Add(channelNumber, new Channel(this, channelNumber));
            }
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
                FrameQueue.Enqueue(frame);
            }
        }

        private void FinishConnecting()
        {
            Connected = 1;
            var remote = (IPEndPoint)Client.Client.RemoteEndPoint;
            var local = (IPEndPoint)Client.Client.LocalEndPoint;
            Log($"connected. Local = {local.Address}:{local.Port}. Remote = {remote.Address}:{remote.Port}");

            // start separate threads to read and write data
            ReadThread = new Thread(() => ReadLoop());
            ReadThread.Start();
            WriteThread = new Thread(() => WriteLoop());
            WriteThread.Start();
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
                    if (FrameQueue.TryDequeue(out Frame? frame) && frame != null)
                    {
                        if (UnitTestTransmitFrameMonitor != null)
                        {
                            UnitTestTransmitFrameMonitor(frame);
                        }
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
                byte[] data = new byte[4096];
                while (Interlocked.Read(ref Connected) == 1)
                {
                    try
                    {
                        // read as much data is available and append to the read buffer
                        int read = Stream.Read(data, 0, data.Length);
                        if (read > 0)
                        {
                            ReadBuffer.Write(data, 0, read);
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
                            // "normal" program flow, but in this case there is no way to
                            // cleanly terminate a thread that is blocking indefinitely on
                            // a Stream.Read.
                            continue;
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

                    if (UnitTestReceiveFrameMonitor != null)
                    {
                        UnitTestReceiveFrameMonitor(frame);
                    }

                    Log($"Received: {frame.ToString()}");

                    if (frame is DisconnectFrame)
                    {
                        Log("disconnecting");
                        Interlocked.Exchange(ref Connected, 0);
                    }

                    else if (frame is DebugFrame)
                    {
                        Log($"DEBUG: {(frame as DebugFrame).Message}");
                    }

                    else
                    {
                        var channel = GetChannel(frame.Channel);
                        channel.Receive(frame.Payload);
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
            long position = ReadBuffer.Position;
            ReadBuffer.Position = 0;

            // try to read a frame from the buffer
            if (ReadBuffer.TryReadByte(out var type)
                && ReadBuffer.TryReadUShortBE(out var channel)
                && ReadBuffer.TryReadIntBE(out var length)
                && ReadBuffer.TryReadBytes(length, out var payload)
                )
            {
                // a frame was read successfully. clear the buffer...
                ReadBuffer.Position = 0;
                ReadBuffer.SetLength(0);
                // ..and return the frame
                return FrameFactory.Decode(new Frame
                {
                    Type = type,
                    Channel = channel,
                    Length = length,
                    Payload = payload!
                });
            }
            else
            {
                // a new frame is not yet available. restore the buffer position and return null
                ReadBuffer.Position = position;
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
                //stream.Write(frame.Payload.Array, frame.Payload.Offset, frame.Payload.Count);
                Stream.Write(frame.Payload, 0, frame.Payload.Length);
                //Console.WriteLine("wrote bytes=" + String.Join(' ', frame.Payload.ToArray().Select(b => b.ToString())));
            }
            Stream.Flush();
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