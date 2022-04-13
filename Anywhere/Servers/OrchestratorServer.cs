using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace AnywhereNET
{
    // TODO: orchestrator should determine which runner in its pool has resources available to run.
    public class OrchestratorServer
    {
        private long IsRunning = 0;

        private Thread? WorkLoopThread;

        private OrchestratorConfiguration Configuration;

        private bool IsDisposed = false;

        private List<Connection> RunnerPool = new List<Connection>();

        private ConcurrentDictionary<Guid, ConnectedClient> ActiveClients = new ConcurrentDictionary<Guid, ConnectedClient>();

        private ConcurrentQueue<ConnectedClient> CompletedClients = new ConcurrentQueue<ConnectedClient>();

        public OrchestratorServer(OrchestratorConfiguration? configuration = null)
        {
            Configuration = configuration ?? new OrchestratorConfiguration();
        }

        public void Dispose()
        {
            Stop();
            if (!IsDisposed)
            {
                IsDisposed = true;
            }
            GC.SuppressFinalize(this);
        }

        public async Task Start(X509Certificate2 cert, int port, IPAddress? ip = null)
        {
            ip = ip ?? IPAddress.Any;

            // listen for incoming connections
            var listener = new TcpListener(ip, port);
            listener.Start();

            // start a work thread to accept and process new connections
            IsRunning = 1;
            WorkLoopThread = new Thread(() => WorkLoop(listener, cert));
            WorkLoopThread.Start();
        }

        public void Stop()
        {
            // signal the work thread to stop
            Interlocked.Exchange(ref IsRunning, 0);

            // wait for it to finish
            WorkLoopThread?.Join();
            WorkLoopThread = null;

            // cleanup all clients
            DisconnectAndCleanupActiveClients();
            CleanupCompletedClients();
        }

        private void WorkLoop(TcpListener listener, X509Certificate2 cert)
        {
            while (Interlocked.Read(ref IsRunning) == 1)
            {
                CleanupCompletedClients();

                // sleep unless a new connection is pending
                if (!listener.Pending())
                {
                    ThreadHelpers.Yield();
                    continue;
                }

                // block and wait for the next incoming connection
                var client = listener.AcceptTcpClient();

                // create a secure connection to the endpoint
                // and start processing it in a dedicated thread
                var connection = new Connection(client, cert);
                var id = Guid.NewGuid();
                var thread = new Thread(() => ProcessClient(id, connection));
                thread.Start();

                // track the thread/connection pair as an active client
                ActiveClients.TryAdd(id, new ConnectedClient(thread, connection));
            }
        }

        private void CleanupCompletedClients()
        {
            while (CompletedClients.TryDequeue(out ConnectedClient? client))
            {
                client?.Thread.Join(1000);
                client?.Connection.Dispose();
            }
        }

        private void DisconnectAndCleanupActiveClients()
        {
            foreach (var client in ActiveClients.Values)
            {
                client.Connection.Disconnect();
                client.Thread.Join(100);
                client.Connection.Dispose();
            }
        }

        private async void ProcessClient(Guid id, Connection connection)
        {
            // create communication channels.
            // NOTE: by design, exactly one of these channels will receive data
            // depending on whether an application or a runner connected.
            // however, until one of the channels receives data, it's impossible to know which.
            MessageChannel? applicationChannel = new MessageChannel(connection, Constants.ApplicationChannel);
            MessageChannel? runnerChannel = new MessageChannel(connection, Constants.RunnerChannel);

            Connection? runnerConnection = null;
            try
            {
                // applications will contact the orchestrator using the application channel
                // to make requests: eg get a runner, check job status, etc
                applicationChannel.OnMessageReceived += (message, channel) =>
                {
                    // this connection is to the application.
                    // close the runner channel so as not to tie up a thread
                    if (runnerChannel != null)
                    {
                        connection.CloseChannel(runnerChannel.Channel);
                        runnerChannel = null;
                    }

                    // TODO: process the message
                    // TODO: find the next best available runner and tell the app to use it
                    // TODO: this requires heuristic load balancing strategies
                    // TODO: eg whether runner is in use, how many "slots" are open, its queue size,
                    // TODO: eg OS, memory support, etc
                };

                // runners will contact the orchestrator using the runner channel
                // to update their status
                runnerChannel.OnMessageReceived += (message, channel) =>
                {
                    // this connection is to a runner.
                    // close the application channel so as not to tie up a thread
                    if (applicationChannel != null)
                    {
                        connection.CloseChannel(applicationChannel.Channel);
                        applicationChannel = null;
                    }

                    // add the runner to the pool, if necessary
                    if (runnerConnection == null)
                    {
                        runnerConnection = connection;
                        lock (RunnerPool)
                        {
                            RunnerPool.Add(runnerConnection);
                        }
                    }

                    // TODO: process the message
                };

                // the client will run forever until it disconnects or this server stops
                while (Interlocked.Read(ref IsRunning) == 1 && connection.IsConnected)
                {
                    ThreadHelpers.Yield();
                }

            }
            catch (Exception ex)
            {
                // TODO: catch exceptions and transmit back to caller
                // (either an application or a runner)
                // TODO: log it?
            }
            finally
            {
                // if the connection was for a runner, remove it from the pool
                if (runnerConnection != null)
                {
                    lock (RunnerPool)
                    {
                        RunnerPool.Remove(runnerConnection);
                    }
                }

                // find and move the client to the completed queue so it can
                // be disposed by the main thread
                if (ActiveClients.TryGetValue(id, out var client))
                {
                    CompletedClients.Enqueue(client);
                }
            }
        }

        private class ConnectedClient
        {
            public Thread Thread;
            public Connection Connection;
            public ConnectedClient(Thread thread, Connection connection)
            {
                Thread = thread;
                Connection = connection;
            }
        }
    }
}