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
        private readonly TcpClient Client;
        private readonly SslStream Stream;
        private readonly ConcurrentQueue<Frame> FrameQueue = new ConcurrentQueue<Frame>();

        private Task? ReadTask;
        private Task? WriteTask;

        private bool IsDisposed = false;
        private long IsConnected = 0;

        private Dictionary<ushort, Channel> Channels = new Dictionary<ushort, Channel>();

        /// <summary>
        /// Create a new connection as a server connected to the provided client.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="serverCertificate"></param>
        public Connection(TcpClient client, X509Certificate2 serverCertificate)
        {
            Client = client;

            Console.WriteLine("creating sslstream as server");
            // if a certificate is supplied, the connection is implied to be a server
            Stream = new SslStream(client.GetStream(), false);

            try
            {
                Console.WriteLine("stream created - authenticating");
                // Authenticate the server but don't require the client to authenticate.
                Stream.AuthenticateAsServer(serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: true);
                Console.WriteLine("authenticated");

                FinishConnecting();
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }
                Console.WriteLine("Authentication failed");
                throw;
            }
        }

        /// <summary>
        /// Create a new connection as a client connected to the provided host server.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="targetHost"></param>
        public Connection(TcpClient client, string targetHost)
        {
            Client = client;

            Console.WriteLine("creating sslstream as client");
            // otherwise the connection is implied to be a client
            Stream = new SslStream(
                Client.GetStream(),
                false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate),
                null
                );

            try
            {
                Console.WriteLine("Authenticating to host {0}.", targetHost);
                Stream.AuthenticateAsClient(targetHost);

                FinishConnecting();
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }
                Console.WriteLine("Authentication failed");
                throw;
            }
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                Console.WriteLine("disposing");

                // dispose managed objects
                ReadTask?.Dispose();
                WriteTask?.Dispose();
                IsDisposed = true;

                Console.WriteLine("disposed");
            }
            GC.SuppressFinalize(this);
        }

        public async Task DisconnectAsync()
        {
            // enqueue a "close" (ie graceful disconnect) frame
            FrameQueue.Enqueue(new DisconnectFrame());

            // indicate we are disconnecting
            Console.WriteLine("disconnecting");
            Interlocked.Exchange(ref IsConnected, 0);

            // wait for the tasks to terminate
            Console.WriteLine("awaiting read and write tasks");
            await ReadTask;
            await WriteTask;

            Console.WriteLine("disconnected");
        }

        public async Task DebugAsync(string message)
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
            FrameQueue.Enqueue(frame);
        }

        internal void EnqueueFrames(IEnumerable<Frame> frames)
        {
            foreach (var frame in frames)
            {
                FrameQueue.Enqueue(frame);
            }
        }

        internal delegate void FrameMonitor(Frame frame);
        internal FrameMonitor? UnitTestReceiveFrameMonitor;
        internal FrameMonitor? UnitTestTransmitFrameMonitor;

        private void FinishConnecting()
        {
            IsConnected = 1;
            var remote = (IPEndPoint)Client.Client.RemoteEndPoint;
            var local = (IPEndPoint)Client.Client.LocalEndPoint;
            Console.WriteLine($"connected. Local = {local.Address}:{local.Port}. Remote = {remote.Address}:{remote.Port}");

            // start separate threads to read and write data
            ReadTask = Task.Run(() => ReadLoop());
            WriteTask = Task.Run(() => WriteLoop());
        }

        private async void WriteLoop()
        {
            Console.WriteLine("starting write loop");
            while (Interlocked.Read(ref IsConnected) == 1)
            {
                Thread.Sleep(0);
                if (FrameQueue.TryDequeue(out Frame? frame) && frame != null)
                {
                    if(UnitTestTransmitFrameMonitor != null)
                    {
                        UnitTestTransmitFrameMonitor(frame);
                    }
                    await Stream.WriteFrame(frame);
                }
            }
            Console.WriteLine("exiting write loop");
        }

        private async void ReadLoop()
        {
            Console.WriteLine("starting read loop");
            try
            {
                while (Interlocked.Read(ref IsConnected) == 1)
                {
                    Console.WriteLine("Waiting for next incoming data frame...");

                    // receive the next data frame from the remote connection
                    var rawFrame = await Stream.ReadFrame();

                    // TODO: why is C#8 making this return type nullable?!?!?
                    var frame = FrameFactory.Decode(rawFrame);

                    if (UnitTestReceiveFrameMonitor != null)
                    {
                        UnitTestReceiveFrameMonitor(frame);
                    }

                    Console.WriteLine("Received: {0}", frame);

                    if (frame is DisconnectFrame)
                    {
                        Console.WriteLine("disconnecting");
                        Interlocked.Exchange(ref IsConnected, 0);
                    }

                    else if (frame is DebugFrame)
                    {
                        Console.WriteLine($"DEBUG: {(frame as DebugFrame).Message}");
                    }

                    else
                    {
                        var channel = GetChannel(frame.Channel);
                        channel.Receive(frame.Payload);
                    }
                }
                Console.WriteLine("exiting read loop");
            }
            catch (IOException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                Console.WriteLine("Client disconnected - closing the connection...");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unhandled Exception: {e.GetType()} {e.Message}");
            }
            finally
            {
                // cleanup
                Stream.Close();
                Console.WriteLine("Closed client connection.");
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