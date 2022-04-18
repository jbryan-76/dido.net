using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;

namespace DidoNet
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

            // debounce the server by giving it a beat or two to startup
            Thread.Sleep(10);
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
                // TODO: check for active workers that have been cancelled or timed out but haven't stopped yet?

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
                    var connection = new Connection(client, cert, "runner");

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
                worker.Dispose();
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
                    ThreadHelpers.Debug($"RUNNER: disposing completed worker");
                    worker?.Dispose();
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
            var filesChannel = worker.Connection.GetChannel(Constants.FileChannelNumber);

            tasksChannel.Channel.Name = "RUNNER";
            filesChannel.Name = "RUNNER";

            try
            {
                // create the execution context that is available to the expression while it's running
                worker.Context = new ExecutionContext
                {
                    ExecutionMode = ExecutionModes.Local,
                    FilesChannel = filesChannel,
                    Cancel = worker.Cancel.Token
                };

                // use a reset event to block until the task is complete
                var reset = new AutoResetEvent(false);

                Thread? taskExecutionThread = null;

                // set up a handler to process task-related messages
                tasksChannel.OnMessageReceived = (message, channel) =>
                {
                    try
                    {
                        ThreadHelpers.Debug($"RUNNER: processing message {message.GetType().FullName}");

                        switch (message)
                        {
                            // process the message requesting to execute a task
                            case TaskRequestMessage request:
                                taskExecutionThread = new Thread(() => ExecuteTask(worker, request, reset, tasksChannel));
                                taskExecutionThread.Start();
                                break;

                            case TaskCancelMessage cancel:
                                ThreadHelpers.Debug($"RUNNER cancelling the worker");
                                worker.Cancel.Cancel();
                                break;

                            default:
                                throw new InvalidOperationException($"Message {message.GetType()} is unknown");
                        }
                    }
                    catch (Exception ex)
                    {
                        // handle all unexpected exceptions explicitly by notifying the application
                        var errorMessage = new TaskErrorMessage(ex, TaskErrorMessage.ErrorTypes.General);
                        ThreadHelpers.Debug($"RUNNER got error: {ex.ToString()}");
                        tasksChannel.Send(errorMessage);
                        worker.Cancel.Cancel();
                        reset.Set();
                    }
                };

                // block until the expression completes or fails
                ThreadHelpers.Debug($"RUNNER: waiting for the task to finish");
                reset.WaitOne();
                ThreadHelpers.Debug($"RUNNER: task finished, joining");
                taskExecutionThread?.Join();
                ThreadHelpers.Debug($"RUNNER: task complete");
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

        private async void ExecuteTask(Worker worker, TaskRequestMessage request, AutoResetEvent reset, MessageChannel tasksChannel)
        {
            var assembliesChannel = worker.Connection.GetChannel(Constants.AssemblyChannelNumber);

            assembliesChannel.Name = "RUNNER";

            // create the runtime environment
            var environment = new Environment
            {
                AssemblyContext = new AssemblyLoadContext(Guid.NewGuid().ToString(), true),
                ExecutionContext = worker.Context,
                ResolveRemoteAssemblyAsync = new DefaultRemoteAssemblyResolver(assembliesChannel).ResolveAssembly,
            };

            using (var stream = new MemoryStream(request.Bytes))
            {
                Func<ExecutionContext, object>? expression = null;

                try
                {
                    // deserialize the expression
                    expression = await ExpressionSerializer.DeserializeAsync<object>(stream, environment);
                }
                catch (Exception ex)
                {
                    // catch and report deserialization errors
                    var errorMessage = new TaskErrorMessage(ex, TaskErrorMessage.ErrorTypes.Deserialization);
                    tasksChannel.Send(errorMessage);
                    // indicate to the main thread that the task is done
                    reset.Set();
                    return;
                }

                Timer? timeout = null;
                try
                {
                    // set up a timer if necessary to cancel the task if it times out
                    long didTimeout = 0;
                    if (request.TimeoutInMs > 0)
                    {
                        ThreadHelpers.Debug($"RUNNER: timeout in {request.TimeoutInMs}ms");
                        timeout = new Timer((arg) =>
                        {
                            // try to cancel the task
                            worker.Cancel.Cancel();

                            // indicate that a timeout occurred
                            Interlocked.Exchange(ref didTimeout, 1);

                            // let the application know immediately the task did not complete due to a timeout
                            // (this way if the task does not cancel soon at least the application
                            // can start a retry)
                            ThreadHelpers.Debug($"RUNNER: sending timeout message");
                            tasksChannel.Send(new TaskTimeoutMessage());
                            ThreadHelpers.Debug($"RUNNER: timeout message sent");

                            // indicate the timeout message was sent
                            Interlocked.Exchange(ref didTimeout, 2);
                        }, null, request.TimeoutInMs, Timeout.Infinite);
                    }

                    // now execute the task by invoking the expression.
                    // the task will run for as long as necessary.
                    ThreadHelpers.Debug($"RUNNER: starting task");
                    var result = expression?.Invoke(environment.ExecutionContext);
                    ThreadHelpers.Debug($"RUNNER: finished task");

                    // dispose the timeout now (if it exists) to prevent it from triggering
                    // accidentally if the task already completed successfully
                    timeout?.Dispose();
                    timeout = null;

                    // if the task did not timeout, continue processing
                    // (otherwise a timeout message was already sent)
                    if (Interlocked.Read(ref didTimeout) == 0)
                    {
                        // if a cancellation was requested, the result can't be trusted,
                        // so ensure a cancellation exception is thrown
                        // (it will be handled in the catch block below)
                        worker.Cancel.Token.ThrowIfCancellationRequested();

                        // otherwise send the result back to the application
                        var resultMessage = new TaskResponseMessage(result);
                        ThreadHelpers.Debug($"RUNNER: sending result message");
                        tasksChannel.Send(resultMessage);
                        ThreadHelpers.Debug($"RUNNER: result message sent");
                    }
                    else
                    {
                        // otherwise handle a rare edge-case where the task completes before
                        // the timeout message finishes sending (since that message is sent in
                        // a pool thread managed by the Timer), in which case delay here
                        // until the message is sent (to prevent the underlying stream from
                        // closing while the message is still writing to it).
                        while (Interlocked.Read(ref didTimeout) == 1)
                        {
                            ThreadHelpers.Yield();
                        }
                    }
                }
                catch (OperationCanceledException ex)
                {
                    ThreadHelpers.Debug($"RUNNER: sending cancelled message");
                    tasksChannel.Send(new TaskCancelMessage());
                    ThreadHelpers.Debug($"RUNNER: cancelled message sent");
                }
                catch (Exception ex)
                {
                    // catch and report invokation errors
                    var errorMessage = new TaskErrorMessage(ex, TaskErrorMessage.ErrorTypes.Invokation);
                    tasksChannel.Send(errorMessage);
                }
                finally
                {
                    timeout?.Dispose();
                }

                // indicate to the main worker thread that the task is done
                reset.Set();
            }
        }

        private class Worker : IDisposable
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

            public void Dispose()
            {
                Thread.Join(1000);
                Connection.Dispose();
                Cancel.Dispose();
            }
        }
    }
}