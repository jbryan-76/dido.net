using System.Linq.Expressions;
using System.Net.Sockets;
using System.Runtime.Loader;

// TODO: need a better name than "Anywhere"

// generate self-signed certs automatically
// https://stackoverflow.com/questions/695802/using-ssl-and-sslstream-for-peer-to-peer-authentication

// Write app code normally, subject to the following constraints when code is executed with Anywhere:
// - arguments to called method must be bi-directionally serializable
// - return type of method must be bi-directionally serializable
// - if method is a member method, calling instance must be bi-directionally serializable
// - otherwise method is a static method and is not called on an instance

// Anywhere (lib): models, public API, enums, callbacks, local execution, delegates
// Anywhere.Env: an execution environment to run code
// Anywhere.Orch: an orchestrator to distribute work among environments
// Anywhere.Test: unit tests

// comm between lib and env: network api? message queue?
// delegate this to a wrapper layer?

// app usage: var anywhere = new Anywhere(CONFIG); await anywhere.Execute( LAMBDA )

// CONFIG = local vs remote, remote endpoint, queues

// immediate execution:
// - app executes a lambda and awaits the result

// eventual execution:
// - option 1: app submits a lambda and a callback which is invoked when the result is ready
// - option 2: app submits a lambda and awaits the result which is an id to poll for the status/result

// GOTCHAS
// - loading local files: maybe use anywhere overloads for IO namespace?
// - using interop or OS-specific calls: allow but have to specify proper env for runtime?
// - explicitly loading other assemblies: overloads?

// app delegates configuration:
// - persistence: where/how to store in-progress "jobs"
// - communications: how the lib talks to the orch and env

// testing:
// - project 1: fake lib with models and methods to do work.
// - project 2: fake app. import 

// COMMUNICATION = SERVER:
// load/create a cert
// start a TcpListener on a port
// infinite loop to wait for client connections
// physical connection is a TcpClient
// each time a client connects, spin off a thread to process it
// process thread:
// create an SslStream from the client
// sslStream.AuthenticateAsServer using the cert
// start infinite loop in separate thread to listen for incoming data
// send data from app thread
// close stream/connection on exception or client disconnect

// COMMUNICATION = CLIENT:
// physical connection is a TcpClient
// connect the client to the server on the port
// create an SslStream for the client and validate the server cert
// sslStream.AuthenticateAsClient
// start infinite loop in separate thread to listen for incoming data
// send data from app thread


// 1) generate a cert
// openssl req -newkey rsa:2048 -new -nodes -keyout test.key -x509 -days 365 -out test.pem
// 2) convert to a pkcs12 pfx
// openssl pkcs12 -export -out cert.pfx -inkey test.key -in test.pem -password pass:1234

namespace AnywhereNET
{
    /// <summary>
    /// Provides support for executing expressions locally for debugging, or remotely for
    /// distributed processing.
    /// </summary>
    public class Anywhere
    {
        /// <summary>
        /// Signature for a method that handles the result of asynchronous
        /// execution of an expression.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="result"></param>
        public delegate void ResultHandler<T>(T result);

        // TODO some of the configuration should be transmitted to the runner and applied globally so it
        // can properly implement remote file access.
        // TODO create an Anywhere.IO to mirror System.IO at least for File and Path. readonly? or write too?

        /// <summary>
        /// The configuration for this instance.
        /// </summary>
        private AnywhereConfiguration Configuration;

        /// <summary>
        /// Create a new instance to execute expressions.
        /// </summary>
        /// <param name="configuration"></param>
        public Anywhere(AnywhereConfiguration? configuration = null)
        {
            Configuration = configuration ?? new AnywhereConfiguration();
        }

        /// <summary>
        /// Execute the provided expression and return its result.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression">The expression to execute.</param>
        /// <param name="executionMode">An optional execution mode to override the currently configured mode.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public Task<Tprop> ExecuteAsync<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression, ExecutionModes? executionMode = null)
        {
            executionMode = executionMode ?? Configuration.ExecutionMode;
            switch (executionMode)
            {
                case ExecutionModes.Local:
                    return LocalExecuteAsync<Tprop>(expression);
                case ExecutionModes.Remote:
                    return RemoteExecuteAsync<Tprop>(expression);
                default:
                    return Task.FromException<Tprop>(new InvalidOperationException($"Illegal or unknown value for '{nameof(Configuration.ExecutionMode)}': {Configuration.ExecutionMode}"));
            }
        }

        /// <summary>
        /// Execute the provided expression and invoke the provided handler with the result.
        /// Use this form when the expression is expected to take a long time to complete.
        /// <para/>NOTE the provided handler is run in a separate thread.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression">The expression to execute.</param>
        /// <param name="resultHandler">A result handler invoked after the expression completes. 
        /// Note this handler runs in a separate thread.</param>
        /// <param name="executionMode">An optional execution mode to override the currently configured mode.</param>
        public void Execute<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression, ResultHandler<Tprop> resultHandler, ExecutionModes? executionMode = null)
        {
            //var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(async () =>
            {
                // TODO: how to handle exceptions?
                var result = await ExecuteAsync(expression, executionMode);
                resultHandler(result);
            });
            thread.Start();
        }

        /// <summary>
        /// Execute the provided expression locally (ie using the current application domain
        /// and environment).
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        public Task<Tprop> LocalExecuteAsync<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression)
        {
#if (DEBUG)
            return DebugLocalExecuteAsync(expression);
#else
            return ReleaseLocalExecute(expression);
#endif
        }

        // TODO: if the orchestrator is in job mode, poll for the result?
        // TODO: if the orchestrator is in job mode, start a background thread to call a handler when the result is available?

        /// <summary>
        /// Execute the provided expression in a remote environment.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<Tprop> RemoteExecuteAsync<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression)
        {
            if (Configuration.OrchestratorUri == null && Configuration.RunnerUri == null)
            {
                throw new InvalidOperationException($"Configuration error: At least one of {nameof(Configuration.OrchestratorUri)} or {nameof(Configuration.RunnerUri)} must be set to a valid value.");
            }

            try
            {
                //var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                var runnerUri = Configuration.RunnerUri;
                if (Configuration.OrchestratorUri != null && Configuration.RunnerUri == null)
                {
                    // TODO: open a connection to the orchestrator
                    // TODO: request an available runner from the orchestrator
                    // TODO: receive the runner connection details
                    // TODO: close the connection
                    runnerUri = Configuration.OrchestratorUri;
                }

                // create a secure connection to the remote runner
                var client = new TcpClient(runnerUri!.Host, runnerUri.Port);
                using (var connection = new Connection(client, runnerUri.Host))
                {
                    // create channels for: expression/result, assemblies, files
                    var expressionChannel = connection.GetChannel(Constants.ExpressionChannel);
                    var assembliesChannel = connection.GetChannel(Constants.AssembliesChannel);
                    var filesChannel = connection.GetChannel(Constants.FilesChannel);

                    // TODO: handle file IO on files channel

                    // handle assembly fetch requests
                    assembliesChannel.BlockingReads = true;
                    assembliesChannel.OnDataAvailable += async (channel) =>
                    {
                        // receive the assembly request
                        var request = new AssemblyRequestMessage();
                        request.Read(channel);

                        if (string.IsNullOrEmpty(request.AssemblyName))
                        {
                            var response = new AssemblyResponseMessage(new ArgumentNullException(nameof(AssemblyRequestMessage.AssemblyName)));
                            response.Write(channel);
                            return;
                        }

                        // resolve the desired assembly and send it back
                        using (var stream = await Configuration.ResolveLocalAssemblyAsync(request.AssemblyName))
                        using (var mem = new MemoryStream())
                        {
                            AssemblyResponseMessage response;
                            if (stream == null)
                            {
                                response = new AssemblyResponseMessage(new FileNotFoundException($"Assembly '{request.AssemblyName}' could not be resolved."));
                            }
                            else
                            {
                                stream.CopyTo(mem);
                                response = new AssemblyResponseMessage(mem.ToArray());
                            }
                            response.Write(channel);
                        }
                    };

                    using (var stream = new MemoryStream())
                    {
                        // serialize the expression and transmit to the remote runner
                        await ExpressionSerializer.SerializeAsync(expression, stream);
                        var expressionRequest = new ExpressionRequestMessage(stream.ToArray());
                        expressionRequest.Write(expressionChannel);
                    }

                    // block until the result is received
                    // TODO: add support for a timeout
                    // TODO: add support for a cancellation token
                    expressionChannel.BlockingReads = true;
                    var result = new ExpressionResponseMessage();
                    result.Read(expressionChannel);

                    // cleanup the connection
                    connection.Dispose();

                    // yield the result
                    return (Tprop)Convert.ChangeType(result.Result, typeof(Tprop));
                }
            }
            catch (Exception e)
            {
                // TODO: remove this once things seem stable
                throw;
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
        public async Task<byte[]> SerializeAsync<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression)
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
        /// <para/>NOTE the byte array must be created with Serialize().
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="data"></param>
        /// <param name="env"></param>
        /// <returns></returns>
        public Task<Func<ExecutionContext, Tprop>> DeserializeAsync<Tprop>(byte[] data, Environment env)
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
        internal async Task<Tprop> DebugLocalExecuteAsync<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression)
        {
            var context = new ExecutionContext
            {
                ExecutionMode = ExecutionModes.Local,
            };

            var env = new Environment
            {
                ExecutionContext = context,
                AssemblyContext = new AssemblyLoadContext(Guid.NewGuid().ToString(), true),
                ResolveRemoteAssemblyAsync = new DebugRemoteAssemblyResolver(AppContext.BaseDirectory).ResolveAssembly
            };

            var data = await SerializeAsync(expression);
            var lambda = await DeserializeAsync<Tprop>(data, env);
            var result = lambda.Invoke(context);

            env.AssemblyContext.Unload();

            return result;
        }

        /// <summary>
        /// Optimized for release builds: perform local mode execution of the provided expression
        /// by directly compiling and invoking it.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        internal Task<Tprop> ReleaseLocalExecuteAsync<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression)
        {
            var context = new ExecutionContext
            {
                ExecutionMode = ExecutionModes.Local,
            };

            // when executing locally in release mode, simply compile and invoke the expression,
            // bypassing all internal (de)serialization and validation checks
            var func = expression.Compile();
            return Task.FromResult(func.Invoke(context));
        }
    }
}
