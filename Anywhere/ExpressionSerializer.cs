using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Serialization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace AnywhereNET
{
    public static class MemberExpressionExtensions
    {
        public static bool GetConstantValue(this MemberExpression exp, out Type type, out object value)
        {
            if (!(exp.Expression is ConstantExpression))
            {
                type = null;
                value = null;
                return false;
            }
            object container = ((ConstantExpression)exp.Expression).Value;
            switch (exp.Member)
            {
                case FieldInfo info:
                    type = ((FieldInfo)exp.Member).FieldType;
                    value = ((FieldInfo)exp.Member).GetValue(container);
                    break;
                case PropertyInfo info:
                    type = ((PropertyInfo)exp.Member).PropertyType;
                    value = ((PropertyInfo)exp.Member).GetValue(container);
                    break;
                default:
                    throw new NotImplementedException($"Could not get constant value from member expression: {exp.Member.GetType()} is not supported.");
            }
            return true;
        }
    }

    public class ExpressionSerializer
    {
        internal static BindingFlags AllMembers = BindingFlags.Instance | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.Static;

        public class SerializeSettings
        {
            public enum Formats
            {
                Json,
                Bson,

                // TODO: explore serializing the node tree to a stream as optimized binary data.
                // TODO: for example, since types will probably be reused, the most compact way might be 
                // TODO: to store the types separately, then store the tree?
                // Binary 
            }

            public Formats Format { get; set; } = Formats.Json;

            public bool LeaveOpen { get; set; } = true;
        }

        public static Task SerializeAsync<TResult>(Expression<Func<ExecutionContext, TResult>> expression, Stream stream, SerializeSettings? settings = null)
        {
            var node = Encode(expression);
            Serialize(node, stream, settings);
            return Task.CompletedTask;
        }

        public static Task<Func<ExecutionContext, TResult>> DeserializeAsync<TResult>(Stream stream, Environment env, SerializeSettings? settings = null)
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
            var state = new ExpressionVisitorState();
            // deserialize the encoded expression tree, which by design will be a lambda expression
            var exp = await DecodeToExpressionAsync(node, env, state);
            var lambda = (LambdaExpression)exp;

            // the return type of the lambda expression will not necessarily match the indicated
            // generic return type, even if the types are compatible/convertable. so wrap the expression
            // in another lambda that converts to the generic type the caller is expecting.
            // if for some reason the types are not compatible, the caller will need to handle
            // the exception
            var convert = Expression.Convert(lambda.Body, typeof(TResult));
            lambda = Expression.Lambda(typeof(Func<ExecutionContext, TResult>), convert, lambda.Parameters);

            // finally, compile the lambda into an invokable function and cast it to the proper return type
            return (Func<ExecutionContext, TResult>)lambda.Compile();
        }

        //public class SerializeTypeBinder : ISerializationBinder
        //{
        //    /// <summary>
        //    /// Used during deserialization to find the type corresponding to the provided
        //    /// assembly and type.
        //    /// </summary>
        //    /// <param name="assemblyName"></param>
        //    /// <param name="typeName"></param>
        //    /// <returns></returns>
        //    public Type BindToType(string assemblyName, string typeName)
        //    {
        //        throw new NotImplementedException();
        //        //return Type.GetType(typeName);
        //        //return KnownTypes.SingleOrDefault(t => t.Name == typeName);
        //    }

        //    /// <summary>
        //    /// Used during serialization to output the specific assembly and type names for
        //    /// an object.
        //    /// </summary>
        //    /// <param name="serializedType"></param>
        //    /// <param name="assemblyName"></param>
        //    /// <param name="typeName"></param>
        //    public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        //    {
        //        // NOTE: there appears to be a bug/feature in Newtonsoft.Json where even when providing
        //        // the assembly full name, it is truncated to just the assembly name
        //        assemblyName = serializedType.Assembly.FullName;
        //        typeName = serializedType.FullName;
        //        //assemblyName = null;
        //        //typeName = serializedType.Name;
        //    }

        //}

        public class DeserializeTypeBinder : ISerializationBinder
        {
            Environment Environment;

            public DeserializeTypeBinder(Environment environment)
            {
                Environment = environment;
            }

            /// <summary>
            /// Used during deserialization to find the type corresponding to the provided
            /// assembly and type.
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
                //throw new InvalidOperationException($"Could not resolve assembly '{assemblyName}' from current Environment.");
                //return Type.GetType(typeName);
                //return KnownTypes.SingleOrDefault(t => t.Name == typeName);
            }

            /// <summary>
            /// Used during serialization to output the specific assembly and type names for
            /// an object.
            /// </summary>
            /// <param name="serializedType"></param>
            /// <param name="assemblyName"></param>
            /// <param name="typeName"></param>
            public void BindToName(Type serializedType, out string assemblyName, out string typeName)
            {
                throw new NotImplementedException();
                //assemblyName = serializedType.Assembly.FullName;
                //typeName = serializedType.FullName;
                //assemblyName = null;
                //typeName = serializedType.Name;
            }
        }

        public static void Serialize(Node node, Stream stream, SerializeSettings? settings = null)
        {
            settings = settings ?? new SerializeSettings();

            switch (settings.Format)
            {
                case SerializeSettings.Formats.Json:
                    using (var streamWriter = new StreamWriter(stream: stream, leaveOpen: settings.LeaveOpen == true))
                    using (var jsonWriter = new JsonTextWriter(streamWriter))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                        //jsonSerializer.SerializationBinder = new TypeBinder();
                        jsonSerializer.Serialize(jsonWriter, node);
                    }
                    //System.Text.Json.JsonSerializer.Serialize(streamWriter, node, new System.Text.Json.JsonSerializerOptions
                    //{

                    //});
                    break;
                case SerializeSettings.Formats.Bson:
                    using (var binaryWriter = new BinaryWriter(stream, Encoding.Default, settings.LeaveOpen == true))
                    using (var bsonWriter = new BsonDataWriter(binaryWriter))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                        //jsonSerializer.SerializationBinder = new TypeBinder();
                        jsonSerializer.Serialize(bsonWriter, node);
                    }
                    break;
                default:
                    throw new NotSupportedException($"Serialization format '{settings.Format}' unknown or unsupported.");
            }
        }

        public static Node Deserialize(Stream stream, SerializeSettings? settings = null)
        {
            // TODO: this whole thing may need to be wrapped in the exception handler to resolve assemblies
            settings = settings ?? new SerializeSettings();

            switch (settings.Format)
            {
                case SerializeSettings.Formats.Json:
                    using (var streamReader = new StreamReader(stream: stream, leaveOpen: settings.LeaveOpen == true))
                    using (var jsonReader = new JsonTextReader(streamReader))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                        //jsonSerializer.SerializationBinder = new TypeBinder();
                        // TODO: this is trying to deserialize a value in a constant node and failing
                        return jsonSerializer.Deserialize<Node>(jsonReader)!;
                    }
                case SerializeSettings.Formats.Bson:
                    using (var binaryReader = new BinaryReader(stream, Encoding.Default, settings.LeaveOpen == true))
                    using (var jsonReader = new BsonDataReader(binaryReader))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                        //jsonSerializer.SerializationBinder = new TypeBinder();
                        return jsonSerializer.Deserialize<Node>(jsonReader)!;
                    }
                default:
                    throw new NotSupportedException($"Serialization format '{settings.Format}' unknown or unsupported.");
            }
        }

        public class TypeModel
        {
            public TypeModel() { }
            public TypeModel(Type type)
            {
                Name = type.AssemblyQualifiedName;
                AssemblyName = type.Assembly.FullName;
                RuntimeVersion = type.Assembly.ImageRuntimeVersion;
            }
            public string Name { get; set; }
            public string AssemblyName { get; set; }
            public string RuntimeVersion { get; set; }

            public Type ToType()
            {
                return Type.GetType(Name, true);
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
                //else if (typeof(Type) == obj.GetType())
                //{
                //    obj = new TypeModel((Type)obj);
                //}
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

            public new MethodInfo ToInfo()
            {
                var declaringType = DeclaringType.ToType();
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

            public MemberInfo ToInfo()
            {
                var declaringType = DeclaringType.ToType();
                return declaringType.GetMember(Name, AllMembers).FirstOrDefault();
            }
        }

        public class Node
        {
            // TODO: serialize as a string instead of integer?
            public ExpressionType ExpressionType { get; set; }
        }

        public class ConstantNode : Node
        {
            public TypeModel Type { get; set; }

            [JsonIgnore]
            public Environment Environment
            {
                get { return environment; }
                set
                {
                    environment = value;
                    typeBinder = new DeserializeTypeBinder(environment);
                }
            }

            //public ConstantNode() { }

            //public ConstantNode(bool serializing)
            //{
            //    if (serializing)
            //    {
            //        //typeBinder = new SerializeTypeBinder();
            //    }
            //}

            private ISerializationBinder typeBinder;
            private Environment environment;

            [JsonIgnore]
            public object Value
            {
                get
                {
                    if (_valueObject == null)
                    {
                        using (var stream = new MemoryStream(_value))
                        using (var streamReader = new StreamReader(stream: stream, leaveOpen: true))
                        using (var jsonReader = new JsonTextReader(streamReader))
                        {
                            var jsonSerializer = new JsonSerializer();
                            jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                            jsonSerializer.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full;
                            if (typeBinder != null)
                            {
                                jsonSerializer.SerializationBinder = typeBinder;
                            }
                            // TODO: this is trying to deserialize a value in a constant node and failing
                            var foo = jsonSerializer.Deserialize(jsonReader)!;
                            var type = foo.GetType();
                            _valueObject = foo;
                            var json = System.Text.Encoding.UTF8.GetString(_value);
                            //return foo;
                        }

                        // since the value was serialized as json,
                        // if the constant value has a numeric type that differes from the deserialized
                        // type, convert it
                        if (
                            (_valueObject.GetType() == typeof(long) && Type != typeof(long)) ||
                            (_valueObject.GetType() == typeof(double) && Type != typeof(double))
                            )
                        {
                            _valueObject = Convert.ChangeType(_valueObject, Type.ToType());
                        }
                    }
                    return _valueObject;
                    //if (_valueObject == null)
                    //{
                    //    using (var stream = new MemoryStream(_value))
                    //    using (var streamReader = new StreamReader(stream: stream, leaveOpen: true))
                    //    using (var jsonReader = new JsonTextReader(streamReader))
                    //    {
                    //        var jsonSerializer = new JsonSerializer();
                    //        jsonSerializer.TypeNameHandling = TypeNameHandling.Objects;
                    //        // TODO: this is trying to deserialize a value in a constant node and failing
                    //        var foo = jsonSerializer.Deserialize(jsonReader)!;
                    //        var type = foo.GetType();
                    //        return foo;
                    //    }
                    //}
                    //return _valueObject;
                }
                set
                {
                    using (var stream = new MemoryStream())
                    {
                        using (var streamWriter = new StreamWriter(stream: stream, leaveOpen: true))
                        using (var jsonWriter = new JsonTextWriter(streamWriter))
                        {
                            var jsonSerializer = new JsonSerializer();
                            jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                            jsonSerializer.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full;
                            if (typeBinder != null)
                            {
                                jsonSerializer.SerializationBinder = typeBinder;
                            }
                            // TODO: serializing a closure object
                            jsonSerializer.Serialize(jsonWriter, value);
                        }
                        _value = stream.ToArray();
                    }
                    var json = System.Text.Encoding.UTF8.GetString(_value);
                    _valueObject = value;
                    //using (var stream = new MemoryStream())
                    //{
                    //    using (var streamWriter = new StreamWriter(stream: stream, leaveOpen: true))
                    //    using (var jsonWriter = new JsonTextWriter(streamWriter))
                    //    {
                    //        var jsonSerializer = new JsonSerializer();
                    //        jsonSerializer.TypeNameHandling = TypeNameHandling.Objects;
                    //        jsonSerializer.Serialize(jsonWriter, value);
                    //    }
                    //    _value = stream.ToArray();
                    //}
                    //_valueObject = value;
                }
            }

            [JsonProperty]
            private byte[] _value;

            [JsonIgnore]
            private object _valueObject;
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
            // the instance object the method is called on, or null if it's a static method
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

        internal class ExpressionVisitorState
        {
            public ParameterExpression? ContextExpression { get; set; }
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
                    // if the expression is actually for a constant,
                    // convert it to a concrete constant value.
                    // this is commonly needed to properly handle closures.
                    if (exp.GetConstantValue(out Type type, out object value))
                    {
                        //if (exp.Expression.Type.IsCompilerGeneratedType())
                        {
                            SchemaChecks.CheckSerializableProperties(type);
                            return new ConstantNode
                            {
                                ExpressionType = ExpressionType.Constant,
                                Type = new TypeModel(type),
                                Value = value
                            };
                        }
                    }

                    return new MemberNode
                    {
                        ExpressionType = exp.NodeType,
                        Expression = EncodeFromExpression(exp.Expression, exp),
                        Member = new MemberInfoModel(exp.Member)
                    };
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

        //internal static Expression DecodeToExpression(Node node)
        //{
        //    switch (node)
        //    {
        //        case ConstantNode n:
        //            //return Expression.Constant(n.GetValue(), n.Type.ToType());
        //            return Expression.Constant(n.Value, n.Type.ToType());
        //        case LambdaNode n:
        //            var body = DecodeToExpression(n.Body);
        //            var parameters = n.Parameters.Select(p => (ParameterExpression)DecodeToExpression(p)).ToArray();
        //            return Expression.Lambda(body, n.Name, parameters);
        //        case MemberNode n:
        //            return Expression.MakeMemberAccess(DecodeToExpression(n.Expression), n.Member.ToInfo());
        //        case MethodCallNode n:
        //            return Expression.Call(DecodeToExpression(n.Object), n.Method.ToInfo(), n.Arguments.Select(a => DecodeToExpression(a)).ToArray());
        //        case ParameterNode n:
        //            return Expression.Parameter(n.Type.ToType(), n.Name);
        //        case UnaryNode n:
        //            return Expression.MakeUnary(n.ExpressionType, DecodeToExpression(n.Operand), n.Type.ToType());
        //        default:
        //            throw new NotSupportedException($"Node type {node.GetType()} not yet supported.");
        //    }
        //}

        internal static async Task<Expression> DecodeToExpressionAsync(Node node, Environment env, ExpressionVisitorState state)
        {
            // TODO: don't try forever
            while (true)
            {
                try
                {
                    switch (node)
                    {
                        case ConstantNode n:
                            // ensure the node has the environment so it can access the 
                            // loaded assemblies while deserializing the value
                            n.Environment = env;
                            //return Expression.Constant(n.Value, n.Type.ToType());
                            // IMPORTANT: materialize the intended type before the value.
                            // otherwise the deserialization and type binding used in Constant.Value
                            // will not always resolve the correct assembly
                            var intendedType = n.Type.ToType();
                            //var actualType = n.GetValue().GetType();
                            //var sameType = actualType.Equals(intendedType);
                            var val = n.Value;// GetValue();
                            return Expression.Constant(val, intendedType);// n.Type.ToType());
                        case LambdaNode n:
                            var body = await DecodeToExpressionAsync(n.Body, env, state);
                            var parameters = n.Parameters
                                .Select(async p => (ParameterExpression)(await DecodeToExpressionAsync(p, env, state)))
                                .Select(t => t.Result)
                                .ToArray();
                            return Expression.Lambda(body, n.Name, parameters);
                        case MemberNode n:
                            return Expression.MakeMemberAccess(await DecodeToExpressionAsync(n.Expression, env, state), n.Member.ToInfo());
                        case MethodCallNode n:
                            return Expression.Call(
                                n.Object == null ? null : await DecodeToExpressionAsync(n.Object, env, state),
                                n.Method.ToInfo(),
                                n.Arguments
                                    .Select(async a => await DecodeToExpressionAsync(a, env, state))
                                    .Select(t => t.Result)
                                    .ToArray()
                                    );
                        case ParameterNode n:
                            var exp = Expression.Parameter(n.Type.ToType(), n.Name);
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
                            return Expression.MakeUnary(n.ExpressionType, await DecodeToExpressionAsync(n.Operand, env, state), n.Type.ToType());
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
                    HandleMissingAssemblyException(e.InnerException, env);
                }
                catch (Exception e)
                {
                    // TODO: don't fail forever. if reach MAX_TRIES abort with exception
                    var type = e.GetType();
                    throw;
                }
            }
        }

        internal static async void HandleMissingAssemblyException(Exception e, Environment env)
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
            // it could not be found, then something else must be going on
            // TODO: better understand whether this can ever happen, and how to handle it when it does
            if (env.LoadedAssemblies.ContainsKey(assemblyName))
            {
                throw new InvalidOperationException($"Could not resolve assembly '{assemblyName}'", e);
            }

            // assembly not found. try to resolve it
            var stream = await env.ResolveRemoteAssemblyAsync(env, assemblyName);

            if (stream == null)
            {
                throw new InvalidOperationException($"Could not resolve assembly '{assemblyName}'", e);
            }

            // TODO: this is worth pursuing for proper code isolation,
            // TODO: but JsonConvert.DeserializeObject will not be able to locate the assemblies to instantiate
            // TODO: type instances unless the assemblies are in AssemblyLoadContext.Default
            //var contextName = env.AssemblyLoadContextName ?? nameof(AssemblyLoadContext.Default);
            //var context = AssemblyLoadContext.All.FirstOrDefault(x => x.Name == contextName);
            //if( context == null)
            //{
            //    throw new InvalidOperationException($"Could not find {nameof(AssemblyLoadContext)} name '{contextName}' in which to load assembly {assemblyName}");
            //}
            //context.LoadFromStream(stream);
            var asm = AssemblyLoadContext.Default.LoadFromStream(stream);

            stream.Dispose();

            env.LoadedAssemblies.Add(assemblyName, asm);
        }
    }
}
