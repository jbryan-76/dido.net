using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace SslTestCommon
{
    // TODO: message objects: 
    // TODO: frame types: heartbeat; create object; disconnect

    public interface IMessage
    {
        // Task<Frame[]>

        // TODO: how to do streams? like we could receive a frame to create an object, then add data to its internal (memory stream) buffer
    }

    public class Connection : IDisposable
    {
        private readonly TcpClient Client;
        private readonly SslStream Stream;
        private readonly ConcurrentQueue<Frame> FrameQueue = new ConcurrentQueue<Frame>();

        private Task? ReadTask;
        private Task? WriteTask;

        private bool IsDisposed = false;
        private long Connected = 0;

        public delegate Task FrameHandler(Frame frame);

        public FrameHandler? HandleFrame = null;

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
        /// Create a new connection as a client connected to the provided server.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="serverName"></param>
        public Connection(TcpClient client, string serverName)
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
                Console.WriteLine("Authenticating to host {0}.", serverName);
                Stream.AuthenticateAsClient(serverName);

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

        internal void FinishConnecting()
        {
            Connected = 1;
            var remote = (IPEndPoint)Client.Client.RemoteEndPoint;
            var local = (IPEndPoint)Client.Client.LocalEndPoint;
            Console.WriteLine($"connected. Local = {local.Address}:{local.Port}. Remote = {remote.Address}:{remote.Port}");

            // start separate threads to read and write data
            ReadTask = Task.Run(() => ReadLoop());
            WriteTask = Task.Run(() => WriteLoop());
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                Console.WriteLine("disposing");

                // dispose managed objects
                ReadTask.Dispose();
                WriteTask.Dispose();
                IsDisposed = true;

                Console.WriteLine("disposed");
            }
            GC.SuppressFinalize(this);
        }

        public async Task SendMessageAsync(IMessage message, ushort channel = 0)
        {
            // TODO: break into frames and enqueue
        }

        public async Task DisconnectAsync()
        {
            // TODO: enqueue a "close" (ie graceful disconnect) frame, then close
            FrameQueue.Enqueue(new DisconnectFrame());

            // indicate we are disconnecting
            Console.WriteLine("disconnecting");
            Interlocked.Exchange(ref Connected, 0);

            //?????
            //Stream.Close();
            //Client.Close();

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

        async void WriteLoop()
        {
            Console.WriteLine("starting write loop");
            while (Interlocked.Read(ref Connected) == 1)
            {
                Thread.Sleep(0);
                if (FrameQueue.TryDequeue(out Frame? frame))
                {
                    await Stream.WriteFrame(frame);
                }
            }
            Console.WriteLine("exiting write loop");
        }

        async void ReadLoop()
        {
            Console.WriteLine("starting read loop");
            try
            {
                while (Interlocked.Read(ref Connected) == 1)
                {
                    Console.WriteLine("Waiting for next incoming data frame...");

                    // receive the next data frame from the remote connection
                    var rawFrame = await Stream.ReadFrame();

                    // TODO: why is C#8 making this return type nullable
                    var frame = FrameFactory.Decode(rawFrame);

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

                    // TODO: process the data frame
                    else if (HandleFrame != null)
                    {
                        await HandleFrame(frame);
                    }

                    //byte[] messsage = Encoding.UTF8.GetBytes("ack");

                    //await sslStream.WriteFrame(new Frame
                    //{
                    //    Type = 1,
                    //    Channel = 1,
                    //    Length = messsage.Length,
                    //    Payload = messsage
                    //});
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
                //Client.Close();
                Console.WriteLine("Closed client connection.");
            }
        }

        // The following method is invoked by the RemoteCertificateValidationDelegate.
        static bool ValidateServerCertificate(
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