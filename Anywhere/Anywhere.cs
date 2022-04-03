using Newtonsoft.Json;
using System.Linq.Expressions;
using System.Reflection;

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
// delegate this to a wrapper layer

// app usage: var anywhere = new Anywhere(CONFIG); await anywhere.Execute( LAMBDA )

// CONFIG = local vs remote, remote endpoint, queues

// immediate execution:
// - app executes a lambda and awaits the result

// eventual execution:
// - option 1: app submits a lambda and awaits the result which is an id to poll for the status/result
// - option 2: app submits a lambda and a callback which is invoked when the result is ready

// execution sequence: 
// - use reflection to serialize the lambda expression into a data blob containing all info to execute the lambda
// - open a connection to the orchestrator
// - transmit the blob
// - deserialize the blob to an invokable method
// - try to instantiate and execute the method
// - catch exceptions for missing assemblies and request from source app as needed via connection
// - return the result to the app via connection
// in debug mode, do above regardless of local vs remote
// in release mode with local execution, invoke lambda directly and bypass all serialization

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

    // NOTE this class is to be used by the client application
    /// <summary>
    /// 
    /// </summary>
    public class Anywhere
    {
        // TODO configuration should be global and static since it applies to the entire application domain
        // TODO configuration should be transmitted to a runner and applied globally so it can properly implement remote file access
        // TODO create an Anywhere.IO to mirror System.IO at least for File and Path. readonly? or write too?

        // TODO configure remote or local. remote needs some kind of connection string
        public ExecutionModes ExecutionMode = ExecutionModes.Local;

        // TODO the connection string to the orchestrator. ip+port?
        public string ConnectionString;

        /// <summary>
        /// Signature for a method that resolves a provided assembly by name,
        /// returning a stream containing the assembly bytecode.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public delegate Task<Stream?> LocalAssemblyResolver(string assemblyName);

        /// <summary>
        /// A delegate method for resolving local runtime assemblies used by the host application.
        /// </summary>
        public LocalAssemblyResolver? ResolveLocalAssemblyAsync = new DefaultLocalAssemblyResolver().ResolveAssembly;

        public async Task<Tprop> Execute<Tprop>(Expression<Func<ExecutionContext, Tprop>> lambda)
        {
            switch (ExecutionMode)
            {
                case ExecutionModes.Local:
                    return await LocalExecute<Tprop>(lambda);
                case ExecutionModes.Remote:
                    return await RemoteExecute<Tprop>(lambda);
                default:
                    throw new InvalidOperationException($"Illegal or unknown value for '{nameof(ExecutionMode)}': {ExecutionMode}");
            }
        }

        // TODO: maybe only allow static method expressions to better enforce good usage patterns and simplify serialization
        // TODO: (since then all arguments need to be simple)?
        // TODO: support all lambda expressions? assuming we can (de)serialize
        public async Task<Tprop> LocalExecute<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression)
        {
#if (DEBUG)
            return await DebugLocalExecute(expression);
#else
            return await ReleaseLocalExecute(expression);
#endif
        }

        internal async Task<Tprop> ReleaseLocalExecute<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression)
        {
            try
            {
                // TODO: initialze this to appropriate values for local execution
                var context = new ExecutionContext
                {
                    ExecutionMode = ExecutionModes.Local,
                };
                var func = expression.Compile();
                var result = func(context);
                return result;
            }
            catch (Exception e)
            {
                // TODO: remove this once things seem stable?
                //return Task.FromException<Tprop>(e);
                throw;
            }
        }

        internal async Task<Tprop> DebugLocalExecute<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression)
        {
            try
            {
                // TODO: initialze this to appropriate values for local execution
                var context = new ExecutionContext
                {
                    ExecutionMode = ExecutionModes.Local,
                };

                var env = new Environment
                {
                    // TODO: initialze these to appropriate values for local execution
                    //ApplicationChannel
                    Context = context
                    //ResolveRemoteAssembly
                };
                //var data = Serialize(expression);
                //var result = await MethodModelDeserializer.DeserializeAndExecuteAsync<Tprop>(env, data);
                //return result;
                return default(Tprop);
            }
            catch (Exception e)
            {
                // TODO: remove this once things seem stable?
                //return Task.FromException<Tprop>(e);
                throw;
            }
        }

        // TODO: add remote execution: convenience wrapper to block until remote execution completes
        public async Task<Tprop> RemoteExecute<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression)
        {
            try
            {
                //var data = Serialize(expression);
                // TODO: open a connection to the orchestrator
                // TODO: request an available runtime destination from the orchestrator
                // TODO: receive the runtime
                // TODO: close the connection
                // TODO: open a connection to the remote runtime
                // TODO: transmit data to remote
                // TODO: handle assembly and/or file fetch requests
                // TODO: receive/poll for result
                // TODO: close the connection
                //var result = await MethodModelDeserializer.DeserializeAndExecuteAsync<Tprop>(data);
                //return result;
                throw new NotImplementedException();
            }
            catch (Exception e)
            {
                // TODO: remove this once things seem stable
                throw;
            }
        }

        /// <summary>
        /// Serialize the given lambda expression to a byte array. The resulting array
        /// can either be stored or transmitted, and later deserialized and executed.
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<byte[]> Serialize<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression)
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
        /// Deserialize a lambda expression from a byte array, using the provided environment
        /// to resolve any required assemblies and load them into the proper runtime assembly
        /// context.
        /// <para/>NOTE the byte array must be created with Serialize().
        /// </summary>
        /// <typeparam name="Tprop"></typeparam>
        /// <param name="data"></param>
        /// <param name="env"></param>
        /// <returns></returns>
        public Task<Func<ExecutionContext, Tprop>> Deserialize<Tprop>(byte[] data, Environment env)
        {
            using (var stream = new MemoryStream(data))
            {
                return ExpressionSerializer.DeserializeAsync<Tprop>(stream, env);
            }
        }
    }
}
