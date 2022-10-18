using Dido.Utilities;
using NLog;
using System;
using System.IO;
using System.Text;
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
        public string Id { get; private set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The connection to the remote application.
        /// </summary>
        public Connection Connection { get; private set; }

        /// <summary>
        /// The request message containing the details of the task this worker is executing.
        /// </summary>
        public TaskRequestMessage? Request { get; private set; }

        /// <summary>
        /// The specific success or failure message of the executed task that is sent back to the application.
        /// </summary>
        public IMessage? Result { get; private set; }

        /// <summary>
        /// Timestamp for when a task starts execution.
        /// </summary>
        public DateTime TaskStarted { get; private set; }

        /// <summary>
        /// Timestamp for when a task stops execution.
        /// </summary>
        public DateTime TaskStopped { get; private set; }

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
        /// A delegate invoked when the task is started. 
        /// </summary>
        private Action<TaskWorker>? OnStart { get; set; }

        /// <summary>
        /// A delegate invoked when the task is complete. 
        /// </summary>
        private Action<TaskWorker>? OnComplete { get; set; }

        /// <summary>
        /// Used to indicate when the task thread is complete.
        /// </summary>
        private AutoResetEvent TaskComplete { get; set; } = new AutoResetEvent(false);

        /// <summary>
        /// The class logger instance.
        /// </summary>
        private readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Create a new worker to process application task requests on the provided connection.
        /// </summary>
        /// <param name="connection"></param>
        public TaskWorker(Connection connection, RunnerConfiguration configuration, TaskTypeMessage taskTypeMessage)
        {
            Connection = connection;
            Configuration = configuration;

            Connection.OnDisconnect = (connection, reason) =>
            {
                // if the connection terminates unexpectedly stop this worker (since the application is no longer connected)
                CancelSource.Cancel();
                TaskComplete.Set();
            };

            // TODO: add "tethering" configuration: what to do if the application connection breaks?
            // TODO: in "tethered" mode, the task should cancel.
            // TODO: in "untethered" mode, the task should continue. what if the task is long running? how to reconnect?
            // TODO: in untethered mode, if the task fails it should wait for the application to reconnect only up to a
            // TODO: maximum time, then simply die.
        }

        /// <summary>
        /// Starts executing the task on a separate thread, invoking the provided handler when complete.
        /// </summary>
        /// <param name="onComplete"></param>
        public void Start(Action<TaskWorker> onStart, Action<TaskWorker> onComplete)
        {
            OnStart = onStart;
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

        /// <inheritdoc/> 
        public void Dispose()
        {
            // remove the disconnection handler so it does not try to run when the connection is disposed
            Connection.OnDisconnect = null;

            WorkThread?.Join(1000);
            Connection.Dispose();
            CancelSource.Dispose();
            TaskComplete.Dispose();
        }

        // TODO: make this DoTetheredWork()?
        /// <summary>
        /// Set up, start, and manage the life cycle of a task request from a single remote connected application.
        /// </summary>
        private void DoWork()
        {
            var tasksChannel = new MessageChannel(Connection, Constants.AppRunner_TaskChannelId);

            try
            {
                TaskStarted = DateTime.UtcNow;
                Logger.Trace($"Worker task {Id} starting");

                // by design, the first message received on the tasks channel is the task request.
                // receive it and execute it on a new thread
                Request = tasksChannel.ReceiveMessage<TaskRequestMessage>();
                OnStart?.Invoke(this);
                var taskExecutionThread = new Thread(() => ExecuteTask(Request));
                taskExecutionThread.Start();

                // block until the task completes or fails
                TaskComplete.WaitOne();

                // cleanup
                taskExecutionThread.Join();
            }
            catch (Exception ex)
            {
                // handle all unexpected exceptions explicitly by notifying the application that an error occurred
                Result = new TaskErrorMessage(TaskErrorMessage.Categories.General, ex);
                tasksChannel.Send(Result);
                CancelSource.Cancel();
                TaskComplete.Set();
                Logger.Trace($"Worker task {Id} failed: {ex}");
            }
            finally
            {
                TaskStopped = DateTime.UtcNow;
                Logger.Info($"Worker task {Id} finished. Elapsed = {(TaskStopped - TaskStarted)}");
                OnComplete?.Invoke(this);
            }
        }

        // TODO: the channels and connection could spontaneously disconnect if the application terminates.
        // TODO: for tethered tasks, this worker should abort.
        // TODO: for untethered tasks, use of a channel should be postponed until connection is re-established

        /// <summary>
        /// Executes the given task.
        /// </summary>
        /// <param name="request"></param>
        private async void ExecuteTask(TaskRequestMessage request)
        {
            var isJob = !string.IsNullOrEmpty(request.JobId);

            // create communication channels to the application for: task communication, assemblies
            // TODO: what happens when the connection terminates?
            // TODO: for tethered tasks the runner should abort.
            // TODO: for untethered tasks these channels will need to be recreated.
            var tasksChannel = new MessageChannel(Connection, Constants.AppRunner_TaskChannelId);
            var assembliesChannel = new MessageChannel(Connection, Constants.AppRunner_AssemblyChannelId);

            // set up a handler to process other task-related messages.
            // NOTE: this handler runs in a separate thread
            tasksChannel.OnMessageReceived = (message, channel) =>
            {
                try
                {
                    switch (message)
                    {
                        // cancel the running task
                        case TaskCancelMessage cancel:
                            CancelSource.Cancel();
                            break;

                        default:
                            throw new UnhandledMessageException(message);
                    }
                }
                catch (Exception ex)
                {
                    // handle all unexpected exceptions explicitly by notifying the application that an error occurred
                    Result = new TaskErrorMessage(TaskErrorMessage.Categories.General, ex);
                    channel.Send(Result);
                    CancelSource.Cancel();
                    TaskComplete.Set();
                }
            };

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
            // folder to prevent file collisions
            var assemblyCachePath = Configuration.AssemblyCachePath;
            if (!string.IsNullOrEmpty(assemblyCachePath) && !string.IsNullOrEmpty(request.ApplicationId))
            {
                assemblyCachePath = Path.Combine(assemblyCachePath, request.ApplicationId);
            }

            // create the runtime environment, then decode and execute the requested expression
            using (var environment = new Environment
            {
                ExecutionContext = context,
                ResolveRemoteAssemblyAsync = new DefaultRemoteAssemblyResolver(assembliesChannel).ResolveAssembly,
                AssemblyCachePath = request.AssemblyCaching == AssemblyCachingPolicies.Always ? assemblyCachePath : null,
                CachedAssemblyEncryptionKey = request.CachedAssemblyEncryptionKey,
            })
            {
                Func<ExecutionContext, object>? expression = null;

                // deserialize the expression
                try
                {
                    using (var stream = new MemoryStream(request.Bytes))
                    {
                        expression = await ExpressionSerializer.DeserializeAsync<object>(stream, environment);
                    }
                }
                catch (Exception ex)
                {
                    // catch and report deserialization errors
                    Result = new TaskErrorMessage(TaskErrorMessage.Categories.Deserialization, ex);
                    tasksChannel.Send(Result);
                    // indicate to the main thread that the task is done
                    TaskComplete.Set();
                    return;
                }

                Timer? timeout = null;
                try
                {
                    // set up a timer if necessary to cancel the task if it takes too long to execute
                    long didTimeout = 0;
                    if (request.TimeoutInMs > 0)
                    {
                        timeout = new Timer((arg) =>
                        {
                            // try to cancel the task
                            CancelSource.Cancel();

                            // indicate that a timeout occurred
                            Interlocked.Exchange(ref didTimeout, 1);

                            // let the application know immediately the task did not complete due to a timeout
                            // (this way if the task does not stop soon at least the application can start a retry)
                            Result = new TaskTimeoutMessage();
                            tasksChannel.Send(Result);

                            // indicate the timeout message was sent
                            Interlocked.Exchange(ref didTimeout, 2);
                        }, null, request.TimeoutInMs, Timeout.Infinite);
                    }

                    // if the task request is a job, notify the application that task processing is starting normally.
                    // the application will then need to interact with the job via the mediator using the jobs API,
                    // but will receive no more traffic on the tasks channel.
                    // NOTE: although the application is notified that the task is starting, the task may not have
                    // all the assemblies it needs. Part of the stipulation of using the jobs API is that the application
                    // must remain online/connected after submitting a job for at least a minimum amount of time so
                    // any necessary assemblies can be transferred.
                    if (isJob)
                    {
                        tasksChannel.Send(new AcknowledgedMessage());
                    }

                    // execute the task by invoking the expression.
                    // the task will run for as long as necessary.
                    var result = expression?.Invoke(environment.ExecutionContext);

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

                        // otherwise create a message with the result, and send it to the application
                        // (unless this task is a job in which case the runner will send the result
                        // to the mediator when this worker completes)
                        Result = new TaskResponseMessage(result);
                        if (!isJob)
                        {
                            tasksChannel.Send(Result);
                        }
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
                    Result = new TaskCancelMessage();
                    if (!isJob)
                    {
                        tasksChannel.Send(Result);
                    }
                }
                catch (Exception ex)
                {
                    // catch and report invocation errors
                    Result = new TaskErrorMessage(TaskErrorMessage.Categories.Invocation, ex);
                    if (!isJob)
                    {
                        tasksChannel.Send(Result);
                    }
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