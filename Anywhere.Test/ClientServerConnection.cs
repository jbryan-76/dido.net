using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace AnywhereNET.Test
{
    class ClientServerConnection : IDisposable
    {
        static readonly string Base64SelfSignedCert = "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";
        static readonly X509Certificate2 ServerCertificate = new X509Certificate2(Convert.FromBase64String(Base64SelfSignedCert), "1234");

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
                // NOTE do not use Sleep(0) here: it causes some kind of weird race condition with the
                // server/client loops running in the thread pool
                ThreadHelpers.Yield();
            } while (ServerRecievedFrames.Count != currentServerReceived + 1
                && ClientTransmittedFrames.Count != currentClientSent + 1);

            // NOTE a sleep here is also necessary due to some kind of concurrent state issue:
            // without it, there are situations where ConcurrentQueue.Count is != 0 but ConcurrentQueue.TryDequeue fails.
            // TODO: determine why there is misbehavior and explore rewriting the threading logic to support testing
            ThreadHelpers.Yield();
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
                // NOTE do not use Sleep(0) here: it causes some kind of weird race condition with the
                // server/client loops running in the thread pool
                ThreadHelpers.Yield();
            } while (ServerTransmittedFrames.Count != currentServerSent + 1
                && ClientRecievedFrames.Count != currentClientReceived + 1);

            // NOTE a sleep here is also necessary due to some kind of concurrent state issue:
            // without it, there are situations where ConcurrentQueue.Count is != 0 but ConcurrentQueue.TryDequeue fails.
            // TODO: determine why there is misbehavior and explore rewriting the threading logic to support testing
            ThreadHelpers.Yield();
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
            var serverTask = StartServer(ServerCertificate, port);
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