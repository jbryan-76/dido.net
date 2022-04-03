using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace AnywhereNET
{
    public class ExpressionSerializer
    {
        /// <summary>
        /// Binding flags to filter all members of a class: instance, public, non-public, and static.
        /// </summary>
        internal static BindingFlags AllMembers = BindingFlags.Instance | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.Static;


        public static Task SerializeAsync<TResult>(Expression<Func<ExecutionContext, TResult>> expression, Stream stream, ExpressionSerializeSettings? settings = null)
        {
            var node = Encode(expression);
            Serialize(node, stream, settings);
            return Task.CompletedTask;
        }

        public static Task<Func<ExecutionContext, TResult>> DeserializeAsync<TResult>(Stream stream, Environment env, ExpressionSerializeSettings? settings = null)
        {
            var node = Deserialize(stream, settings);
            return DecodeAsync<TResult>(node, env);
        }

        public static Node Encode<TResult>(Expression<Func<ExecutionContext, TResult>> expression)
        {
            return EncodeFromExpression(expression);
        }

        public static async Task<Func<ExecutionContext, TResult>> DecodeAsync<TResult>(Node node, Environment env)
        {
            // deserialize the encoded expression tree, which by design will be a lambda expression
            var state = new ExpressionVisitorState();
            var exp = await DecodeToExpressionAsync(node, env, state);
            var lambda = (LambdaExpression)exp;

            // the return type of the lambda expression will not necessarily match the indicated
            // generic return type, even if the types are compatible/convertable. so wrap the expression
            // in another lambda that performs a type conversion to the generic type the caller is expecting.
            // if the types are not compatible, the caller will need to handle the resulting exception.
            var convert = Expression.Convert(lambda.Body, typeof(TResult));
            lambda = Expression.Lambda(typeof(Func<ExecutionContext, TResult>), convert, lambda.Parameters);

            // finally, compile the lambda into an invokable function and cast it to the proper return type
            return (Func<ExecutionContext, TResult>)lambda.Compile();
        }

        public static void Serialize(Node node, Stream stream, ExpressionSerializeSettings? settings = null)
        {
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

        public class TypeModel
        {
            public TypeModel() { }
            public TypeModel(Type type)
            {
                Name = type.FullName;
                AssemblyName = type.Assembly.FullName;
                RuntimeVersion = type.Assembly.ImageRuntimeVersion;
            }
            public string Name { get; set; }
            public string AssemblyName { get; set; }
            public string RuntimeVersion { get; set; }

            public Type ToType(Environment env)
            {
                if (env.LoadedAssemblies.TryGetValue(AssemblyName, out Assembly asm))
                {
                    return asm.GetType(Name);
                }
                throw new FileNotFoundException($"Could not resolve assembly '{AssemblyName}' from current Environment.", AssemblyName);
            }

            public override bool Equals(object? obj)
            {
                if (obj is null)
                {
                    return false;
                }
                else if (Object.ReferenceEquals(this, obj))
                {
                    return true;
                }
                if (obj.GetType() != typeof(TypeModel))
                {
                    return false;
                }
                else
                {
                    var t = (TypeModel)obj;
                    return Name == t.Name && AssemblyName == t.AssemblyName && RuntimeVersion == t.RuntimeVersion;
                }
            }

            public static bool operator ==(TypeModel lhs, Type rhs)
            {
                if (lhs is null)
                {
                    return rhs is null ? true : false;
                }
                else
                {
                    return lhs.Equals(new TypeModel(rhs));
                }
            }

            public static bool operator !=(TypeModel lhs, Type rhs) => !(lhs == rhs);
        }

        public class MethodInfoModel : MemberInfoModel
        {
            public TypeModel ReturnType { get; set; }
            public MethodInfoModel() { }
            public MethodInfoModel(MethodInfo info) : base(info)
            {
                ReturnType = new TypeModel(info.ReturnType);
            }

            public new MethodInfo ToInfo(Environment env)
            {
                var declaringType = DeclaringType.ToType(env);
                return declaringType.GetMethod(Name, AllMembers);
            }
        }

        public class MemberInfoModel
        {
            public string Name { get; set; }
            public TypeModel DeclaringType { get; set; }

            public MemberInfoModel() { }
            public MemberInfoModel(MemberInfo info)
            {
                Name = info.Name;
                DeclaringType = new TypeModel(info.DeclaringType);
            }

            public MemberInfo ToInfo(Environment env)
            {
                var declaringType = DeclaringType.ToType(env);
                return declaringType.GetMember(Name, AllMembers).FirstOrDefault();
            }
        }

        public class Node
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public ExpressionType ExpressionType { get; set; }
        }

        public class ConstantNode : Node
        {
            private ISerializationBinder TypeBinder;

            private Environment _environment;

            [JsonProperty]
            private byte[] EncodedValue;

            private object CachedValue;

            public TypeModel Type { get; set; }

            [JsonIgnore]
            public Environment Environment
            {
                get { return _environment; }
                set
                {
                    _environment = value;
                    TypeBinder = new DeserializeTypeBinder(_environment);
                }
            }

            [JsonIgnore]
            public object Value
            {
                get
                {
                    if (CachedValue == null)
                    {
                        // deserialize the value from the encoded byte array
                        using (var stream = new MemoryStream(EncodedValue))
                        using (var streamReader = new StreamReader(stream: stream, leaveOpen: true))
                        using (var jsonReader = new JsonTextReader(streamReader))
                        {
                            var jsonSerializer = new JsonSerializer();
                            jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                            jsonSerializer.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full;
                            if (TypeBinder != null)
                            {
                                jsonSerializer.SerializationBinder = TypeBinder;
                            }
                            CachedValue = jsonSerializer.Deserialize(jsonReader)!;
                        }

                        // since the value was serialized as json, if it is numeric and its
                        // type differs from the intended deserialized type, convert it
                        if (
                            (CachedValue.GetType() == typeof(long) && Type != typeof(long)) ||
                            (CachedValue.GetType() == typeof(double) && Type != typeof(double))
                            )
                        {
                            CachedValue = Convert.ChangeType(CachedValue, Type.ToType(_environment));
                        }
                    }
                    return CachedValue;
                }
                set
                {
                    // serialize the value to an encoded byte array
                    using (var stream = new MemoryStream())
                    {
                        using (var streamWriter = new StreamWriter(stream: stream, leaveOpen: true))
                        using (var jsonWriter = new JsonTextWriter(streamWriter))
                        {
                            var jsonSerializer = new JsonSerializer();
                            jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                            jsonSerializer.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full;
                            if (TypeBinder != null)
                            {
                                jsonSerializer.SerializationBinder = TypeBinder;
                            }
                            jsonSerializer.Serialize(jsonWriter, value);
                        }
                        EncodedValue = stream.ToArray();
                    }
                    CachedValue = value;
                }
            }
        }

        public class LambdaNode : Node
        {
            public Node Body { get; set; }
            public Node[] Parameters { get; set; }
            public string? Name { get; set; }
            public TypeModel ReturnType { get; set; }
        }

        public class MemberNode : Node
        {
            public Node Expression { get; set; }
            public MemberInfoModel Member { get; set; }
        }

        public class MethodCallNode : Node
        {
            public MethodInfoModel Method { get; set; }
            public Node[] Arguments { get; set; }

            /// <summary>
            /// The instance object the method is called on, or null if it's a static method.
            /// </summary>
            public Node Object { get; set; }
        }

        public class ParameterNode : Node
        {
            public string? Name { get; set; }
            public TypeModel Type { get; set; }
        }

        public class UnaryNode : Node
        {
            public MethodInfoModel? Method { get; set; }
            public Node Operand { get; set; }
            public TypeModel Type { get; set; }
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
                    throw new NotImplementedException();
                    break;
                case BlockExpression exp:
                    throw new NotImplementedException();
                    break;
                case ConditionalExpression exp:
                    throw new NotImplementedException();
                    break;
                case ConstantExpression exp:
                    SchemaChecks.CheckSerializableProperties(exp.Type);
                    return new ConstantNode
                    {
                        ExpressionType = exp.NodeType,
                        Type = new TypeModel(exp.Type),
                        Value = exp.Value
                    };
                case DebugInfoExpression exp:
                    throw new NotImplementedException();
                    break;
                case DefaultExpression exp:
                    throw new NotImplementedException();
                    break;
                case DynamicExpression exp:
                    throw new NotImplementedException();
                    break;
                case GotoExpression exp:
                    throw new NotImplementedException();
                    break;
                case IndexExpression exp:
                    throw new NotImplementedException();
                    break;
                case InvocationExpression exp:
                    throw new NotImplementedException();
                    break;
                case LabelExpression exp:
                    throw new NotImplementedException();
                    break;
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
                    break;
                case LoopExpression exp:
                    throw new NotImplementedException();
                    break;
                case MemberExpression exp:
                    // if the expression is actually for a constant value,
                    // convert it to a constant node instead.
                    // this is commonly needed to properly handle closures.
                    if (exp.GetConstantValue(out Type type, out object value))
                    {
                        SchemaChecks.CheckSerializableProperties(type);
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
                            Expression = EncodeFromExpression(exp.Expression, exp),
                            Member = new MemberInfoModel(exp.Member)
                        };
                    }
                case MemberInitExpression exp:
                    throw new NotImplementedException();
                    break;
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
                    break;
                case NewExpression exp:
                    throw new NotImplementedException();
                    break;
                case ParameterExpression exp:
                    return new ParameterNode
                    {
                        ExpressionType = exp.NodeType,
                        Name = exp.Name,
                        Type = new TypeModel(exp.Type)
                    };
                case RuntimeVariablesExpression exp:
                    throw new NotImplementedException();
                    break;
                case SwitchExpression exp:
                    throw new NotImplementedException();
                    break;
                case TryExpression exp:
                    throw new NotImplementedException();
                    break;
                case TypeBinaryExpression exp:
                    throw new NotImplementedException();
                    break;
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

        private static async Task<Expression> DecodeToExpressionAsync(Node node, Environment env, ExpressionVisitorState state)
        {
            // TODO: don't try forever
            while (true)
            {
                try
                {
                    switch (node)
                    {
                        case ConstantNode n:
                            // ensure the constant node has the current environment so it can access the 
                            // loaded assemblies while deserializing the value
                            n.Environment = env;
                            // IMPORTANT: materialize the intended type explicitly first before the value.
                            // doing so allows any missing/unloaded types to be properly resolved using the
                            // exception handling code flow which requests the proper corresponding assembly.
                            // otherwise the deserialization and type binding used in Constant.Value
                            // may incorrectly bind to an already loaded assembly in the default context.
                            var intendedType = n.Type.ToType(env);
                            return Expression.Constant(n.Value, intendedType);
                        case LambdaNode n:
                            var body = await DecodeToExpressionAsync(n.Body, env, state);
                            var parameters = n.Parameters
                                .Select(async p => (ParameterExpression)(await DecodeToExpressionAsync(p, env, state)))
                                .Select(t => t.Result)
                                .ToArray();
                            return Expression.Lambda(body, n.Name, parameters);
                        case MemberNode n:
                            return Expression.MakeMemberAccess(await DecodeToExpressionAsync(n.Expression, env, state), n.Member.ToInfo(env));
                        case MethodCallNode n:
                            return Expression.Call(
                                n.Object == null ? null : await DecodeToExpressionAsync(n.Object, env, state),
                                n.Method.ToInfo(env),
                                n.Arguments
                                    .Select(async a => await DecodeToExpressionAsync(a, env, state))
                                    .Select(t => t.Result)
                                    .ToArray()
                                    );
                        case ParameterNode n:
                            var exp = Expression.Parameter(n.Type.ToType(env), n.Name);
                            // by design, the only supported expressions are lambdas with a single ExecutionContext parameter.
                            // as soon as a parameter with this type is encountered, remember its ParameterExpression
                            // so it can be reused/substituted throuhgout the rest of the lambda body expressions.
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
                            return Expression.MakeUnary(n.ExpressionType, await DecodeToExpressionAsync(n.Operand, env, state), n.Type.ToType(env));
                        default:
                            throw new NotSupportedException($"Node type {node.GetType()} not yet supported.");
                    }
                }
                catch (Exception e) when (e is FileLoadException || e is FileNotFoundException)
                {
                    await HandleMissingAssemblyException(e, env);
                }
                catch (JsonSerializationException e)
                {
                    await HandleMissingAssemblyException(e.InnerException, env);
                }
                catch (Exception e)
                {
                    // TODO: don't fail forever. if reach MAX_TRIES abort with exception
                    var type = e.GetType();
                    throw;
                }
            }
        }

        internal static async Task HandleMissingAssemblyException(Exception e, Environment env)
        {
            string assemblyName = "";
            if (e is FileNotFoundException)
            {
                assemblyName = (e as FileNotFoundException).FileName;
            }
            else if (e is FileLoadException)
            {
                assemblyName = (e as FileLoadException).FileName;
            }
            else
            {
                throw new InvalidOperationException($"Cannot handle missing assembly from exception {e.GetType()}", e);
            }

            // TODO: first try to load the assembly from a disk cache using Environment.AssemblyCachePath

            // this conditional should never evaluate to true, but keep it here to prevent
            // potential infinite loops: if the correct assembly exists in Environment.LoadedAssemblies,
            // that means it was successfully loaded already, but if an exception was thrown indicating
            // it could not be found, then something else must be going on.
            // TODO: better understand whether this can ever happen, and how to handle it when it does
            if (env.LoadedAssemblies.ContainsKey(assemblyName))
            {
                throw new InvalidOperationException($"Could not resolve assembly '{assemblyName}'", e);
            }

            // check if the assembly is already loaded into the default context
            // (this will be common eg for standard .NET assemblies, eg System)
            var asm = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(asm => asm.FullName == assemblyName);
            if (asm != null)
            {
                env.LoadedAssemblies.Add(assemblyName, asm);
                return;
            }

            // assembly not found. try to resolve it from the remote host
            var stream = await env.ResolveRemoteAssemblyAsync(env, assemblyName);
            if (stream == null)
            {
                throw new InvalidOperationException($"Could not resolve assembly '{assemblyName}'", e);
            }

            // load the assembly into the environment context and cache its reference
            var context = env.AssemblyContext ?? AssemblyLoadContext.Default;
            asm = context.LoadFromStream(stream);
            env.LoadedAssemblies.Add(assemblyName, asm);

            // cleanup
            stream.Dispose();
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

        /// <summary>
        /// A serialization binder to locate the correct type from the set of loaded assemblies
        /// in a specific runtime Environment.
        /// </summary>
        private class DeserializeTypeBinder : ISerializationBinder
        {
            private Environment Environment;

            public DeserializeTypeBinder(Environment environment)
            {
                Environment = environment;
            }

            /// <summary>
            /// Used during deserialization to find the proper Type corresponding to the provided
            /// assembly and type name.
            /// </summary>
            /// <param name="assemblyName"></param>
            /// <param name="typeName"></param>
            /// <returns></returns>
            public Type BindToType(string assemblyName, string typeName)
            {
                if (Environment.LoadedAssemblies.TryGetValue(assemblyName, out Assembly asm))
                {
                    return asm.GetType(typeName);
                }
                throw new FileNotFoundException($"Could not resolve assembly '{assemblyName}' from current Environment.", assemblyName);
            }

            /// <summary>
            /// NOT USED: Only for serialization.
            /// </summary>
            public void BindToName(Type serializedType, out string assemblyName, out string typeName)
            {
                throw new NotImplementedException();
            }
        }
    }
}
