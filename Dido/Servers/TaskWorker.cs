using Dido.Utilities;
using NLog;
using System;
using System.IO;
using System.Threading;

namespace DidoNet
{
    /// <summary>
    /// Manages execution of a single task requested from a remote application.
    /// </summary>
    internal class TaskWorker : IDisposable
    {
        /// <summary>
        /// The unique id of the worker.
        /// </summary>
        public Guid Id { get; private set; } = Guid.NewGuid();

        /// <summary>
        /// The connection to the remote application.
        /// </summary>
        public Connection Connection { get; private set; }

        /// <summary>
        /// The configuration of the runner that is executing this task.
        /// </summary>
        private RunnerConfiguration Configuration { get; set; }

        /// <summary>
        /// The main thread for the worker.
        /// </summary>
        private Thread? WorkThread { get; set; }

        /// <summary>
        /// A cancellation token source to cancel an executing task.
        /// </summary>
        private CancellationTokenSource CancelSource { get; set; } = new CancellationTokenSource();

        /// <summary>
        /// A delegate invoked when the task is complete. 
        /// </summary>
        private Action<TaskWorker>? OnComplete { get; set; }

        /// <summary>
        /// The communications channel for task-related messages between the runner and host application.
        /// </summary>
        private MessageChannel TasksChannel { get; set; }

        /// <summary>
        /// The communications channel for assembly-related messages between the runner and host application.
        /// </summary>
        private MessageChannel AssembliesChannel { get; set; }

        /// <summary>
        /// Used to indicate when the task thread is complete.
        /// </summary>
        private AutoResetEvent TaskComplete { get; set; } = new AutoResetEvent(false);

        /// <summary>
        /// The class logger instance.
        /// </summary>
        private ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Timestamp for when a task starts.
        /// </summary>
        private DateTime TaskStarted;

        /// <summary>
        /// Timestamp for when a task stops.
        /// </summary>
        private DateTime TaskStopped;

        /// <summary>
        /// Create a new worker to process application task requests on the provided connection.
        /// </summary>
        /// <param name="connection"></param>
        public TaskWorker(Connection connection, RunnerConfiguration configuration)
        {
            Connection = connection;
            Configuration = configuration;

            // create communication channels to the application for: task communication, assemblies, files
            TasksChannel = new MessageChannel(Connection, Constants.AppRunner_TaskChannelId);
            AssembliesChannel = new MessageChannel(Connection, Constants.AppRunner_AssemblyChannelId);

            // TODO: add "tethering" configuration: what to do if the application connection breaks?
            // TODO: in "tethered" mode, the task should cancel.
            // TODO: in "untethered" mode, the task should continue. what if the task is long running? how to reconnect?

            // TODO: add option to "store" result in Mediator instead/in-addition to sending to application (to support "job mode")
        }

        /// <summary>
        /// Starts executing the task on a separate thread, invoking the provided handler when complete.
        /// </summary>
        /// <param name="onComplete"></param>
        public void Start(Action<TaskWorker> onComplete)
        {
            OnComplete = onComplete;
            WorkThread = new Thread(() => DoWork());
            WorkThread.Start();
        }

        /// <summary>
        /// Cancels the task.
        /// </summary>
        public void Cancel()
        {
            CancelSource.Cancel();
        }

        public void Dispose()
        {
            WorkThread?.Join(1000);
            Connection.Dispose();
            CancelSource.Dispose();
        }

        /// <summary>
        /// Set up, start, and manage the lifecycle of a task request from a single remote connected application.
        /// </summary>
        private void DoWork()
        {
            try
            {
                TaskStarted = DateTime.UtcNow;
                Logger.Info($"Worker task {Id} starting");

                // keep a reference to the task execution thread to cleanup later
                Thread? taskExecutionThread = null;

                // set up a handler to process task-related messages.
                // this handler runs in a separate thread
                TasksChannel.OnMessageReceived = (message, channel) =>
                {
                    try
                    {
                        ThreadHelpers.Debug($"RUNNER: processing message {message.GetType().FullName}");

                        switch (message)
                        {
                            // execute a task by running it in a separate thread
                            case TaskRequestMessage request:
                                taskExecutionThread = new Thread(() => ExecuteTask(request));
                                taskExecutionThread.Start();
                                break;

                            // cancel the running task
                            case TaskCancelMessage cancel:
                                ThreadHelpers.Debug($"RUNNER canceling the worker");
                                CancelSource.Cancel();
                                break;

                            default:
                                throw new InvalidOperationException($"Unknown message type '{message.GetType()}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        // handle all unexpected exceptions explicitly by notifying the application that an error occurred
                        var errorMessage = new TaskErrorMessage(TaskErrorMessage.Categories.General, ex);
                        ThreadHelpers.Debug($"RUNNER got error: {ex.ToString()}");
                        TasksChannel.Send(errorMessage);
                        CancelSource.Cancel();
                        TaskComplete.Set();
                    }
                };

                // block until the task completes or fails
                ThreadHelpers.Debug($"RUNNER: waiting for the task to finish");
                TaskComplete.WaitOne();
                ThreadHelpers.Debug($"RUNNER: task finished, joining");
                taskExecutionThread?.Join();
                ThreadHelpers.Debug($"RUNNER: task complete");
            }
            catch (Exception ex)
            {
                // handle all unexpected exceptions explicitly by notifying the application that an error occurred
                var errorMessage = new TaskErrorMessage(TaskErrorMessage.Categories.General, ex);
                TasksChannel.Send(errorMessage);
                // TODO: log it?
            }
            finally
            {
                OnComplete?.Invoke(this);
                TaskStopped = DateTime.UtcNow;
                Logger.Info($"Worker task {Id} finished. Elapsed = {(TaskStopped - TaskStarted)}");
            }
        }

        /// <summary>
        /// Executes the given task.
        /// </summary>
        /// <param name="request"></param>
        private async void ExecuteTask(TaskRequestMessage request)
        {
            // create the execution context that is available to the expression while it's running
            var context = new ExecutionContext
            {
                Connection = Connection,
                ExecutionMode = ExecutionModes.Remote,
                File = new IO.RunnerFileProxy(Connection, Configuration, request.ApplicationId),
                Directory = new IO.RunnerDirectoryProxy(Connection, Configuration, request.ApplicationId),
                Cancel = CancelSource.Token
            };

            // update the assembly cache path where necessary to ensure each application has its own
            // folder to help prevent collisions
            var assemblyCachePath = Configuration.AssemblyCachePath;
            if (!string.IsNullOrEmpty(assemblyCachePath) && !string.IsNullOrEmpty(request.ApplicationId))
            {
                assemblyCachePath = Path.Combine(assemblyCachePath, request.ApplicationId);
            }

            // create the runtime environment
            using (var environment = new Environment
            {
                ExecutionContext = context,
                ResolveRemoteAssemblyAsync = new DefaultRemoteAssemblyResolver(AssembliesChannel).ResolveAssembly,
                AssemblyCachePath = request.AssemblyCaching == AssemblyCachingPolicies.Always ? assemblyCachePath : null
            })

            // decode and execute the requested expression
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
                    var errorMessage = new TaskErrorMessage(TaskErrorMessage.Categories.Deserialization, ex);
                    TasksChannel.Send(errorMessage);
                    // indicate to the main thread that the task is done
                    TaskComplete.Set();
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
                            CancelSource.Cancel();

                            // indicate that a timeout occurred
                            Interlocked.Exchange(ref didTimeout, 1);

                            // let the application know immediately the task did not complete due to a timeout
                            // (this way if the task does not cancel soon at least the application
                            // can start a retry)
                            ThreadHelpers.Debug($"RUNNER: sending timeout message");
                            TasksChannel.Send(new TaskTimeoutMessage());
                            ThreadHelpers.Debug($"RUNNER: timeout message sent");

                            // indicate the timeout message was sent
                            Interlocked.Exchange(ref didTimeout, 2);
                        }, null, request.TimeoutInMs, Timeout.Infinite);
                    }

                    // execute the task by invoking the expression.
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
                        CancelSource.Token.ThrowIfCancellationRequested();

                        // otherwise send the result back to the application
                        var resultMessage = new TaskResponseMessage(result);
                        ThreadHelpers.Debug($"RUNNER: sending result message");
                        TasksChannel.Send(resultMessage);
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
                catch (OperationCanceledException)
                {
                    // catch and report cancellations
                    TasksChannel.Send(new TaskCancelMessage());
                }
                catch (Exception ex)
                {
                    // catch and report invocation errors
                    var errorMessage = new TaskErrorMessage(TaskErrorMessage.Categories.Invocation, ex);
                    TasksChannel.Send(errorMessage);
                }
                finally
                {
                    // cleanup
                    timeout?.Dispose();
                }

                // indicate to the main worker thread that the task is done
                TaskComplete.Set();
            }
        }

    }
}