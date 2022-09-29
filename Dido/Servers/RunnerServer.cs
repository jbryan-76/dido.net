using Dido.Utilities;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace DidoNet
{
    // NOTE: it's outside the scope of this library to decide how and when to spin up new Runners.

    /// <summary>
    /// A configurable Dido Runner server that can be deployed as a console app, service, or integrated into a
    /// larger application.
    /// </summary>
    public class RunnerServer : IDisposable
    {
        /// <summary>
        /// The current configuration of the runner.
        /// </summary>
        public RunnerConfiguration Configuration { get; private set; }

        /// <summary>
        /// The runner server instance's specific cache path. Each server instance needs a unique cache path
        /// to prevent file collisions.
        /// </summary>
        private string RunnerSpecificCachePath
        {
            get { return Path.Combine(Configuration.CachePath, Configuration.Id); }
        }

        /// <summary>
        /// Indicates whether the runner has started and is running.
        /// Note an integer is used because booleans are not supported by Interlocked.Exchange.
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
        private ConcurrentDictionary<string, TaskWorker> ActiveWorkers = new ConcurrentDictionary<string, TaskWorker>();

        /// <summary>
        /// The set of workers that have completed processing tasks and need to be disposed.
        /// </summary>
        private ConcurrentQueue<TaskWorker> CompletedWorkers = new ConcurrentQueue<TaskWorker>();

        /// <summary>
        /// The set of queued workers that have not yet started processing tasks.
        /// </summary>
        private ConcurrentQueue<TaskWorker> QueuedWorkers = new ConcurrentQueue<TaskWorker>();

        /// <summary>
        /// A timer to periodically clean the cache.
        /// </summary>
        private Timer? CacheCleanupTimer;

        /// <summary>
        /// The class logger instance.
        /// </summary>
        private readonly ILogger Logger = LogManager.GetCurrentClassLogger();

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

            InitCache();
        }

        public void Dispose()
        {
            Stop();
            if (!IsDisposed)
            {
                MediatorConnection?.Dispose();
                IsDisposed = true;
            }
            CacheCleanupTimer?.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Starts listening for incoming connection requests from remote applications.
        /// </summary>
        /// <param name="cert"></param>
        /// <param name="port"></param>
        /// <param name="ip"></param>
        /// <returns></returns>
        public void Start(X509Certificate2 cert, int? port = null, IPAddress? ip = null)
        {
            ip ??= IPAddress.Any;
            port ??= Constants.DefaultPort;

            Logger.Info($"Starting runner {Configuration.Id} listening at {ip}:{port}");

            // update the configured endpoint applications should use to connect,
            // if one was not provided
            if (Configuration.Endpoint == null)
            {
                var host = ip.ToString();
                if (host == "0.0.0.0")
                {
                    host = "localhost";
                }
                Configuration.Endpoint = new UriBuilder("https", host, port.Value).Uri.ToString();
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
                MediatorChannel.Send(new RunnerStartMessage(Configuration.Id, Configuration.Endpoint.ToString(),
                    Configuration.MaxTasks, Configuration.MaxQueue, Configuration.Label, Configuration.Tags));
                MediatorChannel.Send(new RunnerStatusMessage(RunnerStates.Starting, 0, 0));

                // TODO: receive the response. if error, kill the runner, else proceed

                // TODO: start an infrequent (eg 60s) heartbeat to mediator to update status and environment stats (eg cpu, ram)?

                // start listening for messages from the mediator
                MediatorChannel.OnMessageReceived = async (message, channel) => await MediatorChannelMessageHandler(message, channel);

                Logger.Info($"Runner {Configuration.Id} using mediator = {Configuration.MediatorUri}");
            }

            // listen for incoming connections
            var listener = new TcpListener(ip, port.Value);
            listener.Start();

            // start a work thread to accept and process new connections
            IsRunning = 1;
            WorkLoopThread = new Thread(() => WorkLoop(listener, cert));
            WorkLoopThread.Start();

            // debounce the server by giving it a beat or two to startup
            // TODO: test and remove this: is it necessary?
            Thread.Sleep(10);
        }

        /// <summary>
        /// Processes all messages from the mediator.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        private async Task MediatorChannelMessageHandler(IMessage message, MessageChannel channel)
        {
            // process the message
            switch (message)
            {
                case JobCancelMessage cancel:
                    // try to find the worker...
                    var worker = ActiveWorkers.Values.FirstOrDefault(w => w.Request?.JobId == cancel.JobId);
                    if (worker != null)
                    {
                        // ...then cancel it
                        worker.Cancel();
                        // move the worker to the completed queue to attempt to cleanly dispose it
                        ActiveWorkers.Remove(worker.Id, out _);
                        CompletedWorkers.Enqueue(worker);
                    }
                    break;
            }
        }

        /// <summary>
        /// Stops listening for incoming requests and shuts down the server.
        /// </summary>
        public void Stop()
        {
            Logger.Info($"Stopping runner {Configuration.Id}...");

            // signal the work thread to stop
            Interlocked.Exchange(ref IsRunning, 0);

            // wait for it to finish
            WorkLoopThread?.Join();
            WorkLoopThread = null;

            // inform the mediator that this runner is stopping
            // TODO: what to do if mediator is not available? retry? how long?
            MediatorChannel?.Send(new RunnerStatusMessage(RunnerStates.Stopping, 0, 0));

            if (Configuration.DeleteCacheAtShutdown)
            {
                Logger.Info($"Deleting runner cache {Configuration.Id}...");
                DeleteCache();
            }

            Logger.Info($"  Runner {Configuration.Id} stopped");
        }

        /// <summary>
        /// Delete the runner's cache.
        /// </summary>
        public void DeleteCache()
        {
            Directory.Delete(RunnerSpecificCachePath, true);
        }

        /// <summary>
        /// Delete all expired cached files.
        /// </summary>
        internal void CleanCache()
        {
            if (Configuration.CacheMaxAge <= TimeSpan.Zero)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var file in Directory.EnumerateFiles(RunnerSpecificCachePath, "*", SearchOption.AllDirectories))
            {
                var lastWrite = File.GetLastWriteTimeUtc(file);
                if (now - lastWrite > Configuration.CacheMaxAge)
                {
                    File.Delete(file);
                }
            }
        }

        /// <summary>
        /// Initialize and cleanup the local cache folder for the runner.
        /// </summary>
        private void InitCache()
        {
            if (string.IsNullOrEmpty(Configuration.CachePath))
            {
                return;
            }

            // ensure the specific cache folders exist and are accessible
            var assemblyDirInfo = Directory.CreateDirectory(Path.Combine(RunnerSpecificCachePath, "assemblies"));
            var fileDirInfo = Directory.CreateDirectory(Path.Combine(RunnerSpecificCachePath, "files"));

            // update the configuration to use absolute paths
            Configuration.AssemblyCachePath = assemblyDirInfo.FullName;
            Configuration.FileCachePath = fileDirInfo.FullName;

            // start a timer to periodically clean the cache, constrained to a reasonable timespan
            int cleanupPeriodInSeconds = Math.Max(
                60 * 60, // no more frequent than once an hour
                Math.Min(24 * 60 * 60, // at least once a day
                (int)Configuration.CacheMaxAge.TotalSeconds));
            CacheCleanupTimer = new Timer((arg) => CleanCache(), null, 0, cleanupPeriodInSeconds * 1000);
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
                // TODO: check for active workers that have been canceled or timed out but haven't stopped yet?

                CleanupCompletedWorkers();

                // if there are any queued workers and there is capacity to start them, go ahead
                while (QueuedWorkers.Count > 0 && ActiveWorkers.Count < Configuration.MaxTasks)
                {
                    if (QueuedWorkers.TryDequeue(out var worker))
                    {
                        StartWorker(worker);
                        //ActiveWorkers.TryAdd(worker.Id, worker);
                        //worker.Start(WorkerComplete);
                        //SendStatusToMediator();
                    }
                }

                // sleep unless a new connection is pending
                if (!listener.Pending())
                {
                    ThreadHelpers.Yield();
                    continue;
                }

                // block and wait for the next incoming connection
                var client = listener.AcceptTcpClient();

                // create a secure connection to the endpoint
                var connection = new Connection(client, cert);

                // receive the initial task detail message
                TaskTypeMessage taskTypeMessage = new TaskTypeMessage();
                using (var controlChannel = new MessageChannel(connection, Constants.AppRunner_ControlChannelId))
                {
                    taskTypeMessage = controlChannel.ReceiveMessage<TaskTypeMessage>();
                }

                // TODO: if the connection is reconnecting to an existing runner, find and update the worker
                if (taskTypeMessage.TaskType == TaskTypeMessage.TaskTypes.Untethered
                    && !string.IsNullOrEmpty(taskTypeMessage.TaskId))
                {
                    if (ActiveWorkers.TryGetValue(taskTypeMessage.TaskId, out var worker))
                    {
                        // TODO: update the worker connection
                        // TODO: send something back to the application
                    }
                    else
                    {
                        // TODO: if an active worker could not be found, that means it's done (or failed).
                        // TODO: send something back to the application
                    }
                }

                // the mediator uses an optimistic scheduling strategy, which means
                // it will route traffic to runners based on the conditions known at the 
                // time of the request, which may differ from the runner conditions by
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
                    var worker = new TaskWorker(connection, Configuration, taskTypeMessage);

                    if (ActiveWorkers.Count < Configuration.MaxTasks
                        && QueuedWorkers.Count == 0)
                    {
                        // ...and start it immediately if there is spare capacity...
                        //ActiveWorkers.TryAdd(worker.Id, worker);
                        //worker.Start(WorkerComplete);
                        StartWorker(worker);
                    }
                    else
                    {
                        // ...otherwise queue it for later
                        //QueuedWorkers.Enqueue(worker);
                        //SendStatusToMediator();
                        QueueWorker(worker);
                    }
                }
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

            SendStatusToMediator();
        }

        private void StartWorker(TaskWorker worker)
        {
            ActiveWorkers.TryAdd(worker.Id, worker);
            worker.Start(WorkerStarted, WorkerComplete);
            SendStatusToMediator();
        }

        private void QueueWorker(TaskWorker worker)
        {
            QueuedWorkers.Enqueue(worker);
            SendStatusToMediator();
        }

        /// <summary>
        /// A handler invoked when a worker starts.
        /// </summary>
        /// <param name="worker"></param>
        private void WorkerStarted(TaskWorker worker)
        {
            if (!string.IsNullOrEmpty(worker.Request?.JobId))
            {
                // if the task is in "job" mode, inform the mediator that it started
                MediatorChannel?.Send(new JobStartMessage(Configuration.Id, worker.Request.JobId));
                // TODO: receive confirmation
            }
        }

        /// <summary>
        /// A handler invoked when a worker completes.
        /// </summary>
        /// <param name="worker"></param>
        private void WorkerComplete(TaskWorker worker)
        {
            if (!string.IsNullOrEmpty(worker.Request?.JobId))
            {
                // if the task is in "job" mode, send the result to the mediator only if the runner
                // is still running (if it's not running, the worker probably completed prematurely
                // when the work thread was stopped and all tasks cancelled)
                if (Interlocked.Read(ref IsRunning) == 1)
                {
                    MediatorChannel?.Send(new JobCompleteMessage(Configuration.Id, worker.Request.JobId, worker.Result!));
                }
            }

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
            if (MediatorChannel != null)
            {
                // use a critical section to ensure sequential message sending
                // since this method is called from WorkLoop() and WorkerComplete()
                // and the run in different threads
                lock (MediatorChannel)
                {
                    MediatorChannel?.Send(new RunnerStatusMessage(
                        RunnerStates.Ready,
                        ActiveWorkers.Count,
                        QueuedWorkers.Count)
                    );
                }
            }
        }
    }
}