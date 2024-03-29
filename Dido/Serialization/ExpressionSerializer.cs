﻿using Dido.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace DidoNet
{
    internal static class ExpressionSerializer
    {
        public static async Task<byte[]> SerializeAsync<TResult>(Expression<Func<ExecutionContext, TResult>> expression, ExpressionSerializeSettings? settings = null)
        {
            using (var stream = new MemoryStream())
            {
                await SerializeAsync(expression, stream, settings);
                return stream.ToArray();
            }
        }

        public static Task SerializeAsync<TResult>(Expression<Func<ExecutionContext, TResult>> expression, Stream stream, ExpressionSerializeSettings? settings = null)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var node = Encode(expression);
            Serialize(node, stream, settings);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Deserialize an expression from a byte array, using the provided environment
        /// to resolve any required assemblies and load them into the proper runtime assembly
        /// context.
        /// <para/>NOTE the byte array must be created with SerializeAsync().
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="data"></param>
        /// <param name="env"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static Task<Func<ExecutionContext, TResult>> DeserializeAsync<TResult>(byte[] data, Environment env, ExpressionSerializeSettings? settings = null)
        {
            using (var stream = new MemoryStream(data))
            {
                return DeserializeAsync<TResult>(stream, env, settings);
            }
        }

        public static Task<Func<ExecutionContext, TResult>> DeserializeAsync<TResult>(Stream stream, Environment env, ExpressionSerializeSettings? settings = null)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            if (env == null)
            {
                throw new ArgumentNullException(nameof(env));
            }
            var node = Deserialize(stream, settings);
            return DecodeAsync<TResult>(node, env);
        }

        public static Node Encode<TResult>(Expression<Func<ExecutionContext, TResult>> expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            return EncodeFromExpression(expression);
        }

        public static async Task<Func<ExecutionContext, TResult>> DecodeAsync<TResult>(Node node, Environment env)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            if (env == null)
            {
                throw new ArgumentNullException(nameof(env));
            }

            // deserialize the encoded expression tree, which by design will be a lambda expression
            var state = new ExpressionVisitorState();
            var exp = await DecodeToExpressionAsync(node, env, state);
            var lambda = (LambdaExpression)exp;

            // the return type of the lambda expression will not necessarily match the indicated
            // generic return type, even if the types are compatible/convertible. so wrap the expression
            // in another lambda that performs a type conversion to the generic type the caller is expecting.
            // if the types are not compatible, the caller will need to handle the resulting exception.
            var convert = Expression.Convert(lambda.Body, typeof(TResult));
            lambda = Expression.Lambda(typeof(Func<ExecutionContext, TResult>), convert, lambda.Parameters);

            // finally, compile the lambda into an invocation function and cast it to the proper return type
            return (Func<ExecutionContext, TResult>)lambda.Compile();
        }

        public static void Serialize(Node node, Stream stream, ExpressionSerializeSettings? settings = null)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            settings = settings ?? new ExpressionSerializeSettings();

            switch (settings.Format)
            {
                case ExpressionSerializeSettings.Formats.Json:
                    using (var streamWriter = new StreamWriter(stream: stream, leaveOpen: settings.LeaveOpen == true))
                    using (var jsonWriter = new JsonTextWriter(streamWriter))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                        jsonSerializer.Serialize(jsonWriter, node);
                    }
                    break;
                case ExpressionSerializeSettings.Formats.Bson:
                    using (var binaryWriter = new BinaryWriter(stream, Encoding.Default, settings.LeaveOpen == true))
                    using (var bsonWriter = new BsonDataWriter(binaryWriter))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                        jsonSerializer.Serialize(bsonWriter, node);
                    }
                    break;
                // TODO: case ExpressionSerializeSettings.Formats.Binary?
                default:
                    throw new NotSupportedException($"Serialization format '{settings.Format}' unknown or unsupported.");
            }
        }

        public static Node Deserialize(Stream stream, ExpressionSerializeSettings? settings = null)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            settings = settings ?? new ExpressionSerializeSettings();

            switch (settings.Format)
            {
                case ExpressionSerializeSettings.Formats.Json:
                    using (var streamReader = new StreamReader(stream: stream, leaveOpen: settings.LeaveOpen == true))
                    using (var jsonReader = new JsonTextReader(streamReader))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                        return jsonSerializer.Deserialize<Node>(jsonReader)!;
                    }
                case ExpressionSerializeSettings.Formats.Bson:
                    using (var binaryReader = new BinaryReader(stream, Encoding.Default, settings.LeaveOpen == true))
                    using (var jsonReader = new BsonDataReader(binaryReader))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                        return jsonSerializer.Deserialize<Node>(jsonReader)!;
                    }
                // TODO: case ExpressionSerializeSettings.Formats.Binary?
                default:
                    throw new NotSupportedException($"Serialization format '{settings.Format}' unknown or unsupported.");
            }
        }

        internal static Node EncodeFromExpression(Expression expression, Expression? parent = null)
        {
            while (expression.CanReduce)
            {
                expression = expression.Reduce();
            }

            switch (expression)
            {
                case BinaryExpression exp:
                    return new BinaryNode
                    {
                        ExpressionType = exp.NodeType,
                        Conversion = exp.Conversion != null ? EncodeFromExpression(exp.Conversion, exp) : null,
                        Left = EncodeFromExpression(exp.Left, exp),
                        LiftToNull = exp.IsLiftedToNull,
                        Right = EncodeFromExpression(exp.Right, exp),
                        Method = exp.Method != null ? new MethodInfoModel(exp.Method) : null
                    };
                case BlockExpression exp:
                    throw new NotImplementedException();
                case ConditionalExpression exp:
                    throw new NotImplementedException();
                case ConstantExpression exp:
                    TypeChecks.CheckSerializableProperties(exp.Type);
                    return new ConstantNode
                    {
                        ExpressionType = exp.NodeType,
                        Type = new TypeModel(exp.Type),
                        Value = exp.Value!
                    };
                case DebugInfoExpression exp:
                    throw new NotImplementedException();
                case DefaultExpression exp:
                    throw new NotImplementedException();
                case DynamicExpression exp:
                    throw new NotImplementedException();
                case GotoExpression exp:
                    throw new NotImplementedException();
                case IndexExpression exp:
                    throw new NotImplementedException();
                case InvocationExpression exp:
                    throw new NotImplementedException();
                case LabelExpression exp:
                    throw new NotImplementedException();
                case LambdaExpression exp:
                    return new LambdaNode
                    {
                        ExpressionType = exp.NodeType,
                        Body = EncodeFromExpression(exp.Body, exp),
                        Parameters = exp.Parameters.Select(p => EncodeFromExpression(p, exp)).ToArray(),
                        Name = exp.Name,
                        ReturnType = new TypeModel(exp.ReturnType),
                    };
                case ListInitExpression exp:
                    throw new NotImplementedException();
                case LoopExpression exp:
                    throw new NotImplementedException();
                case MemberExpression exp:
                    // if the expression is actually for a constant value,
                    // convert it to a constant node instead.
                    // this is commonly needed to properly handle closures.
                    if (exp.GetConstantValue(out Type type, out object value))
                    {
                        TypeChecks.CheckSerializableProperties(type);
                        return new ConstantNode
                        {
                            ExpressionType = ExpressionType.Constant,
                            Type = new TypeModel(type),
                            Value = value
                        };
                    }
                    else
                    {
                        return new MemberNode
                        {
                            ExpressionType = exp.NodeType,
                            Expression = exp.Expression != null ? EncodeFromExpression(exp.Expression, exp) : null,
                            Member = new MemberInfoModel(exp.Member)
                        };
                    }
                case MemberInitExpression exp:
                    throw new NotImplementedException();
                case MethodCallExpression exp:
                    return new MethodCallNode
                    {
                        ExpressionType = exp.NodeType,
                        Object = exp.Object != null ? EncodeFromExpression(exp.Object, exp) : null,
                        Arguments = exp.Arguments.Select(a => EncodeFromExpression(a, exp)).ToArray(),
                        Method = new MethodInfoModel(exp.Method)
                    };
                case NewArrayExpression exp:
                    throw new NotImplementedException();
                case NewExpression exp:
                    throw new NotImplementedException();
                case ParameterExpression exp:
                    // TODO: throw if a ref or out parameter is used
                    return new ParameterNode
                    {
                        ExpressionType = exp.NodeType,
                        Name = exp.Name,
                        Type = new TypeModel(exp.Type)
                    };
                case RuntimeVariablesExpression exp:
                    throw new NotImplementedException();
                case SwitchExpression exp:
                    throw new NotImplementedException();
                case TryExpression exp:
                    throw new NotImplementedException();
                case TypeBinaryExpression exp:
                    throw new NotImplementedException();
                case UnaryExpression exp:
                    return new UnaryNode
                    {
                        ExpressionType = exp.NodeType,
                        Operand = EncodeFromExpression(exp.Operand, exp),
                        Method = exp.Method != null ? new MethodInfoModel(exp.Method) : null,
                        Type = new TypeModel(exp.Type)
                    };
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Handles processing when a needed assembly could not be found during deserialization or invocation of
        /// an expression.
        /// <para/>Note: the AssemblyLoadContext.Resolving event is also used, but does not handle all cases, so 
        /// additional core logic is needed to explicitly catch and process exceptions involving missing assemblies.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="env"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="MissingAssemblyException"></exception>
        internal static void HandleMissingAssemblyException(Exception e, Environment env)
        {
            // determine the missing assembly name from the exception
            string assemblyName = string.Empty;
            if (e is FileNotFoundException notFoundException)
            {
                assemblyName = notFoundException.FileName!;
            }
            else if (e is FileLoadException loadException)
            {
                assemblyName = loadException.FileName!;
            }
            else
            {
                throw new MissingAssemblyException($"Cannot handle missing assembly from exception {e.GetType()}. See inner exception for details.", e);
            }

            // this conditional should never evaluate to true, but keep it here to prevent
            // potential infinite loops: if the correct assembly exists in Environment.LoadedAssemblies,
            // that means it was successfully loaded already, but if an exception was thrown indicating
            // it could not be found, then something else must be going on.
            // TODO: better understand whether this can ever happen, and how to handle it when it does
            //if (env.LoadedAssemblies.ContainsKey(assemblyName))
            if (env.IsAssemblyLoaded(assemblyName))
            {
                throw new MissingAssemblyException($"Could not resolve assembly '{assemblyName}'. See inner exception for details.", e);
            }

            var asm = env.ResolveAssembly(assemblyName);
            if (asm == null)
            {
                throw new MissingAssemblyException($"Could not resolve assembly '{assemblyName}'", e);
            }
        }

        private static async Task<Expression> DecodeToExpressionAsync(Node node, Environment env, ExpressionVisitorState state)
        {
            // TODO: don't try forever
            while (true)
            {
                try
                {
                    switch (node)
                    {
                        case BinaryNode n:
                            return Expression.MakeBinary(n.ExpressionType,
                                await DecodeToExpressionAsync(n.Left, env, state),
                                await DecodeToExpressionAsync(n.Right, env, state),
                                n.LiftToNull,
                                n.Method?.ToInfo(env));
                        case ConstantNode n:
                            // ensure the constant node has the current environment so it can access the 
                            // loaded assemblies while deserializing the value
                            n.Environment = env;
                            // IMPORTANT: materialize the intended type explicitly first before the value.
                            // doing so allows any missing/unloaded types to be properly resolved using the
                            // exception handling code flow which requests the proper corresponding assembly.
                            // otherwise the deserialization and type binding used in Constant.Value
                            // may incorrectly bind to an already loaded assembly in the default context.
                            var intendedType = n.Type.ToType(env)!;
                            return Expression.Constant(n.Value, intendedType);
                        case LambdaNode n:
                            var body = await DecodeToExpressionAsync(n.Body, env, state);
                            var parameters = n.Parameters
                                .Select(async p => (ParameterExpression)(await DecodeToExpressionAsync(p, env, state)))
                                .Select(t => t.Result)
                                .ToArray();
                            return Expression.Lambda(body, n.Name, parameters);
                        case MemberNode n:
                            return Expression.MakeMemberAccess(
                                n.Expression != null ? await DecodeToExpressionAsync(n.Expression, env, state) : null,
                                n.Member!.ToInfo(env));
                        case MethodCallNode n:
                            return Expression.Call(
                                n.Object == null ? null : await DecodeToExpressionAsync(n.Object, env, state),
                                n.Method!.ToInfo(env),
                                n.Arguments
                                    .Select(async a => await DecodeToExpressionAsync(a, env, state))
                                    .Select(t => t.Result)
                                    .ToArray()
                                    );
                        case ParameterNode n:
                            var exp = Expression.Parameter(n.Type.ToType(env)!, n.Name);
                            // by design, the only supported expressions are lambdas with a single ExecutionContext parameter.
                            // as soon as a parameter with this type is encountered, remember its ParameterExpression
                            // so it can be reused/substituted throughout the rest of the lambda body expressions.
                            if (exp.Type == typeof(ExecutionContext))
                            {
                                if (state.ContextExpression == null)
                                {
                                    state.ContextExpression = exp;
                                }
                                return state.ContextExpression;
                            }
                            else
                            {
                                return exp;
                            }
                        case UnaryNode n:
                            return Expression.MakeUnary(n.ExpressionType, await DecodeToExpressionAsync(n.Operand, env, state), n.Type.ToType(env)!);
                        default:
                            throw new NotSupportedException($"Node type {node.GetType()} not yet supported.");
                    }
                }
                catch (Exception e) when (e is FileLoadException || e is FileNotFoundException)
                {
                    HandleMissingAssemblyException(e, env);
                }
                catch (JsonSerializationException e)
                {
                    HandleMissingAssemblyException(e.InnerException!, env);
                }
                catch (Exception)
                {
                    // TODO: don't fail forever. if reach MAX_TRIES abort with exception
                    //var type = e.GetType();
                    throw;
                }
            }
        }

        /// <summary>
        /// Maintains state needed while recursively visiting an expression node tree.
        /// </summary>
        private class ExpressionVisitorState
        {
            /// <summary>
            /// The single instance of the ExecutionContext referenced
            /// throughout an encoded lambda expression. This is needed
            /// to ensure all references to the ExecutionContext parameter use
            /// the same instance.
            /// </summary>
            public ParameterExpression? ContextExpression { get; set; }
        }
    }
}
