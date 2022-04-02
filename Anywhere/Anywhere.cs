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
        public LocalAssemblyResolver? ResolveLocalAssemblyAsync = DefaultLocalAssemblyResolver.ResolveAssembly;

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

        //public string Serialize<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression)
        //{
        //    // first verify the expression is a lambda expression
        //    if (expression as LambdaExpression == null)
        //    {
        //        throw new InvalidOperationException($"{nameof(expression)} must be a lambda expression");
        //    }

        //    // convert the expression into a serializable model and serialize it
        //    var model = BuildModelFromExpression(expression);
        //    return JsonConvert.SerializeObject(model);
        //}

        public async Task<byte[]> SerializeNew<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression)
        {
            // first verify the expression is a lambda expression
            if (expression as LambdaExpression == null)
            {
                throw new InvalidOperationException($"{nameof(expression)} must be a lambda expression");
            }

            using (var stream = new MemoryStream())
            {
                await ExpressionSerializer.SerializeAsync(expression, stream);
                //return Convert.ToBase64String(stream.ToArray());
                return stream.ToArray();
            }

            //// convert the expression into a serializable model and serialize it
            //var model = BuildModelFromExpression(expression);
            //return JsonConvert.SerializeObject(model);
        }

        public Task<Func<ExecutionContext, Tprop>> DeserializeNew<Tprop>(byte[] data, Environment env)
        {
            using (var stream = new MemoryStream(data))
            {
                return ExpressionSerializer.DeserializeAsync<Tprop>(stream, env);
            }
        }

        //internal MethodModel BuildModelFromExpression<Tprop>(Expression<Func<ExecutionContext, Tprop>> expression)
        //{
        //    // https://stackoverflow.com/questions/3607464/how-to-get-the-instance-of-a-reffered-instance-from-a-lambda-expression
        //    // https://stackoverflow.com/questions/35231897/get-name-and-value-of-static-class-properties-using-expression-trees

        //    // TODO: how to serialize a lambda to inject the ExecutionContext?

        //    // The expression is a lambda expression...
        //    var lambdaExp = expression as LambdaExpression;

        //    // ...with a method call body...
        //    var methodCallExp = (MethodCallExpression)lambdaExp.Body;

        //    // ...and a set of expected parameters to the expression.
        //    var parameters = lambdaExp.Parameters;

        //    // TODO: confirm methodCallExp is not null
        //    // TODO: confirm parameters is either empty, or only contains 1 value with Type = ExecutionContext

        //    // The method call has a list of arguments.
        //    var args = new List<ValueModel>();
        //    foreach (var a in methodCallExp.Arguments)
        //    {
        //        var type = a.Type;

        //        if (a is ConstantExpression)
        //        {
        //            // argument is an expression with a constant value
        //            var exp = (ConstantExpression)a;
        //            // add an argument with the constant value
        //            args.Add(new ValueModel
        //            {
        //                Value = exp.Value,
        //                Type = new TypeModel(type)
        //            });
        //        }
        //        else if (a is MemberExpression)
        //        {
        //            // argument is an expression accessing a field or property
        //            var exp = (MemberExpression)a;
        //            var constExp = (ConstantExpression)exp.Expression;
        //            var fieldInfo = (FieldInfo)exp.Member;
        //            var obj = ((FieldInfo)exp.Member).GetValue((exp.Expression as ConstantExpression).Value);
        //            // add an argument with the member value
        //            args.Add(new ValueModel
        //            {
        //                Value = obj,
        //                Type = new TypeModel(type)
        //            });
        //        }
        //        else if (a is ParameterExpression)
        //        {
        //            var exp = (ParameterExpression)a;
        //            if (exp.Type != typeof(ExecutionContext))
        //            {
        //                throw new InvalidOperationException($"Only a single {typeof(ExecutionContext)} parameter is supported for lambda expressions");
        //            }
        //            // argument is a parameter of type ExecutionContext
        //            args.Add(new ValueModel
        //            {
        //                Value = null,
        //                Type = new TypeModel(typeof(ExecutionContext))
        //            });
        //        }
        //        else
        //        {
        //            throw new InvalidOperationException($"Unknown, unsupported, or unexpected expression type");
        //        }
        //    }

        //    // The method is called on a member of some instance.
        //    var instanceExp = methodCallExp.Object;

        //    // Start building the invoker.
        //    var invoker = new MethodModel
        //    {
        //        ReturnType = new TypeModel(methodCallExp.Method.ReturnType),
        //        Method = methodCallExp.Method,
        //        MethodName = methodCallExp.Method.Name,
        //        Arguments = args.ToArray()
        //    };

        //    // The instance expression is either null or not null.
        //    if (instanceExp == null)
        //    {
        //        // If null, then the method is a static method (and therefore not called on an actual instance).
        //        invoker.IsStatic = true;
        //        invoker.Instance = new ValueModel
        //        {
        //            Value = null,
        //            Type = new TypeModel(methodCallExp.Method.DeclaringType)
        //        };
        //    }
        //    else
        //    {
        //        // If not null, then the expression contains an instance of the class that defines the method.
        //        var unaryExp = instanceExp as UnaryExpression;
        //        var memberExp = instanceExp as MemberExpression;

        //        var constant = (ConstantExpression)memberExp.Expression;
        //        var anonymousClassInstance = constant.Value;
        //        var calledClassField = (FieldInfo)memberExp.Member;

        //        var underlyingType = calledClassField.FieldType;
        //        var instanceMethodIsCalledOn = calledClassField.GetValue(anonymousClassInstance);

        //        invoker.Instance = new ValueModel
        //        {
        //            Value = instanceMethodIsCalledOn,
        //            Type = new TypeModel(underlyingType)
        //        };
        //    }

        //    return invoker;
        //}


    }
}
