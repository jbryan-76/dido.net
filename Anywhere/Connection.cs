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

        //private Task? ReadTask;
        //private Task? WriteTask;

        private Thread? ReadThread;
        private Thread? WriteThread;
        private Exception? ReadThreadException;
        private Exception? WriteThreadException;

        //private bool IsDisposed = false;
        private long Connected = 0;

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
            Console.WriteLine("starting connection dispose");
            if (Interlocked.Read(ref Connected) == 1)
            {
                Console.WriteLine("  still connected");
                // TODO: force disconnect?
                Disconnect();
                Stream.Dispose();
            }

            //if (!IsDisposed)
            //{
            //    Console.WriteLine("disposing connection");

            //    // TODO: dispose any managed objects
            //    IsDisposed = true;

            //}
            Console.WriteLine("disposed connection");
            GC.SuppressFinalize(this);
        }

        public void Disconnect()
        {
            Console.WriteLine("starting connection disconnect");
            // short circuit if already disconnected
            if (Interlocked.Read(ref Connected) == 0)
            {
                return;
            }
            Console.WriteLine("  still connected: sending disconnect frame");

            // gracefully attempt to shut down the other side of the connection
            var frame = new DisconnectFrame();
            if (UnitTestTransmitFrameMonitor != null)
            {
                UnitTestTransmitFrameMonitor(frame);
            }
            Stream.WriteFrame(frame);

            FinishDisconnecting();
        }

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

        private void FinishDisconnecting()
        {
            try
            {
                var exceptions = new List<Exception>();

                Console.WriteLine("FinishDisconnecting");
                // signal all threads to stop then wait for them to terminate
                Interlocked.Exchange(ref Connected, 0);
                ReadThread!.Join(1000);
                WriteThread!.Join(1000);
                //if (!ReadThread!.Join(5000))
                //{
                //    exceptions.Add(new TimeoutException("The read thread did not join after disconnect within the time allotted."));
                //}
                //if (!WriteThread!.Join(5000))
                //{
                //    exceptions.Add(new TimeoutException("The write thread did not join after disconnect within the time allotted."));
                //}

                // report any exceptions that may have occurred
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
            finally
            {
                Stream.Close();

                Console.WriteLine("disconnected");
            }
        }

        //public async Task DisconnectAsync()
        //{
        //    if (Interlocked.Read(ref IsConnected) == 0)
        //    {
        //        return;
        //    }

        //    // enqueue a "close" (ie graceful disconnect) frame
        //    FrameQueue.Enqueue(new DisconnectFrame());

        //    // indicate we are disconnecting
        //    Console.WriteLine("disconnecting");
        //    Interlocked.Exchange(ref IsConnected, 0);

        //    // wait for the tasks to terminate
        //    Console.WriteLine("awaiting read and write tasks");
        //    await ReadTask;
        //    await WriteTask;

        //    Console.WriteLine("disconnected");
        //}

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
            Console.WriteLine($"enqueuing frame: {frame}");
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
            Connected = 1;
            var remote = (IPEndPoint)Client.Client.RemoteEndPoint;
            var local = (IPEndPoint)Client.Client.LocalEndPoint;
            Console.WriteLine($"connected. Local = {local.Address}:{local.Port}. Remote = {remote.Address}:{remote.Port}");

            // start separate threads to read and write data
            //try
            //{
            //ReadTask = Task.Run(() => ReadLoop());
            //WriteTask = Task.Run(() => WriteLoop());

            ReadThread = new Thread(() => ReadLoop());
            ReadThread.Start();
            WriteThread = new Thread(() => WriteLoop());
            WriteThread.Start();
            //}
            //catch (Exception ex)
            //{

            //}
        }

        private async void WriteLoop()
        {
            try
            {
                Console.WriteLine("starting write loop");
                while (Interlocked.Read(ref Connected) == 1)
                {
                    Thread.Sleep(0);
                    // TODO: try to combine multiple channels frames together into a single frame?
                    if (FrameQueue.TryDequeue(out Frame? frame) && frame != null)
                    {
                        if (UnitTestTransmitFrameMonitor != null)
                        {
                            UnitTestTransmitFrameMonitor(frame);
                        }
                        await Stream.WriteFrameAsync(frame);
                    }
                }
                Console.WriteLine("exiting write loop");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unhandled Exception: {e.GetType()} {e.Message}");
                WriteThreadException = e;
                // force all thread termination on any error
                Interlocked.Exchange(ref Connected, 0);
            }
        }

        private async void ReadLoop()
        {
            // TODO: set ReadTimeout and properly handle a blocking read returning 0?
            // TODO: this is tricky if we're waiting for an entire frame. 
            // TODO: regardless, how would we detect the stream closed?
            //Stream.ReadTimeout = 100;
            Console.WriteLine("starting read loop");
            try
            {
                while (Interlocked.Read(ref Connected) == 1)
                {
                    Console.WriteLine("Waiting for next incoming data frame...");

                    // receive the next data frame from the remote connection
                    var rawFrame = await Stream.ReadFrameAsync();

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
                        Interlocked.Exchange(ref Connected, 0);
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
                // force all thread termination on any error
                Interlocked.Exchange(ref Connected, 0);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unhandled Exception: {e.GetType()} {e.Message}");
                ReadThreadException = e;
                // force all thread termination on any error
                Interlocked.Exchange(ref Connected, 0);
            }
            //finally
            //{
            //    // cleanup
            //    //Stream.Close();
            //    Console.WriteLine("Closed client connection.");
            //}
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