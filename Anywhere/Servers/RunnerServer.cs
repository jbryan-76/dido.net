using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;

namespace AnywhereNET
{
    // NOTE: it's outside the scope of this library to decide how and when to spin up new Runners.

    public class RunnerServer : IDisposable
    {
        /// <summary>
        /// Indicates whether the runner is running.
        /// </summary>
        private long IsRunning = 0;

        private Thread? WorkLoopThread = null;

        private Connection? OrchestratorConnection = null;

        private MessageChannel? OrchestratorChannel = null;

        private RunnerConfiguration Configuration;

        private bool IsDisposed = false;

        private ConcurrentDictionary<Guid, Worker> ActiveWorkers = new ConcurrentDictionary<Guid, Worker>();

        private ConcurrentQueue<Worker> CompletedWorkers = new ConcurrentQueue<Worker>();

        private ConcurrentQueue<Worker> QueuedWorkers = new ConcurrentQueue<Worker>();

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
                OrchestratorConnection?.Dispose();
                IsDisposed = true;
            }
            GC.SuppressFinalize(this);
        }

        public async Task Start(X509Certificate2 cert, int port, IPAddress? ip = null)
        {
            ip ??= IPAddress.Any;

            // update the configured endpoint applications should use to connect,
            // if one was not provided
            if (Configuration.Endpoint == null)
            {
                Configuration.Endpoint = new UriBuilder("https", ip.ToString(), port).Uri;
            }

            if (Configuration.OrchestratorUri != null)
            {
                // create a secure connection to the optional orchestrator
                var uri = Configuration.OrchestratorUri;
                var client = new TcpClient(uri!.Host, uri.Port);
                OrchestratorConnection = new Connection(client, uri.Host);
                OrchestratorChannel = new MessageChannel(OrchestratorConnection, Constants.RunnerChannelNumber);

                // announce this runner to the orchestrator
                OrchestratorChannel.Send(new RunnerStartMessage(Configuration.Endpoint.ToString(),
                    Configuration.MaxTasks, Configuration.MaxQueue, Configuration.Label, Configuration.Tags));
                OrchestratorChannel.Send(new RunnerStatusMessage(RunnerStatusMessage.States.Starting, 0, 0));

                // TODO: start an infrequent (eg 60s) heartbeat to orchestrator to update status and environment stats
            }

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
            // inform the orchestrator that this runner is stopping
            OrchestratorChannel?.Send(new RunnerStatusMessage(RunnerStatusMessage.States.Stopping, 0, 0));

            // signal the work thread to stop
            Interlocked.Exchange(ref IsRunning, 0);

            // wait for it to finish
            WorkLoopThread?.Join();
            WorkLoopThread = null;
        }

        private void WorkLoop(TcpListener listener, X509Certificate2 cert)
        {
            while (Interlocked.Read(ref IsRunning) == 1)
            {
                CleanupCompletedWorkers();

                // if there are any queued workers and there is capacity to start them, go ahead
                while (QueuedWorkers.Count > 0 && ActiveWorkers.Count < Configuration.MaxTasks)
                {
                    if (QueuedWorkers.TryDequeue(out var worker))
                    {
                        ActiveWorkers.TryAdd(worker.Id, worker);
                        worker.Start();
                        SendStatusToOrchestrator();
                    }
                }

                // if there are any pending connections, accept them
                if (listener.Pending())
                {
                    // block and wait for the next incoming connection
                    var client = listener.AcceptTcpClient();

                    // create a secure connection to the endpoint
                    var connection = new Connection(client, cert);

                    // the orchestrator uses an optimistic scheduling strategy, which means
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
                        var tasksChannel = new MessageChannel(connection, Constants.TaskChannelNumber);
                        tasksChannel.Send(new RunnerBusyMessage());
                        connection.Dispose();
                    }
                    else
                    {
                        // otherwise create a worker...
                        var worker = new Worker(this, connection);

                        if (ActiveWorkers.Count < Configuration.MaxTasks
                            && QueuedWorkers.Count == 0)
                        {
                            // ...and start it immediately if there is spare capacity...
                            ActiveWorkers.TryAdd(worker.Id, worker);
                            worker.Start();
                        }
                        else
                        {
                            // ...otherwise queue it for later
                            QueuedWorkers.Enqueue(worker);
                        }

                        SendStatusToOrchestrator();
                    }
                }

                ThreadHelpers.Yield();
            }

            // the work loop is stopping:
            // clear the queue and send back cancellation messages to each requesting application,
            // then cancel and cleanup all workers

            foreach (var worker in QueuedWorkers.ToArray())
            {
                var tasksChannel = new MessageChannel(worker.Connection, Constants.TaskChannelNumber);
                tasksChannel.Send(new TaskCancelMessage());
                worker.Connection.Dispose();
            }
            QueuedWorkers.Clear();

            CancelActiveWorkers();

            CleanupCompletedWorkers();
        }

        private void CleanupCompletedWorkers()
        {
            while (CompletedWorkers.TryDequeue(out Worker? worker))
            {
                try
                {
                    worker?.Thread.Join(1000);
                    worker?.Connection.Dispose();
                    worker?.Cancel.Dispose();
                }
                catch (Exception)
                {
                    // ignore all exceptions thrown while trying to cleanup the worker
                }
            }
        }

        private void CancelActiveWorkers()
        {
            foreach (var worker in ActiveWorkers.Values.ToArray())
            {
                // try to cancel the executing task
                worker.Cancel.Cancel();
                // move the worker to completed to attempt to cleanly dispose it
                ActiveWorkers.Remove(worker.Id, out _);
                CompletedWorkers.Enqueue(worker);
            }
        }

        private void SendStatusToOrchestrator()
        {
            OrchestratorChannel?.Send(new RunnerStatusMessage(
                RunnerStatusMessage.States.Ready,
                ActiveWorkers.Count,
                QueuedWorkers.Count)
            );
        }

        /// <summary>
        /// Processes the task request from a single remote connected application.
        /// </summary>
        /// <param name="worker"></param>
        /// <exception cref="InvalidOperationException"></exception>
        private void ProcessClient(Worker worker)
        {
            // create communication channels to the application for: task communication, assemblies, files
            var tasksChannel = new MessageChannel(worker.Connection, Constants.TaskChannelNumber);
            var assembliesChannel = worker.Connection.GetChannel(Constants.AssemblyChannelNumber);
            var filesChannel = worker.Connection.GetChannel(Constants.FileChannelNumber);

            try
            {
                // create the execution context that is available to the expression while it's running
                worker.Context = new ExecutionContext
                {
                    ExecutionMode = ExecutionModes.Local,
                    FilesChannel = filesChannel,
                    Cancel = worker.Cancel.Token
                };

                // create the runtime environment
                var environment = new Environment
                {
                    AssemblyContext = new AssemblyLoadContext(Guid.NewGuid().ToString(), true),
                    ExecutionContext = worker.Context,
                    ResolveRemoteAssemblyAsync = new DefaultRemoteAssemblyResolver(assembliesChannel).ResolveAssembly,
                };

                // use a reset event to block until the task is complete
                var reset = new AutoResetEvent(false);

                // set up a handler to process task-related messages
                tasksChannel.OnMessageReceived += async (message, channel) =>
                {
                    try
                    {
                        switch (message)
                        {
                            case TaskRequestMessage request:
                                using (var stream = new MemoryStream(request.Bytes))
                                {
                                    // TODO: execute in a task.Run so it can be cancelled or timed out

                                    Func<ExecutionContext, object>? decodedExpression = null;

                                    try
                                    {
                                        // deserialize the expression
                                        decodedExpression = await ExpressionSerializer.DeserializeAsync<object>(stream, environment);
                                    }
                                    catch (Exception ex)
                                    {
                                        // catch and report deserialization errors
                                        var errorMessage = new TaskErrorMessage(ex, TaskErrorMessage.ErrorTypes.Deserialization);
                                        tasksChannel.Send(errorMessage);
                                    }

                                    try
                                    {
                                        // TODO: set up a timer to cancel and send back a timeout message

                                        // execute it
                                        var result = decodedExpression?.Invoke(environment.ExecutionContext);

                                        // if a cancellation was requested, the result can't be trusted,
                                        // so ensure a cancellation exception is thrown
                                        worker.Cancel.Token.ThrowIfCancellationRequested();

                                        // send the result back to the application on the expression channel
                                        var resultMessage = new TaskResponseMessage(result);
                                        tasksChannel.Send(resultMessage);
                                    }
                                    catch (Exception ex)
                                    {
                                        // TODO: if it's cancelled catch the cancelation exception
                                        // catch and report invokation errors
                                        var errorMessage = new TaskErrorMessage(ex, TaskErrorMessage.ErrorTypes.Invokation);
                                        tasksChannel.Send(errorMessage);
                                    }

                                    reset.Set();
                                }
                                break;

                            case TaskCancelException cancel:
                                worker.Cancel.Cancel();
                                break;

                            default:
                                throw new InvalidOperationException($"Message {message.GetType()} is unknown");
                        }
                    }
                    catch (Exception ex)
                    {
                        // all exceptions must be handled explicitly since this handler is running in another
                        // thread that is never awaited
                        var errorMessage = new TaskErrorMessage(ex, TaskErrorMessage.ErrorTypes.General);
                        tasksChannel.Send(errorMessage);
                        reset.Set();
                    }
                };

                // block until the expression completes or fails
                reset.WaitOne();
            }
            catch (Exception ex)
            {
                // send the exception back to the application on the expression channel
                var errorMessage = new TaskErrorMessage(ex, TaskErrorMessage.ErrorTypes.General);
                tasksChannel.Send(errorMessage);
                // TODO: log it?
            }
            finally
            {
                // move the worker to the completed queue so it can
                // be disposed by the main thread
                ActiveWorkers.Remove(worker.Id, out _);
                CompletedWorkers.Enqueue(worker);

                SendStatusToOrchestrator();
            }
        }

        private class Worker
        {
            public Guid Id { get; private set; } = Guid.NewGuid();
            public Thread Thread;
            public Connection Connection;
            public RunnerServer Server;
            public ExecutionContext Context;
            public CancellationTokenSource Cancel = new CancellationTokenSource();

            public Worker(RunnerServer server, Connection connection)
            {
                Server = server;
                Connection = connection;
            }

            public void Start()
            {
                Thread = new Thread(() => Server.ProcessClient(this));
                Thread.Start();
            }
        }
    }
}