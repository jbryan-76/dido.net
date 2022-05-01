﻿using System.Linq.Expressions;
using System.Runtime.Loader;

// TODO: best name? dido (distribute, disseminate, divide, spread, separate, put) vs alio (to another place)

// generate self-signed certs automatically
// https://stackoverflow.com/questions/695802/using-ssl-and-sslstream-for-peer-to-peer-authentication

// 1) generate a cert
// openssl req -newkey rsa:2048 -new -nodes -keyout test.key -x509 -days 365 -out test.pem
// 2) convert to a pkcs12 pfx
// openssl pkcs12 -export -out cert.pfx -inkey test.key -in test.pem -password pass:1234

namespace DidoNet
{
    /// <summary>
    /// Provides support for executing expressions locally for debugging, or remotely for
    /// distributed processing.
    /// </summary>
    public static class Dido
    {
        /// <summary>
        /// Signature for a method that handles the result of asynchronous
        /// execution of a task.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="result"></param>
        public delegate void ResultHandler<T>(T result);

        /// <summary>
        /// Signature for a method that handles execptions during asynchronous
        /// execution of a task.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="result"></param>
        public delegate void ExceptionHandler(Exception ex);

        /// <summary>
        /// Execute the provided expression as a task and return its result.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression">The expression to execute.</param>
        /// <param name="executionMode">An optional execution mode to override the currently configured mode.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static Task<Tprop> RunAsync<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            Configuration? configuration = null, CancellationToken? cancellationToken = null)
        {
            configuration ??= new Configuration();

            switch (configuration.ExecutionMode)
            {
                case ExecutionModes.Local:
                    return RunLocalAsync<Tprop>(expression, configuration, cancellationToken);
                case ExecutionModes.Remote:
                    return RunRemoteAsync<Tprop>(expression, configuration, cancellationToken);
                default:
                    return Task.FromException<Tprop>(new InvalidOperationException($"Illegal or unknown value for '{nameof(configuration.ExecutionMode)}': {configuration.ExecutionMode}"));
            }
        }

        /// <summary>
        /// Execute the provided expression as a task and invoke the provided handler with the result.
        /// Use this form when the expression is expected to take a long time to complete.
        /// <para/>NOTE the provided handler is run in a separate thread.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression">The expression to execute.</param>
        /// <param name="resultHandler">A result handler invoked after the expression completes. 
        /// Note this handler runs in a separate thread.</param>
        /// <param name="executionMode">An optional execution mode to override the currently configured mode.</param>
        public static void Run<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            ResultHandler<Tprop> resultHandler,
            Configuration? configuration = null,
            ExceptionHandler? execptionHandler = null,
            CancellationToken? cancellationToken = null)
        {
            var thread = new Thread(async () =>
            {
                try
                {
                    var result = await RunAsync(expression, configuration, cancellationToken);
                    resultHandler(result);
                }
                catch (Exception ex)
                {
                    execptionHandler?.Invoke(ex);
                }
            });
            thread.Start();
        }

        /// <summary>
        /// Execute the provided expression as a task locally (ie using the current application domain
        /// and environment).
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static Task<Tprop> RunLocalAsync<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            Configuration? configuration = null,
            CancellationToken? cancellationToken = null)
        {
            configuration ??= new Configuration();

            // create an (unused) cancellation token source if no cancellation token has been provided
            CancellationTokenSource? source = null;
            if (cancellationToken == null)
            {
                source = new CancellationTokenSource();
                cancellationToken = source.Token;
            }

            try
            {
#if (DEBUG)
                return DebugRunLocalAsync(expression, configuration, cancellationToken.Value);
#else
                return ReleaseRunLocalAsync(expression, configuration, cancellationToken.Value);
#endif
            }
            finally
            {
                source?.Dispose();
            }
        }

        // TODO: "jobs" API: must use a mediator and runners
        // TODO: SubmitJobAsync(expression) => submit request to mediator, get back an id
        // TODO: JobStatusAsync(id) => get job status/result by job id
        // TODO: CancelJobAsync(id) => cancel job
        // TODO: AddJobHandler(id,handler) => invoke handler when job is done (and have background thread poll)

        /// <summary>
        /// Execute the provided expression as a task in a remote environment.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static async Task<Tprop> RunRemoteAsync<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            Configuration configuration,
            CancellationToken? cancellationToken = null)
        {
            if (configuration.MediatorUri == null && configuration.RunnerUri == null)
            {
                throw new InvalidOperationException($"Configuration error: At least one of {nameof(Configuration.MediatorUri)} or {nameof(Configuration.RunnerUri)} must be set to a valid value.");
            }

            // create an (unused) cancellation token source if no cancellation token has been provided
            CancellationTokenSource? source = null;
            if (cancellationToken == null)
            {
                source = new CancellationTokenSource();
                cancellationToken = source.Token;
            }

            try
            {
                int tries = 0;
                while (true)
                {
                    ++tries;
                    try
                    {
                        return await DoRunRemoteAsync(expression, configuration, cancellationToken.Value);
                    }
                    // only retry if the exception is a TimeoutException or RunnerBusyException
                    // (otherwise the exception is for a different error and should bubble up)
                    catch (Exception e) when (e is TimeoutException || e is RunnerBusyException)
                    {
                        // don't retry forever
                        if (configuration.MaxTries > 0 && tries == configuration.MaxTries)
                        {
                            throw;
                        }
                    }
                }
            }
            finally
            {
                source?.Dispose();
            }
        }

        /// <summary>
        /// Execute the provided expression as a task in a remote environment.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        /// <exception cref="TaskGeneralException"></exception>
        /// <exception cref="TaskDeserializationException"></exception>
        /// <exception cref="TaskInvokationException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        private static async Task<Tprop> DoRunRemoteAsync<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            Configuration configuration,
            CancellationToken cancellationToken)
        {
            var runnerUri = configuration.RunnerUri;

            // if there is a mediator configured but no runner, ask the mediator to choose a runner
            if (configuration.MediatorUri != null && runnerUri == null)
            {
                // open a connection to the mediator
                using (var mediatorConnection = new Connection(configuration.MediatorUri.Host, configuration.MediatorUri.Port))
                {
                    // create the communications channel and request an available runner from the mediator
                    var applicationChannel = new MessageChannel(mediatorConnection, Constants.ApplicationChannelNumber);
                    applicationChannel.Send(new RunnerRequestMessage());

                    // receive and process the response
                    var message = applicationChannel.ReceiveMessage();
                    switch (message)
                    {
                        case RunnerResponseMessage response:
                            runnerUri = new Uri(response.Endpoint);
                            break;

                        case RunnerNotAvailableMessage notAvailable:
                            throw new RunnerNotAvailableException();

                        default:
                            throw new InvalidOperationException($"Unknown message type '{message.GetType()}'");
                    }
                }
            }

            // create a secure connection to the remote runner
            using (var runnerConnection = new Connection(runnerUri!.Host, runnerUri.Port))
            {
                // TODO: refactor below into separate class to handle all the business logic

                // create communication channels to the runner for: task messages, assemblies, files
                var tasksChannel = new MessageChannel(runnerConnection, Constants.TaskChannelNumber);
                var assembliesChannel = new MessageChannel(runnerConnection, Constants.AssemblyChannelNumber);
                var filesChannel = runnerConnection.GetChannel(Constants.FileChannelNumber);

                // TODO: cleanup once stable
                tasksChannel.Channel.Name = "DIDO";
                assembliesChannel.Channel.Name = "DIDO";
                filesChannel.Name = "DIDO";

                // TODO: handle file messages

                // handle assembly messages
                assembliesChannel.OnMessageReceived = async (message, channel) =>
                {
                    switch (message)
                    {
                        case AssemblyRequestMessage request:
                            if (string.IsNullOrEmpty(request.AssemblyName))
                            {
                                var response = new AssemblyErrorMessage(new ArgumentNullException(nameof(AssemblyRequestMessage.AssemblyName)));
                                channel.Send(response);
                                return;
                            }

                            // resolve the desired assembly and send it back
                            using (var stream = await configuration.ResolveLocalAssemblyAsync(request.AssemblyName))
                            using (var mem = new MemoryStream())
                            {
                                IMessage response;
                                if (stream == null)
                                {
                                    response = new AssemblyErrorMessage(new FileNotFoundException($"Assembly '{request.AssemblyName}' could not be resolved."));
                                }
                                else
                                {
                                    stream.CopyTo(mem);
                                    response = new AssemblyResponseMessage(mem.ToArray());
                                }
                                ThreadHelpers.Debug($"dido: sending AssemblyResponseMessage");
                                channel.Send(response);
                                ThreadHelpers.Debug($"dido: AssemblyRequestMessage WROTE");
                            }
                            break;
                    }
                };

                // handle task messages
                var responseSource = new TaskCompletionSource<IMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                tasksChannel.OnMessageReceived = (message, channel) =>
                {
                    ThreadHelpers.Debug($"dido: received message {message.GetType()}");
                    // after kicking off the task request below, exactly one response message is expected back,
                    // which is received in this handler and attached to the response source to be awaited
                    // below and then processed
                    responseSource.SetResult(message);
                };

                // kickoff the task by serializing the expression and transmitting it to the remote runner
                using (var stream = new MemoryStream())
                {
                    await ExpressionSerializer.SerializeAsync(expression, stream);
                    var requestMessage = new TaskRequestMessage(stream.ToArray(), configuration.TimeoutInMs);
                    lock (tasksChannel)
                    {
                        tasksChannel.Send(requestMessage);
                    }
                }

                // when the cancellation token is canceled, send the cancel message to the runner
                cancellationToken.Register(() =>
                {
                    ThreadHelpers.Debug($"dido: sending cancel message");
                    lock (tasksChannel)
                    {
                        tasksChannel.Send(new TaskCancelMessage());
                    }
                });

                // now wait until a response is received

                ThreadHelpers.Debug($"dido: waiting for response...");
                Task.WaitAll(responseSource.Task);
                var message = responseSource.Task.Result;
                ThreadHelpers.Debug($"dido: got response; starting dispose...");

                // cleanup the connection
                runnerConnection.Dispose();
                ThreadHelpers.Debug($"dido: disposed");

                // yield the result
                switch (message)
                {
                    case TaskResponseMessage response:
                        return (Tprop)Convert.ChangeType(response.Result, typeof(Tprop));
                    case TaskErrorMessage error:
                        switch (error.ErrorType)
                        {
                            case TaskErrorMessage.ErrorTypes.General:
                                throw new TaskGeneralException(error.Error);
                            case TaskErrorMessage.ErrorTypes.Deserialization:
                                throw new TaskDeserializationException(error.Error);
                            case TaskErrorMessage.ErrorTypes.Invokation:
                                throw new TaskInvokationException(error.Error);
                            default:
                                throw new InvalidOperationException($"Task error type {error.ErrorType} is unknown");
                        }
                    case TaskTimeoutMessage timeout:
                        throw string.IsNullOrEmpty(timeout.Message) ? new TimeoutException() : new TimeoutException(timeout.Message);
                    case TaskCancelMessage cancel:
                        throw string.IsNullOrEmpty(cancel.Message) ? new OperationCanceledException() : new OperationCanceledException(cancel.Message);
                    case RunnerBusyMessage busy:
                        throw string.IsNullOrEmpty(busy.Message) ? new RunnerBusyException() : new RunnerBusyException(busy.Message);
                    default:
                        throw new InvalidOperationException($"Unknown message type '{message.GetType()}'");
                }
            }
        }

        /// <summary>
        /// Serialize the given expression to a byte array. The resulting array
        /// can either be stored or transmitted, and later deserialized and executed.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static async Task<byte[]> SerializeAsync<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            using (var stream = new MemoryStream())
            {
                await ExpressionSerializer.SerializeAsync(expression, stream);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserialize an expression from a byte array, using the provided environment
        /// to resolve any required assemblies and load them into the proper runtime assembly
        /// context.
        /// <para/>NOTE the byte array must be created with SerializeAsync().
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="data"></param>
        /// <param name="env"></param>
        /// <returns></returns>
        public static Task<Func<ExecutionContext, Tprop>> DeserializeAsync<Tprop>(byte[] data, Environment env)
        {
            using (var stream = new MemoryStream(data))
            {
                return ExpressionSerializer.DeserializeAsync<Tprop>(stream, env);
            }
        }

        /// <summary>
        /// For debugging only: perform local mode execution of the provided expression but
        /// include serialization and deserialization steps to confirm the expression obeys
        /// all serialization requirements and all assemblies can be resolved and loaded into
        /// a temporary AssemblyLoadContext.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        internal static async Task<Tprop> DebugRunLocalAsync<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            Configuration configuration,
            CancellationToken cancellationToken)
        {
            var context = new ExecutionContext
            {
                Cancel = cancellationToken,
                ExecutionMode = ExecutionModes.Local,
            };

            var env = new Environment
            {
                ExecutionContext = context,
                AssemblyContext = new AssemblyLoadContext(Guid.NewGuid().ToString(), true),
                ResolveRemoteAssemblyAsync = new DebugRemoteAssemblyResolver(AppContext.BaseDirectory).ResolveAssembly
            };

            try
            {
                // to adequately test the end-to-end processing and current configuration,
                // serialize and deserialize the expression
                var data = await SerializeAsync(expression);
                var func = await DeserializeAsync<Tprop>(data, env);

                // run the expression with the optional configured timeout and return its result
                var result = await Task
                    .Run(() => func.Invoke(context))
                    .WaitAsync(TimeSpan.FromMilliseconds(configuration.TimeoutInMs));
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
            finally
            {
                env.AssemblyContext.Unload();
            }
        }

        /// <summary>
        /// Optimized for release builds: perform local mode execution of the provided expression
        /// by directly compiling and invoking it.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        internal static async Task<Tprop> ReleaseRunLocalAsync<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            Configuration configuration,
            CancellationToken cancellationToken)
        {
            var context = new ExecutionContext
            {
                Cancel = cancellationToken,
                ExecutionMode = ExecutionModes.Local,
            };

            // when executing locally in release mode, simply compile and invoke the expression,
            // bypassing all internal (de)serialization and validation checks
            var func = expression.Compile();

            // run the expression with the optional configured timeout and return its result
            var result = await Task
                .Run(() => func.Invoke(context))
                .WaitAsync(TimeSpan.FromMilliseconds(configuration.TimeoutInMs));
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
    }
}