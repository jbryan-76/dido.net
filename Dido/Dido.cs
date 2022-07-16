using Dido.Utilities;
using DidoNet.IO;
using NLog;
using System;
using System.IO;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

// generate self-signed certs automatically:
// https://stackoverflow.com/questions/695802/using-ssl-and-sslstream-for-peer-to-peer-authentication
// view installed certificates: certmgr.msc
// install a cert programmatically:
// https://docs.microsoft.com/en-us/troubleshoot/developer/dotnet/framework/general/install-pfx-file-using-x509certificate
// install a cert manually:
// https://community.spiceworks.com/how_to/1839-installing-self-signed-ca-certificate-in-windows

// 1) generate a cert
// openssl req -newkey rsa:2048 -new -nodes -keyout test.key -x509 -days 365 -out test.pem
// NOTE: the "Common Name"/FQDN (fully qualified domain name) must match the "targetHost" parameter of the Connection
// constructor specialized for connecting a client to a server.
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
        /// Signature for a method that handles exceptions during asynchronous
        /// execution of a task.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="result"></param>
        public delegate void ExceptionHandler(Exception ex);

        /// <summary>
        /// The optional global configuration. If an explicit configuration is not provided to a Run method,
        /// the global configuration will be used.
        /// </summary>
        public static Configuration? GlobalConfiguration = null;

        /// <summary>
        /// The class logger instance.
        /// </summary>
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Execute the provided expression as a task and return its result.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression">The expression to execute.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static Task<Tprop> RunAsync<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            Configuration? configuration = null,
            CancellationToken? cancellationToken = null)
        {
            configuration ??= GlobalConfiguration ?? new Configuration();

            // force the execution mode to local if no remote service is configured
            var executionMode = configuration.ExecutionMode;
            if (configuration.MediatorUri == null && configuration.RunnerUri == null && executionMode == ExecutionModes.Remote)
            {
                Logger.Warn($"Forcing execution mode to {nameof(ExecutionModes.Local)}: no mediator nor runner configured.");
                executionMode = ExecutionModes.Local;
            }

            switch (executionMode)
            {
                case ExecutionModes.Local:
                    return RunLocalAsync<Tprop>(expression, configuration, cancellationToken);
                case ExecutionModes.DebugLocal:
#if DEBUG
                    return RunLocalAsync<Tprop>(expression, configuration, cancellationToken);
#else
                    return RunRemoteAsync<Tprop>(expression, configuration, cancellationToken);
#endif
                case ExecutionModes.Remote:
                    return RunRemoteAsync<Tprop>(expression, configuration, cancellationToken);
                default:
                    return Task.FromException<Tprop>(new InvalidOperationException($"Illegal or unknown value for '{nameof(configuration.ExecutionMode)}': {configuration.ExecutionMode}"));
            }
        }

        /// <summary>
        /// Execute the provided expression as a task and invoke the provided handler with the result.
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
        /// Execute the provided expression as a task locally (i.e. using the current application domain
        /// and environment), regardless of the value of configuration.ExecutionMode.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static Task<Tprop> RunLocalAsync<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            Configuration? configuration = null,
            CancellationToken? cancellationToken = null)
        {
            configuration ??= GlobalConfiguration ?? new Configuration();

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
        /// Execute the provided expression as a task in a remote environment, regardless of the value of configuration.ExecutionMode.
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
                        using (var handle = await StartRemoteAsync(
                            expression,
                            configuration,
                            cancellationToken.Value))
                        {
                            handle.WaitUntilFinished();
                            handle.ThrowIfError();
                            return handle.GetResult<Tprop>();
                        }
                        //return await DoRunRemoteAsync(expression, configuration, cancellationToken.Value);
                    }
                    // only retry if the exception is a TimeoutException or RunnerBusyException
                    // (otherwise the exception is for a different error and should bubble up)
                    catch (Exception e) when (e is TimeoutException || e is RunnerBusyException)
                    {
                        // don't retry forever
                        if (configuration.MaxTries > 0 && tries >= configuration.MaxTries)
                        {
                            throw;
                        }
                        else
                        {
                            // exponential back-off: roughly [100, 700, 3000, 10000] ms
                            ThreadHelpers.Yield((int)Math.Pow(100, Math.Sqrt(Math.Max(tries, 4))));
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
        /// Execute the provided expression as a task and return its result.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression">The expression to execute.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static Task<ITaskHandle> StartAsync<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            Configuration? configuration = null,
            CancellationToken? cancellationToken = null)
        {
            configuration ??= GlobalConfiguration ?? new Configuration();

            // force the execution mode to local if no remote service is configured
            var executionMode = configuration.ExecutionMode;
            if (configuration.MediatorUri == null && configuration.RunnerUri == null && executionMode == ExecutionModes.Remote)
            {
                Logger.Warn($"Forcing execution mode to {nameof(ExecutionModes.Local)}: no mediator nor runner configured.");
                executionMode = ExecutionModes.Local;
            }

            switch (executionMode)
            {
                case ExecutionModes.Local:
                    return StartLocalAsync(expression, configuration, cancellationToken);
                case ExecutionModes.DebugLocal:
#if DEBUG
                    return StartLocalAsync(expression, configuration, cancellationToken);
#else
                    return StartRemoteAsync(expression, configuration, cancellationToken);
#endif
                case ExecutionModes.Remote:
                    return StartRemoteAsync(expression, configuration, cancellationToken);
                default:
                    return Task.FromException<ITaskHandle>(new InvalidOperationException($"Illegal or unknown value for '{nameof(configuration.ExecutionMode)}': {configuration.ExecutionMode}"));
            }
        }

        public static Task<ITaskHandle> ContinueAsync<Tprop>(
            string runnerId, string taskId,
            Configuration? configuration = null,
            CancellationToken? cancellationToken = null)
        {
            configuration ??= GlobalConfiguration ?? new Configuration();

            // force the execution mode to local if no remote service is configured
            var executionMode = configuration.ExecutionMode;
            if (configuration.MediatorUri == null && configuration.RunnerUri == null && executionMode == ExecutionModes.Remote)
            {
                Logger.Warn($"Forcing execution mode to {nameof(ExecutionModes.Local)}: no mediator nor runner configured.");
                executionMode = ExecutionModes.Local;
            }

            switch (executionMode)
            {
                case ExecutionModes.Local:
                    return ContinueLocalAsync(runnerId, taskId, configuration, cancellationToken);
                case ExecutionModes.DebugLocal:
#if DEBUG
                    return ContinueLocalAsync(runnerId, taskId, configuration, cancellationToken);
#else
                    return StartRemoteAsync(expression, configuration, cancellationToken);
#endif
                case ExecutionModes.Remote:
                    return ContinueRemoteAsync(runnerId, taskId, configuration, cancellationToken);
                default:
                    return Task.FromException<ITaskHandle>(new InvalidOperationException($"Illegal or unknown value for '{nameof(configuration.ExecutionMode)}': {configuration.ExecutionMode}"));
            }
        }

        internal static Task<ITaskHandle> StartLocalAsync<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            Configuration? configuration = null,
            CancellationToken? cancellationToken = null)
        {
            configuration ??= GlobalConfiguration ?? new Configuration();

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
                return DebugStartLocalAsync(expression, configuration, cancellationToken.Value);
#else
                return ReleaseStartLocalAsync(expression, configuration, cancellationToken.Value);
#endif
            }
            finally
            {
                source?.Dispose();
            }
        }

        internal static Task<ITaskHandle> ContinueLocalAsync(
            string runnerId, string taskId,
            Configuration? configuration = null,
            CancellationToken? cancellationToken = null)
        {
            configuration ??= GlobalConfiguration ?? new Configuration();

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
                return DebugContinueLocalAsync(runnerId, taskId, configuration, cancellationToken.Value);
#else
                return ReleaseContinueLocalAsync(expression, configuration, cancellationToken.Value);
#endif
            }
            finally
            {
                source?.Dispose();
            }
        }

        internal static async Task<ITaskHandle> StartRemoteAsync<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            Configuration configuration,
            CancellationToken? cancellationToken = null)
        {
            // create an (unused) cancellation token source if no cancellation token has been provided
            CancellationTokenSource? source = null;
            if (cancellationToken == null)
            {
                source = new CancellationTokenSource();
                cancellationToken = source.Token;
            }

            var handle = new RemoteTaskHandle();
            await handle.StartAsync(expression, configuration, cancellationToken.Value);
            return handle;
        }

        internal static async Task<ITaskHandle> ContinueRemoteAsync(
            string runnerId, string taskId,
            Configuration configuration,
            CancellationToken? cancellationToken = null)
        {
            // create an (unused) cancellation token source if no cancellation token has been provided
            CancellationTokenSource? source = null;
            if (cancellationToken == null)
            {
                source = new CancellationTokenSource();
                cancellationToken = source.Token;
            }

            // TODO: reconnect to the runner 

            throw new NotImplementedException();
            return new RemoteTaskHandle();
        }

        ///// <summary>
        ///// Execute the provided expression as a task in a remote environment.
        ///// </summary>
        ///// <typeparam name="Tprop"></typeparam>
        ///// <param name="expression"></param>
        ///// <returns></returns>
        ///// <exception cref="TaskGeneralException"></exception>
        ///// <exception cref="TaskDeserializationException"></exception>
        ///// <exception cref="TaskInvocationException"></exception>
        ///// <exception cref="InvalidOperationException"></exception>
        //private static async Task<Tprop> DoRunRemoteAsync<Tprop>(
        //    Expression<Func<ExecutionContext, Tprop>> expression,
        //    Configuration configuration,
        //    CancellationToken cancellationToken)
        //{
        //    using (var handle = await StartRemoteAsync(
        //        expression,
        //        configuration,
        //        cancellationToken))
        //    {
        //        handle.WaitUntilFinished();
        //        handle.ThrowIfError();
        //        return handle.GetResult<Tprop>();
        //    }

        //    //// choose a runner to execute the expression
        //    //var runnerUri = Helpers.SelectRunner(configuration);

        //    //var connectionSettings = new ClientConnectionSettings
        //    //{
        //    //    ValidaionPolicy = configuration.ServerCertificateValidationPolicy,
        //    //    Thumbprint = configuration.ServerCertificateThumbprint
        //    //};

        //    //// create a secure connection to the remote runner
        //    //using (var runnerConnection = new Connection(runnerUri!.Host, runnerUri.Port, null, connectionSettings))
        //    //{
        //    //    Logger.Trace($"Executing expression via {nameof(RunRemoteAsync)} to {runnerUri}");

        //    //    // create communication channels to the runner for: control messages, task messages, assembly messages
        //    //    var controlChannel = new MessageChannel(runnerConnection, Constants.AppRunner_ControlChannelId);
        //    //    var tasksChannel = new MessageChannel(runnerConnection, Constants.AppRunner_TaskChannelId);
        //    //    var assembliesChannel = new MessageChannel(runnerConnection, Constants.AppRunner_AssemblyChannelId);

        //    //    // inform the runner about what kind of task will be executed
        //    //    controlChannel.Send(new TaskTypeMessage(TaskTypeMessage.TaskTypes.Tethered));

        //    //    // create a proxy to handle file-system IO requests from the expression executing on the runner
        //    //    var ioProxy = new ApplicationIOProxy(runnerConnection);

        //    //    // handle assembly messages
        //    //    assembliesChannel.OnMessageReceived = (message, channel) => Helpers.HandleAssemblyMessages(configuration, message, channel);

        //    //    //// after kicking off the task request below, exactly one response message is expected back,
        //    //    //// which is received in this handler and attached to a TaskCompletionSource to be awaited
        //    //    //// below and then processed
        //    //    //var responseSource = new TaskCompletionSource<IMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        //    //    //tasksChannel.OnMessageReceived = (message, channel) =>
        //    //    //{
        //    //    //    responseSource.SetResult(message);
        //    //    //};

        //    //    // kickoff the remote task by serializing the expression and transmitting it to the runner
        //    //    var requestMessage = new TaskRequestMessage(
        //    //        await ExpressionSerializer.SerializeAsync(expression),
        //    //        configuration.Id,
        //    //        configuration.GetActualAssemblyCachingPolicy(),
        //    //        configuration.CachedAssemblyEncryptionKey,
        //    //        configuration.TimeoutInMs);
        //    //    tasksChannel.Send(requestMessage);

        //    //    // when the cancellation token is triggered, send a cancel message to the runner
        //    //    cancellationToken.Register(() =>
        //    //    {
        //    //        tasksChannel.Send(new TaskCancelMessage());
        //    //    });

        //    //    // the runner will send one message before the task is started. synchronously wait for that message
        //    //    var message = tasksChannel.ReceiveMessage();
        //    //    switch (message)
        //    //    {
        //    //        case TaskIdMessage taskId:
        //    //            // TODO:??
        //    //            break;
        //    //        case IErrorMessage error:
        //    //            throw error.Exception;
        //    //        default:
        //    //            throw new UnhandledMessageException(message);
        //    //    }

        //    //    //// wait until a response is received
        //    //    //Task.WaitAll(responseSource.Task);
        //    //    //var message = responseSource.Task.Result;

        //    //    // then the runner will send another message when the task completes
        //    //    message = tasksChannel.ReceiveMessage();

        //    //    // yield the result
        //    //    switch (message)
        //    //    {
        //    //        case TaskResponseMessage response:
        //    //            return response.GetResult<Tprop>();
        //    //        case IErrorMessage error:
        //    //            throw error.Exception;
        //    //        default:
        //    //            throw new UnhandledMessageException(message);
        //    //    }
        //    //}
        //}

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
            Logger.Trace($"Executing expression via {nameof(DebugRunLocalAsync)}");

            // to adequately test end-to-end processing and the current configuration,
            // use a degenerate loop-back connection for file channel messages
            var loopback = new Connection.LoopbackProxy();
            var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client);
            var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server);
            var ioProxy = new ApplicationIOProxy(appLoopbackConnection);

            string cachePath;
            string? tempCacheFolder = null;
            if (string.IsNullOrEmpty(configuration.DebugCachePath))
            {
                tempCacheFolder = Path.GetTempFileName();
                if (File.Exists(tempCacheFolder))
                {
                    File.Delete(tempCacheFolder);
                }
                Directory.CreateDirectory(tempCacheFolder);
                cachePath = tempCacheFolder;
            }
            else
            {
                cachePath = configuration.DebugCachePath;
            }

            var runnerConfig = new RunnerConfiguration
            {
                FileCachePath = Path.Combine(cachePath, "files")
            };

            var context = new ExecutionContext
            {
                Cancel = cancellationToken,
                ExecutionMode = ExecutionModes.Local,
                // in debug local mode, use the loop-back connection to ensure all IO works as expected
                // when the expression is executed remotely
                File = new RunnerFileProxy(runnerLoopbackConnection, runnerConfig),
                Directory = new RunnerDirectoryProxy(runnerLoopbackConnection, runnerConfig)
            };

            try
            {
                using (var env = new Environment
                {
                    ExecutionContext = context,
                    ResolveRemoteAssemblyAsync = new DebugRemoteAssemblyResolver(AppContext.BaseDirectory).ResolveAssembly
                })
                {
                    // TODO: should this use a proxy RunnerServer too (like the unit tests), or is that overkill?

                    // to adequately test end-to-end processing and the current configuration,
                    // serialize and deserialize the expression to simulate transmission to a remote runner
                    var data = await ExpressionSerializer.SerializeAsync(expression);
                    var func = await ExpressionSerializer.DeserializeAsync<Tprop>(data, env);

                    // run the expression with the optional configured timeout and return its result
                    var result = await Task
                        .Run(() => func.Invoke(context))
                        .TimeoutAfter(TimeSpan.FromMilliseconds(configuration.TimeoutInMs));
                    cancellationToken.ThrowIfCancellationRequested();
                    return result;
                }
            }
            finally
            {
                runnerLoopbackConnection.Dispose();
                appLoopbackConnection.Dispose();
                loopback.Dispose();

                if (Directory.Exists(tempCacheFolder))
                {
                    Directory.Delete(tempCacheFolder, true);
                }
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
            Logger.Trace($"Executing expression via {nameof(ReleaseRunLocalAsync)}");

            var context = new ExecutionContext
            {
                Cancel = cancellationToken,
                ExecutionMode = ExecutionModes.Local,
                // in release local mode, pass-through file and directory IO directly to the local file-system
                File = new RunnerFileProxy(null),
                Directory = new RunnerDirectoryProxy(null)
            };

            // when executing locally in release mode, simply compile and invoke the expression,
            // bypassing all internal (de)serialization and validation checks
            var func = expression.Compile();

            // run the expression with the optional configured timeout and return its result
            var result = await Task
                .Run(() => func.Invoke(context))
                .TimeoutAfter(TimeSpan.FromMilliseconds(configuration.TimeoutInMs));
            cancellationToken.ThrowIfCancellationRequested();
            return result;
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
        internal static async Task<ITaskHandle> DebugStartLocalAsync<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            Configuration configuration,
            CancellationToken cancellationToken)
        {
            Logger.Trace($"Executing expression via {nameof(DebugRunLocalAsync)}");

            // to adequately test end-to-end processing and the current configuration,
            // use a degenerate loop-back connection for file channel messages
            var loopback = new Connection.LoopbackProxy();
            var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client);
            var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server);
            var ioProxy = new ApplicationIOProxy(appLoopbackConnection);

            string cachePath;
            string? tempCacheFolder = null;
            if (string.IsNullOrEmpty(configuration.DebugCachePath))
            {
                tempCacheFolder = Path.GetTempFileName();
                if (File.Exists(tempCacheFolder))
                {
                    File.Delete(tempCacheFolder);
                }
                Directory.CreateDirectory(tempCacheFolder);
                cachePath = tempCacheFolder;
            }
            else
            {
                cachePath = configuration.DebugCachePath;
            }

            var runnerConfig = new RunnerConfiguration
            {
                FileCachePath = Path.Combine(cachePath, "files")
            };

            var context = new ExecutionContext
            {
                Cancel = cancellationToken,
                ExecutionMode = ExecutionModes.Local,
                // in debug local mode, use the loop-back connection to ensure all IO works as expected
                // when the expression is executed remotely
                File = new RunnerFileProxy(runnerLoopbackConnection, runnerConfig),
                Directory = new RunnerDirectoryProxy(runnerLoopbackConnection, runnerConfig)
            };

            try
            {
                using (var env = new Environment
                {
                    ExecutionContext = context,
                    ResolveRemoteAssemblyAsync = new DebugRemoteAssemblyResolver(AppContext.BaseDirectory).ResolveAssembly
                })
                {
                    // TODO: should this use a proxy RunnerServer too (like the unit tests), or is that overkill?

                    // to adequately test end-to-end processing and the current configuration,
                    // serialize and deserialize the expression to simulate transmission to a remote runner
                    var data = await ExpressionSerializer.SerializeAsync(expression);
                    var func = await ExpressionSerializer.DeserializeAsync<Tprop>(data, env);

                    //// run the expression with the optional configured timeout and return its result
                    //var result = await Task
                    //    .Run(() => func.Invoke(context))
                    //    .TimeoutAfter(TimeSpan.FromMilliseconds(configuration.TimeoutInMs));
                    cancellationToken.ThrowIfCancellationRequested();

                    throw new NotImplementedException();
                    //return new RemoteTaskHandle();
                }
            }
            finally
            {
                runnerLoopbackConnection.Dispose();
                appLoopbackConnection.Dispose();
                loopback.Dispose();

                if (Directory.Exists(tempCacheFolder))
                {
                    Directory.Delete(tempCacheFolder, true);
                }
            }
        }

        /// <summary>
        /// Optimized for release builds: perform local mode execution of the provided expression
        /// by directly compiling and invoking it.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        internal static async Task<ITaskHandle> ReleaseStartLocalAsync<Tprop>(
            Expression<Func<ExecutionContext, Tprop>> expression,
            Configuration configuration,
            CancellationToken cancellationToken)
        {
            Logger.Trace($"Executing expression via {nameof(ReleaseRunLocalAsync)}");

            var context = new ExecutionContext
            {
                Cancel = cancellationToken,
                ExecutionMode = ExecutionModes.Local,
                // in release local mode, pass-through file and directory IO directly to the local file-system
                File = new RunnerFileProxy(null),
                Directory = new RunnerDirectoryProxy(null)
            };

            // when executing locally in release mode, simply compile and invoke the expression,
            // bypassing all internal (de)serialization and validation checks
            var func = expression.Compile();

            // run the expression with the optional configured timeout and return its result
            //var result = await Task
            //    .Run(() => func.Invoke(context))
            //    .TimeoutAfter(TimeSpan.FromMilliseconds(configuration.TimeoutInMs));
            cancellationToken.ThrowIfCancellationRequested();

            throw new NotImplementedException();
            //return new RemoteTaskHandle();
        }

        internal static async Task<ITaskHandle> DebugContinueLocalAsync(
            string runnerId, string taskId,
            Configuration configuration,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal static async Task<ITaskHandle> ReleaseContinueLocalAsync(
            string runnerId, string taskId,
            Configuration configuration,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

    }
}
