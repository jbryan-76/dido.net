using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace DidoNet.Test.Common
{
    public class ClientServerConnection : IDisposable
    {
        private bool IsDisposed = false;

        public Connection ServerConnection { get; set; }
        public Connection ClientConnection { get; set; }
        public ConcurrentQueue<Frame> ServerRecievedFrames { get; set; } = new ConcurrentQueue<Frame>();
        public ConcurrentQueue<Frame> ServerTransmittedFrames { get; set; } = new ConcurrentQueue<Frame>();
        public ConcurrentQueue<Frame> ClientRecievedFrames { get; set; } = new ConcurrentQueue<Frame>();
        public ConcurrentQueue<Frame> ClientTransmittedFrames { get; set; } = new ConcurrentQueue<Frame>();

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                Close();
                ServerConnection?.Dispose();
                ClientConnection?.Dispose();
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Send the provided frame from the client to the server and block until the transmission is complete.
        /// </summary>
        /// <param name="frame"></param>
        public void SendClientToServer(Frame frame)
        {
            // note the current transmission counts
            var currentClientSent = ClientTransmittedFrames.Count;
            var currentServerReceived = ServerRecievedFrames.Count;

            if (currentClientSent != 0)
            {
                throw new InvalidOperationException("WTF1");
            }
            if (currentServerReceived != 0)
            {
                throw new InvalidOperationException("WTF2");
            }

            // enqueue the frame for transmission
            ClientConnection.EnqueueFrame(frame);

            // wait for the transfer to complete
            do
            {
                ThreadHelpers.Yield();
            } while (ServerRecievedFrames.Count != currentServerReceived + 1
                && ClientTransmittedFrames.Count != currentClientSent + 1);

            // NOTE a sleep here is also necessary due to some kind of concurrent state issue:
            // without it, there are situations where ConcurrentQueue.Count is != 0 but ConcurrentQueue.TryDequeue fails.
            // TODO: determine why there is misbehavior and explore rewriting the threading logic to support testing
            ThreadHelpers.Yield(100);
        }

        /// <summary>
        /// Send the provided frame from the server to the client and block until the transmission is complete.
        /// </summary>
        /// <param name="frame"></param>
        public void SendServerToClient(Frame frame)
        {
            // note the current transmission counts
            var currentServerSent = ServerTransmittedFrames.Count;
            var currentClientReceived = ClientRecievedFrames.Count;

            // enqueue the frame for transmission
            ServerConnection.EnqueueFrame(frame);

            // wait for the transfer to complete
            do
            {
                ThreadHelpers.Yield();
            } while (ServerTransmittedFrames.Count != currentServerSent + 1
                && ClientRecievedFrames.Count != currentClientReceived + 1);

            // NOTE a sleep here is also necessary due to some kind of concurrent state issue:
            // without it, there are situations where ConcurrentQueue.Count is != 0 but ConcurrentQueue.TryDequeue fails.
            // TODO: determine why there is misbehavior and explore rewriting the threading logic to support testing
            ThreadHelpers.Yield(100);
        }

        /// <summary>
        /// Clear all transmitted and received frame queues.
        /// </summary>
        public void ClearFrames()
        {
            ServerRecievedFrames.Clear();
            ServerTransmittedFrames.Clear();
            ClientRecievedFrames.Clear();
            ClientTransmittedFrames.Clear();
        }

        /// <summary>
        /// Close the connections.
        /// </summary>
        /// <returns></returns>
        public void Close()
        {
            var exceptions = new List<Exception>();
            try
            {
                ClientConnection.Disconnect();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
            try
            {
                ServerConnection.Disconnect();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
        }

        /// <summary>
        /// Create a local loopback client+server system on the specified port.
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public static async Task<ClientServerConnection> CreateAsync(int port)
        {
            var result = new ClientServerConnection();

            // start a server and client asynchronously...
            var serverTask = StartServer(TestSelfSignedCert.ServerCertificate, port);
            var clientTask = ConnectClient(port);

            // ...then wait for their connection to each other to complete
            result.ServerConnection = await serverTask;
            result.ClientConnection = await clientTask;

            // register monitor methods to track sent and received data frames
            result.ServerConnection.UnitTestReceiveFrameMonitor = (frame) => result.ServerRecievedFrames.Enqueue(frame);
            result.ServerConnection.UnitTestTransmitFrameMonitor = (frame) => result.ServerTransmittedFrames.Enqueue(frame);
            result.ClientConnection.UnitTestReceiveFrameMonitor = (frame) => result.ClientRecievedFrames.Enqueue(frame);
            result.ClientConnection.UnitTestTransmitFrameMonitor = (frame) => result.ClientTransmittedFrames.Enqueue(frame);

            return result;
        }

        /// <summary>
        /// Start a local loopback server that yields a connection for the first client that connects to the provided port.
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        static private Task<Connection> StartServer(X509Certificate2 cert, int port)
        {
            return Task.Run(async () =>
            {
                // listen for incoming connections
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();

                // block and wait for the next incoming connection
                var client = await listener.AcceptTcpClientAsync();

                // create a secure connection to the client
                var serverConnection = new Connection(client, cert, "server");
                return serverConnection;
            });
        }

        /// <summary>
        /// Connect to an already running local loopback server at the provided port.
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        static private Task<Connection> ConnectClient(int port)
        {
            var localhost = "localhost";
            return Task.Run(() =>
            {
                // connect to the local server
                var client = new TcpClient(localhost, port);

                // return a secure connection to the server
                var clientConnection = new Connection(client, localhost, "client");
                return clientConnection;
            });
        }
    }
}