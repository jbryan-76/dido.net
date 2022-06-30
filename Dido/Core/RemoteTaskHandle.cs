using DidoNet.IO;
using NLog;
using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace DidoNet
{
    public class RemoteTaskHandle : ITaskHandle
    {
        //public string Id
        //{
        //    get
        //    {
        //        // TODO: decode RunnerId+TaskId
        //    }
        //    set
        //    {
        //        // TODO: encode RunnerId+TaskId
        //    }
        //}

        public string RunnerId { get; private set; }

        public string TaskId { get; private set; }

        public Uri RunnerUri { get; private set; }

        public bool IsFinished { get { return Interlocked.Read(ref Finished) != 0; } }

        public object? Result { get; private set; } = null;

        public Exception? Error { get; private set; } = null;

        // TODO: add delegate OnError
        // TODO: add delegate OnDisconnect

        internal Connection RunnerConnection { get; set; }

        internal CancellationToken CancellationToken { get; set; }

        internal ApplicationIOProxy IoProxy { get; set; }

        internal MessageChannel ControlChannel { get; set; }

        internal MessageChannel TasksChannel { get; set; }

        internal MessageChannel AssembliesChannel { get; set; }

        private readonly AutoResetEvent FinishedEvent = new AutoResetEvent(false);

        private long Finished = 0;

        /// <summary>
        /// The class logger instance.
        /// </summary>
        private readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public void WaitUntilFinished()
        {
            FinishedEvent.WaitOne();
        }

        public void ThrowIfError()
        {
            if (Error != null)
            {
                throw Error;
            }
        }

        public T GetResult<T>()
        {
            if (Result == null)
            {
                throw new InvalidCastException($"The remote task {nameof(Result)} value is null.");
            }

            // Result will always be a non-task value.
            // if the desired type is a Task, wrap the result in a properly typed
            // Task so it can be awaited.
            var intendedType = typeof(T);
            if (intendedType.IsGenericType && intendedType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // convert the result to the expected generic task type
                var resultGenericType = intendedType.GenericTypeArguments[0];
                var resultValue = Convert.ChangeType(Result, resultGenericType);
                // then construct a Task.FromResult using the expected generic type
                var method = typeof(Task).GetMethod(nameof(Task.FromResult), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                method = method!.MakeGenericMethod(resultGenericType);
                return (T)method!.Invoke(null, new[] { resultValue })!;
            }
            else
            {
                return (T)Convert.ChangeType(Result, typeof(T));
            }
        }

        public void Dispose()
        {
            RunnerConnection?.Dispose();
            FinishedEvent.Dispose();
        }

        // TODO: custom message channels

        internal async Task StartAsync<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            Configuration configuration,
            CancellationToken cancellationToken)
        {
            var serializedExpression = await ExpressionSerializer.SerializeAsync(expression);

            CancellationToken = cancellationToken;

            // choose a runner to execute the expression
            RunnerUri = Helpers.SelectRunner(configuration);

            var connectionSettings = new ClientConnectionSettings
            {
                ValidaionPolicy = configuration.ServerCertificateValidationPolicy,
                Thumbprint = configuration.ServerCertificateThumbprint
            };

            // create a secure connection to the remote runner
            RunnerConnection = new Connection(RunnerUri!.Host, RunnerUri.Port, null, connectionSettings);
            RunnerConnection.OnDisconnect = (connection, reason) =>
            {
                switch (reason)
                {
                    case DisconnectionReasons.Unknown:
                    case DisconnectionReasons.Error:
                    case DisconnectionReasons.Dropped:
                    case DisconnectionReasons.Unresponsive:
                        Error = new DisconnectedException($"The connection closed unexpectedly.");
                        break;
                }
                Interlocked.Exchange(ref Finished, 1);
                FinishedEvent.Set();
            };

            Logger.Trace($"Starting expression via {nameof(RemoteTaskHandle)}{nameof(StartAsync)} to {RunnerUri}");

            // create communication channels to the runner for: control messages, task messages, assembly messages
            ControlChannel = new MessageChannel(RunnerConnection, Constants.AppRunner_ControlChannelId);
            TasksChannel = new MessageChannel(RunnerConnection, Constants.AppRunner_TaskChannelId);
            AssembliesChannel = new MessageChannel(RunnerConnection, Constants.AppRunner_AssemblyChannelId);

            // inform the runner about what kind of task will be executed
            ControlChannel.Send(new TaskTypeMessage(TaskTypeMessage.TaskTypes.Untethered));

            // create a proxy to handle file-system IO requests from the expression executing on the runner
            IoProxy = new ApplicationIOProxy(RunnerConnection);

            // handle assembly messages
            AssembliesChannel.OnMessageReceived = (message, channel) => Helpers.HandleAssemblyMessages(configuration, message, channel);

            // kickoff the remote task by serializing the expression and transmitting it to the runner
            var requestMessage = new TaskRequestMessage(
                serializedExpression,
                configuration.Id,
                configuration.GetActualAssemblyCachingPolicy(),
                configuration.CachedAssemblyEncryptionKey,
                configuration.TimeoutInMs);
            TasksChannel.Send(requestMessage);

            // when the cancellation token is triggered, send a cancel message to the runner
            CancellationToken.Register(() =>
            {
                TasksChannel.Send(new TaskCancelMessage());
            });

            // the runner will send one message before the task is started. synchronously wait for that message
            var message = TasksChannel.ReceiveMessage();
            ProcessMessage(message);

            // then the runner will send another message when the task completes
            TasksChannel.OnMessageReceived = (message, channel) => ProcessMessage(message);
        }

        private void ProcessMessage(IMessage message)
        {
            switch (message)
            {
                case TaskIdMessage taskId:
                    RunnerId = taskId.RunnerId;
                    TaskId = taskId.TaskId;
                    return;
                case TaskResponseMessage response:
                    Result = response.Result;
                    Interlocked.Exchange(ref Finished, 1);
                    FinishedEvent.Set();
                    return;
                case IErrorMessage error:
                    Result = null;
                    Error = error.Exception;
                    Interlocked.Exchange(ref Finished, 1);
                    FinishedEvent.Set();
                    return;
                default:
                    Result = null;
                    Error = new UnhandledMessageException(message);
                    Interlocked.Exchange(ref Finished, 1);
                    FinishedEvent.Set();
                    return;
            }
        }
    }
}
