using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace DidoNet
{
    /// <summary>
    /// A configurable Dido Mediator server that can be deployed as a console app, service, or integrated into a
    /// larger application.
    /// </summary>
    public class MediatorServer
    {
        /// <summary>
        /// The unique id of the mediator instance.
        /// </summary>
        public string Id { get; private set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The current configuration of the mediator.
        /// </summary>
        public MediatorConfiguration Configuration { get; private set; }

        /// <summary>
        /// The set of known runners.
        /// </summary>
        internal List<Runner> RunnerPool = new List<Runner>();

        /// <summary>
        /// Indicates whether the mediator has started and is running.
        /// </summary>
        private long IsRunning = 0;

        /// <summary>
        /// The thread that accepts and processes new connections.
        /// </summary>
        private Thread? WorkLoopThread;

        /// <summary>
        /// Indicates whether the instance is disposed.
        /// </summary>
        private bool IsDisposed = false;

        /// <summary>
        /// The set of all active connected clients;
        /// </summary>
        private ConcurrentDictionary<Guid, ConnectedClient> ActiveClients = new ConcurrentDictionary<Guid, ConnectedClient>();

        /// <summary>
        /// The set of clients that have disconnected and need to be disposed.
        /// </summary>
        private ConcurrentQueue<ConnectedClient> CompletedClients = new ConcurrentQueue<ConnectedClient>();

        /// <summary>
        /// The class logger instance.
        /// </summary>
        private ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Create a new mediator server with the given configuration.
        /// </summary>
        /// <param name="configuration"></param>
        public MediatorServer(MediatorConfiguration? configuration = null)
        {
            Configuration = configuration ?? new MediatorConfiguration();
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

        /// <summary>
        /// Starts listening for incoming connection requests from applications and runners.
        /// </summary>
        /// <param name="cert"></param>
        /// <param name="port"></param>
        /// <param name="ip"></param>
        public void Start(X509Certificate2 cert, int port, IPAddress? ip = null)
        {
            ip = ip ?? IPAddress.Any;

            Logger.Info($"Starting mediator {Id} listening at {ip}:{port}");

            // listen for incoming connections
            var listener = new TcpListener(ip, port);
            listener.Start();

            // start a work thread to accept and process new connections
            IsRunning = 1;
            WorkLoopThread = new Thread(() => WorkLoop(listener, cert));
            WorkLoopThread.Start();
        }

        /// <summary>
        /// Stops listening for incoming requests and shuts down the server.
        /// </summary>
        public void Stop()
        {
            Logger.Info($"Stopping mediator {Id}...");

            // signal the work thread to stop
            Interlocked.Exchange(ref IsRunning, 0);

            // wait for it to finish
            WorkLoopThread?.Join();
            WorkLoopThread = null;

            // cleanup all clients
            DisconnectAndCleanupActiveClients();
            CleanupCompletedClients();

            Logger.Info($"  Mediator {Id} stopped");
        }

        /// <summary>
        /// Finds and returns the next best available runner using the filtering and matching criteria
        /// of the provided request and the configuration and state of all runners in the runner pool.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        internal Runner? GetNextAvailableRunner(RunnerRequestMessage request)
        {
            // TODO: if the request identifies a specific runner (by id) for a "tetherless" re-connect,
            // TODO: find and return that runner immediately, with no filtering

            // TODO: build the query once and reuse it

            // initialize a query to the current set of ready runners
            var query = RunnerPool.Where(x => x.State == RunnerStates.Ready);

            // filter to include only matching platforms, if necessary
            if (request.Platforms?.Length > 0)
            {
                query = query.Where(x => request.Platforms.Contains(x.Platform));
            }

            // filter to include only matching runner labels, if necessary
            if (!string.IsNullOrEmpty(request.Label))
            {
                query = query.Where(x => x.Label == request.Label);
            }

            // filter to include only matching runner tags, if necessary
            if (request.Tags?.Length > 0)
            {
                query = query.Where(x => x.Tags.Intersect(request.Tags).Any());
            }

            // filter to include runners that have:
            query = query.Where(x => x.ActiveTasks < x.MaxTasks // available task slots
                || x.MaxQueue < 0 // or unlimited queue
                || x.QueueLength < x.MaxQueue // or available queue
            );

            // now sort them by "most availability":
            // first by number of open slots (ie runners with immediate vacancies)
            query = query.OrderByDescending(x => x.MaxTasks - x.ActiveTasks)
                    // then by shortest queue (ie runners with the least pending work)
                    .ThenBy(x => x.QueueLength);

            // finally materialize and return the first (ie best) matching eligible runner
            lock (RunnerPool)
            {
                return query.FirstOrDefault();
            }
        }

        /// <summary>
        /// The work loop for the main mediator thread, responsible for accepting incoming connections 
        /// from applications that are requesting remote execution of tasks and the runners that are 
        /// processing those tasks.
        /// </summary>
        /// <param name="listener"></param>
        /// <param name="cert"></param>
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

        /// <summary>
        /// Disposes all completed clients.
        /// </summary>
        private void CleanupCompletedClients()
        {
            while (CompletedClients.TryDequeue(out ConnectedClient? client))
            {
                client?.Thread.Join(1000);
                client?.Connection.Dispose();
            }
        }

        /// <summary>
        /// Disconnects and disposes all active clients.
        /// </summary>
        private void DisconnectAndCleanupActiveClients()
        {
            foreach (var client in ActiveClients.Values)
            {
                client.Connection.Disconnect();
                client.Thread.Join(100);
                client.Connection.Dispose();
            }
        }

        /// <summary>
        /// Processes the given connection from either an application or a mediator,
        /// handling all received messages and communication.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="connection"></param>
        private void ProcessClient(Guid id, Connection connection)
        {
            // create communication channels.
            // NOTE: by design, exactly one of these channels will receive data
            // depending on whether an application or a runner connected.
            // however, until one of the channels receives data, it's impossible to know which.
            var applicationChannel = new MessageChannel(connection, Constants.MediatorApp_ChannelId);
            var runnerChannel = new MessageChannel(connection, Constants.MediatorRunner_ChannelId);

            Runner? runner = null;
            try
            {
                // applications will contact the mediator using the application channel
                // to make requests: eg get a runner, check job status, etc
                applicationChannel.OnMessageReceived = (message, channel) =>
                {
                    // this connection is to an application.
                    // close the runner channel so as not to tie up a thread
                    runnerChannel?.Dispose();

                    // process the message
                    switch (message)
                    {
                        case RunnerRequestMessage request:
                            var runner = GetNextAvailableRunner(request);
                            if (runner == null)
                            {
                                channel.Send(new RunnerNotAvailableMessage());
                            }
                            else
                            {
                                channel.Send(new RunnerResponseMessage(runner.Endpoint));
                            }
                            break;
                    }
                };

                // runners will contact the mediator using the runner channel to update their status
                runnerChannel.OnMessageReceived = (message, channel) =>
                {
                    // this connection is to a runner.
                    // close the application channel so as not to tie up a thread
                    applicationChannel?.Dispose();

                    // add the runner to the pool, if necessary
                    if (runner == null)
                    {
                        runner = new Runner();
                        lock (RunnerPool)
                        {
                            RunnerPool.Add(runner);
                        }
                    }

                    // process the message
                    switch (message)
                    {
                        case RunnerStartMessage start:
                            runner.Init(start);
                            break;
                        case RunnerStatusMessage status:
                            runner.Update(status);
                            break;
                    }
                };

                // the client will run forever until it disconnects or this server stops
                while (Interlocked.Read(ref IsRunning) == 1 && connection.IsConnected)
                {
                    ThreadHelpers.Yield();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                // if the connection was for a runner, remove it from the pool
                if (runner != null)
                {
                    lock (RunnerPool)
                    {
                        RunnerPool.Remove(runner);
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

        /// <summary>
        /// Tracks a connection from an application or runner, including its local processing thread.
        /// </summary>
        private class ConnectedClient
        {
            /// <summary>
            /// The processing thread for the client.
            /// </summary>
            public Thread Thread { get; private set; }

            /// <summary>
            /// The communications connection for the client.
            /// </summary>
            public Connection Connection { get; private set; }

            /// <summary>
            /// Create a new connected client instance.
            /// </summary>
            /// <param name="thread"></param>
            /// <param name="connection"></param>
            public ConnectedClient(Thread thread, Connection connection)
            {
                Thread = thread;
                Connection = connection;
            }
        }

        /// <summary>
        /// Tracks the state and status of a connected runner.
        /// </summary>
        internal class Runner : IRunnerDetail, IRunnerStatus
        {
            /// <summary>
            /// The OS platform the runner is on.
            /// </summary>
            public OSPlatforms Platform { get; set; } = OSPlatforms.Unknown;

            /// <summary>
            /// The OS version for the platform the runner is on.
            /// </summary>
            public string OSVersion { get; set; } = string.Empty;

            /// <summary>
            /// The uri for applications to use to connect to the runner.
            /// </summary>
            public string Endpoint { get; set; } = string.Empty;

            /// <summary>
            /// The maximum number of tasks the runner can execute concurrently.
            /// <para/>Legal values are:
            /// <para/>Less than or equal to zero (default) = Auto (will be set to the available 
            /// number of cpu cores present on the system).
            /// <para/>Anything else indicates the maximum number of tasks.
            /// </summary>
            public int MaxTasks { get; set; } = 0;

            /// <summary>
            /// The maximum number of pending tasks the runner can accept and queue before rejecting.
            /// <para/>Legal values are:
            /// <para/>Less than zero = Unlimited (up to the number of simultaneous connections allowed by the OS).
            /// <para/>Zero (default) = Tasks cannot be queued. New tasks are accepted only if fewer than
            /// the maximum number of concurrent tasks are currently running.
            /// <para/>Anything else indicates the maximum number of tasks to queue.
            /// </summary>
            public int MaxQueue { get; set; } = 0;

            /// <summary>
            /// The optional runner label.
            /// </summary>
            public string Label { get; set; } = string.Empty;

            /// <summary>
            /// The optional runner tags.
            /// </summary>
            public string[] Tags { get; set; } = new string[0];

            /// <summary>
            /// The last known state of the runner.
            /// </summary>
            public RunnerStates State { get; set; } = RunnerStates.Starting;

            /// <summary>
            /// The number of tasks the runner is currently executing.
            /// By definition this is less than or equal to MaxTasks.
            /// </summary>
            public int ActiveTasks { get; set; } = 0;

            /// <summary>
            /// The number of pending tasks the runner has in its queue.
            /// By definition this is less than or equal to MaxQueue (unless MaxQueue is less than zero).
            /// </summary>
            public int QueueLength { get; set; } = 0;

            /// <summary>
            /// Initialize the runner metadata from the provided message.
            /// </summary>
            /// <param name="message"></param>
            public void Init(RunnerStartMessage message)
            {
                Platform = message.Platform;
                OSVersion = message.OSVersion;
                Endpoint = message.Endpoint;
                MaxTasks = message.MaxTasks;
                MaxQueue = message.MaxQueue;
                Label = message.Label;
                Tags = message.Tags;
                State = RunnerStates.Starting;
            }

            /// <summary>
            /// Update the runner status from the provided message.
            /// </summary>
            /// <param name="message"></param>
            public void Update(RunnerStatusMessage message)
            {
                State = message.State;
                ActiveTasks = message.ActiveTasks;
                QueueLength = message.QueueLength;
            }
        }
    }
}