using NLog;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace DidoNet
{
    // NOTE: it's outside the scope of this library to decide how and when to spin up new Runners.

    // TODO: add logging

    public class RunnerServer : IDisposable
    {
        /// <summary>
        /// The unique id of the server instance.
        /// </summary>
        public string Id { get; private set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The current configuration of the runner.
        /// </summary>
        public RunnerConfiguration Configuration { get; private set; }

        /// <summary>
        /// Indicates whether the runner has started and is running.
        /// </summary>
        private long IsRunning = 0;

        /// <summary>
        /// The thread that accepts and processes new connections.
        /// </summary>
        private Thread? WorkLoopThread = null;

        /// <summary>
        /// The connection to the optional mediator.
        /// </summary>
        private Connection? MediatorConnection = null;

        /// <summary>
        /// The message channel to communicate with the mediator.
        /// </summary>
        private MessageChannel? MediatorChannel = null;

        /// <summary>
        /// Indicates whether the instance is disposed.
        /// </summary>
        private bool IsDisposed = false;

        /// <summary>
        /// The set of all active workers that are processing tasks.
        /// </summary>
        private ConcurrentDictionary<Guid, TaskWorker> ActiveWorkers = new ConcurrentDictionary<Guid, TaskWorker>();

        /// <summary>
        /// The set of workers that have completed processing tasks and need to be disposed.
        /// </summary>
        private ConcurrentQueue<TaskWorker> CompletedWorkers = new ConcurrentQueue<TaskWorker>();

        /// <summary>
        /// The set of queued workers that have not yet started processing tasks.
        /// </summary>
        private ConcurrentQueue<TaskWorker> QueuedWorkers = new ConcurrentQueue<TaskWorker>();

        /// <summary>
        /// The class logger instance.
        /// </summary>
        private ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Create a new runner server with the given configuration.
        /// </summary>
        /// <param name="configuration"></param>
        public RunnerServer(RunnerConfiguration? configuration = null)
        {
            Configuration = configuration ?? new RunnerConfiguration();
            if (Configuration.MaxTasks <= 0)
            {
                Configuration.MaxTasks = Math.Max(System.Environment.ProcessorCount, 1);
            }
        }

        public void Dispose()
        {
            Stop();
            if (!IsDisposed)
            {
                MediatorConnection?.Dispose();
                IsDisposed = true;
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Starts listening for incoming connection requests from applications.
        /// </summary>
        /// <param name="cert"></param>
        /// <param name="port"></param>
        /// <param name="ip"></param>
        /// <returns></returns>
        public void Start(X509Certificate2 cert, int port, IPAddress? ip = null)
        {
            ip ??= IPAddress.Any;

            Logger.Info($"Starting runner {Id} listening at {ip}:{port}");

            // update the configured endpoint applications should use to connect,
            // if one was not provided
            if (Configuration.Endpoint == null)
            {
                Configuration.Endpoint = new UriBuilder("https", ip.ToString(), port).Uri.ToString();
            }

            Logger.Info($"  Endpoint = {Configuration.Endpoint}");

            if (Configuration.MediatorUri != null)
            {
                var connectionSettings = new ClientConnectionSettings
                {
                    ValidaionPolicy = Configuration.ServerValidationPolicy,
                    Thumbprint = Configuration.ServerCertificateThumbprint
                };

                // create a secure connection to the optional mediator
                var uri = new Uri(Configuration.MediatorUri);
                MediatorConnection = new Connection(uri!.Host, uri.Port, null, connectionSettings);
                MediatorChannel = new MessageChannel(MediatorConnection, Constants.MediatorRunner_ChannelId);

                // announce this runner to the mediator
                MediatorChannel.Send(new RunnerStartMessage(Configuration.Endpoint.ToString(),
                    Configuration.MaxTasks, Configuration.MaxQueue, Configuration.Label, Configuration.Tags));
                MediatorChannel.Send(new RunnerStatusMessage(RunnerStates.Starting, 0, 0));

                // TODO: start an infrequent (eg 60s) heartbeat to mediator to update status and environment stats (eg cpu, ram)?
                Logger.Info($"Runner {Id} using mediator = {Configuration.MediatorUri}");
            }

            // listen for incoming connections
            var listener = new TcpListener(ip, port);
            listener.Start();

            // start a work thread to accept and process new connections
            IsRunning = 1;
            WorkLoopThread = new Thread(() => WorkLoop(listener, cert));
            WorkLoopThread.Start();

            // debounce the server by giving it a beat or two to startup
            Thread.Sleep(10);
        }

        /// <summary>
        /// Stops listening for incoming requests and shuts down the server.
        /// </summary>
        public void Stop()
        {
            Logger.Info($"Stopping runner {Id}...");

            // inform the mediator that this runner is stopping
            MediatorChannel?.Send(new RunnerStatusMessage(RunnerStates.Stopping, 0, 0));

            // signal the work thread to stop
            Interlocked.Exchange(ref IsRunning, 0);

            // wait for it to finish
            WorkLoopThread?.Join();
            WorkLoopThread = null;

            Logger.Info($"  Runner {Id} stopped");
        }

        /// <summary>
        /// The work loop for the main runner thread, responsible for accepting incoming connections 
        /// from applications that are requesting remote execution of tasks.
        /// </summary>
        /// <param name="listener"></param>
        /// <param name="cert"></param>
        private void WorkLoop(TcpListener listener, X509Certificate2 cert)
        {
            // indicate this runner is ready to receive task requests
            SendStatusToMediator();

            while (Interlocked.Read(ref IsRunning) == 1)
            {
                // TODO: check for active workers that have been cancelled or timed out but haven't stopped yet?

                CleanupCompletedWorkers();

                // if there are any queued workers and there is capacity to start them, go ahead
                while (QueuedWorkers.Count > 0 && ActiveWorkers.Count < Configuration.MaxTasks)
                {
                    if (QueuedWorkers.TryDequeue(out var worker))
                    {
                        ActiveWorkers.TryAdd(worker.Id, worker);
                        worker.Start(WorkerComplete);
                        SendStatusToMediator();
                    }
                }

                // if there are any pending connections, accept them
                if (listener.Pending())
                {
                    // block and wait for the next incoming connection
                    var client = listener.AcceptTcpClient();

                    // create a secure connection to the endpoint
                    var connection = new Connection(client, cert);

                    // the mediator uses an optimistic scheduling strategy, which means
                    // it will route traffic to runners based on the conditions known at the 
                    // time of the decision, which may differ from the runner conditions by
                    // the time the application connects. 
                    // if this runner is "too busy" because it already has the maximum
                    // number of tasks running and its queue is full, send a "too busy" message
                    // back to the application and disconnect.
                    // this will (probably) cause the application to try again, up to
                    // its configured level of patience.
                    if (ActiveWorkers.Count >= Configuration.MaxTasks
                        && Configuration.MaxQueue >= 0
                        && QueuedWorkers.Count >= Configuration.MaxQueue)
                    {
                        var tasksChannel = new MessageChannel(connection, Constants.AppRunner_TaskChannelId);
                        tasksChannel.Send(new RunnerBusyMessage());
                        connection.Dispose();
                    }
                    else
                    {
                        // otherwise create a worker...
                        var worker = new TaskWorker(connection);

                        if (ActiveWorkers.Count < Configuration.MaxTasks
                            && QueuedWorkers.Count == 0)
                        {
                            // ...and start it immediately if there is spare capacity...
                            ActiveWorkers.TryAdd(worker.Id, worker);
                            worker.Start(WorkerComplete);
                        }
                        else
                        {
                            // ...otherwise queue it for later
                            QueuedWorkers.Enqueue(worker);
                        }

                        SendStatusToMediator();
                    }
                }

                ThreadHelpers.Yield();
            }

            // the work loop is stopping:
            // clear the queue and send back cancellation messages to each requesting application,
            // then cancel and cleanup all workers
            foreach (var worker in QueuedWorkers.ToArray())
            {
                var tasksChannel = new MessageChannel(worker.Connection, Constants.AppRunner_TaskChannelId);
                tasksChannel.Send(new TaskCancelMessage());
                worker.Dispose();
            }
            QueuedWorkers.Clear();

            CancelActiveWorkers();

            CleanupCompletedWorkers();
        }

        /// <summary>
        /// A handler invoked when a worker completes.
        /// </summary>
        /// <param name="worker"></param>
        private void WorkerComplete(TaskWorker worker)
        {
            // move the worker to the completed queue so it can be disposed by the main thread
            ActiveWorkers.Remove(worker.Id, out _);
            CompletedWorkers.Enqueue(worker);
            SendStatusToMediator();
        }

        /// <summary>
        /// Disposes all completed workers.
        /// </summary>
        private void CleanupCompletedWorkers()
        {
            while (CompletedWorkers.TryDequeue(out TaskWorker? worker))
            {
                try
                {
                    ThreadHelpers.Debug($"RUNNER: disposing completed worker");
                    worker?.Dispose();
                }
                catch (Exception)
                {
                    // ignore all exceptions thrown while trying to cleanup the worker:
                    // they are being disposed anyway
                }
            }
        }

        /// <summary>
        /// Cancels all active workers and moves them to the completed queue for disposal.
        /// </summary>
        private void CancelActiveWorkers()
        {
            foreach (var worker in ActiveWorkers.Values.ToArray())
            {
                // try to cancel the executing task
                worker.Cancel();
                // move the worker to the completed queue to attempt to cleanly dispose it
                ActiveWorkers.Remove(worker.Id, out _);
                CompletedWorkers.Enqueue(worker);
            }
        }

        /// <summary>
        /// Sends a status message to the mediator so it can track this runner for task scheduling.
        /// </summary>
        private void SendStatusToMediator()
        {
            MediatorChannel?.Send(new RunnerStatusMessage(
                RunnerStates.Ready,
                ActiveWorkers.Count,
                QueuedWorkers.Count)
            );
        }
    }
}