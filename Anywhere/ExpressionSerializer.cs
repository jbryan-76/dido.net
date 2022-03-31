using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using System.Text;

namespace AnywhereNET
{

    public static class TypeExtensions
    {
        public static bool IsCompilerGeneratedType(this Type type)
        {
            return type.FullName.Contains("<>");
        }
    }

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
                    throw new NotImplementedException($"Cound not get constant value from member expression: {exp.Member.GetType()} is not supported.");
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


            Environment Environment;
        }

        public static void Serialize<TResult>(Expression<Func<ExecutionContext, TResult>> expression, Stream stream, SerializeSettings? settings = null)
        {
            var node = Encode(expression);
            Serialize(node, stream, settings);
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
            // deserialize the encoded expression tree, which by design will be a lambda expression
            var exp = await DecodeToExpressionAsync(node, env);
            var lambda = (LambdaExpression)exp;

            // the return type of the lambda expression will not necessarily match the desired
            // type, even if the types are compatible/convertable. so wrap the expression in
            // another lambda that converts to the generic type the caller is expecting.
            // if for some reason the types are not compatible, the caller will need to handle
            // the exception
            var convert = Expression.Convert(lambda.Body, typeof(TResult));
            lambda = Expression.Lambda(typeof(Func<ExecutionContext, TResult>), convert, lambda.Parameters);

            // finally compile the lambda into an invokable function and cast it to the proper return type
            return (Func<ExecutionContext, TResult>)lambda.Compile();
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
                        jsonSerializer.TypeNameHandling = TypeNameHandling.Objects;
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
                        jsonSerializer.TypeNameHandling = TypeNameHandling.Objects;
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
                        jsonSerializer.TypeNameHandling = TypeNameHandling.Objects;
                        // TODO: this is trying to deserialize a value in a constant node and failing
                        return jsonSerializer.Deserialize<Node>(jsonReader)!;
                    }
                case SerializeSettings.Formats.Bson:
                    using (var binaryReader = new BinaryReader(stream, Encoding.Default, settings.LeaveOpen == true))
                    using (var jsonReader = new BsonDataReader(binaryReader))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.Objects;
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

            //[JsonIgnore]
            //// TODO: may need to serialize this value if it's a complex object?
            //public object Value
            //{
            //    get
            //    {
            //        //if (_valueObject == null)
            //        {
            //            using (var stream = new MemoryStream(_value))
            //            using (var streamReader = new StreamReader(stream: stream, leaveOpen: true))
            //            using (var jsonReader = new JsonTextReader(streamReader))
            //            {
            //                var jsonSerializer = new JsonSerializer();
            //                jsonSerializer.TypeNameHandling = TypeNameHandling.Objects;
            //                // TODO: this is trying to deserialize a value in a constant node and failing
            //                var foo = jsonSerializer.Deserialize(jsonReader)!;
            //                var type = foo.GetType();
            //                return foo;
            //            }
            //        }
            //        //return _valueObject;
            //    }
            //    set
            //    {
            //        using (var stream = new MemoryStream())
            //        {
            //            using (var streamWriter = new StreamWriter(stream: stream, leaveOpen: true))
            //            using (var jsonWriter = new JsonTextWriter(streamWriter))
            //            {
            //                var jsonSerializer = new JsonSerializer();
            //                jsonSerializer.TypeNameHandling = TypeNameHandling.Objects;
            //                jsonSerializer.Serialize(jsonWriter, value);
            //            }
            //            _value = stream.ToArray();
            //        }
            //        //_valueObject = value;
            //    }
            //}

            public void SetValue(object value)
            {
                using (var stream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(stream: stream, leaveOpen: true))
                    using (var jsonWriter = new JsonTextWriter(streamWriter))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.Objects;
                        jsonSerializer.Serialize(jsonWriter, value);
                    }
                    _value = stream.ToArray();
                }
                _valueObject = value;
            }

            public object GetValue()
            {
                if (_valueObject == null)
                {
                    using (var stream = new MemoryStream(_value))
                    using (var streamReader = new StreamReader(stream: stream, leaveOpen: true))
                    using (var jsonReader = new JsonTextReader(streamReader))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.Objects;
                        // TODO: this is trying to deserialize a value in a constant node and failing
                        var foo = jsonSerializer.Deserialize(jsonReader)!;
                        var type = foo.GetType();
                        _valueObject = foo;
                        //return foo;
                    }
                }
                return _valueObject;
            }

            [JsonProperty]
            private byte[] _value;

            [JsonIgnore]
            private object _valueObject;

            //public void SetValue(object obj)
            //{
            //    using (var stream = new MemoryStream())
            //    {
            //        var serializer = new BinaryFormatter();
            //        serializer.Serialize(stream, obj);
            //    }
            //}

            //public object GetValue()
            //{

            //}

            /// <summary>
            /// Some serializers (eg json) will deserialize values to the "largest"
            /// compatible type (eg any integer is deserialized to long/Int64).
            /// This method is automatically invoked after deserializing the object
            /// and is used to convert values back to the proper type, if necessary.
            /// </summary>
            /// <param name="context"></param>
            [OnDeserialized]
            internal void OnDeserializedMethod(StreamingContext context)
            {
                if (_value != null)
                {
                    //var currentType = Value.GetType();
                    //if( currentType == typeof(Int64)
                    //&& Type != typeof(Int64))
                    if (Type == typeof(byte)
                        || Type == typeof(short)
                        || Type == typeof(ushort)
                        || Type == typeof(int)
                        || Type == typeof(uint)
                        || Type == typeof(long)
                        || Type == typeof(ulong)
                        || Type == typeof(float)
                        || Type == typeof(double)
                    )
                    //if (intendedType != currentType && intendedType != typeof(object))
                    {
                        var intendedType = Type.ToType();
                        //_valueObject = Convert.ChangeType(Value, intendedType);
                        //Value = Convert.ChangeType(Value, intendedType);
                        SetValue(Convert.ChangeType(GetValue(), intendedType));
                    }
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
                    var node = new ConstantNode
                    {
                        ExpressionType = exp.NodeType,
                        Type = new TypeModel(exp.Type),
                        //Value = exp.Value
                    };
                    node.SetValue(exp.Value);
                    return node;
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
                    // convert a closure member reference to a constant value
                    if (exp.Expression.Type.IsCompilerGeneratedType())
                    {
                        if (exp.GetConstantValue(out Type type, out object value))
                        {
                            var foo = new ConstantNode
                            {
                                ExpressionType = ExpressionType.Constant,
                                Type = new TypeModel(type),
                                //Value = value
                            };
                            foo.SetValue(value);
                            return foo;
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

        internal static Expression DecodeToExpression(Node node)
        {
            switch (node)
            {
                case ConstantNode n:
                    return Expression.Constant(n.GetValue(), n.Type.ToType());
                    //return Expression.Constant(n.Value, n.Type.ToType());
                case LambdaNode n:
                    var body = DecodeToExpression(n.Body);
                    var parameters = n.Parameters.Select(p => (ParameterExpression)DecodeToExpression(p)).ToArray();
                    return Expression.Lambda(body, n.Name, parameters);
                case MemberNode n:
                    return Expression.MakeMemberAccess(DecodeToExpression(n.Expression), n.Member.ToInfo());
                case MethodCallNode n:
                    return Expression.Call(DecodeToExpression(n.Object), n.Method.ToInfo(), n.Arguments.Select(a => DecodeToExpression(a)).ToArray());
                case ParameterNode n:
                    return Expression.Parameter(n.Type.ToType(), n.Name);
                case UnaryNode n:
                    return Expression.MakeUnary(n.ExpressionType, DecodeToExpression(n.Operand), n.Type.ToType());
                default:
                    throw new NotSupportedException($"Node type {node.GetType()} not yet supported.");
            }
        }

        internal static async Task<Expression> DecodeToExpressionAsync(Node node, Environment env)
        {
            // TODO: don't try forever
            while (true)
            {
                try
                {
                    switch (node)
                    {
                        case ConstantNode n:
                            //return Expression.Constant(n.Value, n.Type.ToType());
                            // materialize the intended type first otherwise the deserialization
                            // in GetValue will use the wrong assembly!?
                            var intendedType = n.Type.ToType();
                            var actualType = n.GetValue().GetType();
                            var sameType = actualType.Equals(intendedType);
                            var val = n.GetValue();
                            return Expression.Constant(val, intendedType);// n.Type.ToType());
                        case LambdaNode n:
                            var body = await DecodeToExpressionAsync(n.Body, env);
                            var parameters = n.Parameters
                                .Select(async p => (ParameterExpression)(await DecodeToExpressionAsync(p, env)))
                                .Select(t => t.Result)
                                .ToArray();
                            return Expression.Lambda(body, n.Name, parameters);
                        case MemberNode n:
                            return Expression.MakeMemberAccess(await DecodeToExpressionAsync(n.Expression, env), n.Member.ToInfo());
                        case MethodCallNode n:
                            return Expression.Call(await DecodeToExpressionAsync(n.Object, env),
                                n.Method.ToInfo(),
                                n.Arguments
                                    .Select(async a => await DecodeToExpressionAsync(a, env))
                                    .Select(t => t.Result)
                                    .ToArray()
                                    );
                        case ParameterNode n:
                            return Expression.Parameter(n.Type.ToType(), n.Name);
                        case UnaryNode n:
                            return Expression.MakeUnary(n.ExpressionType, await DecodeToExpressionAsync(n.Operand, env), n.Type.ToType());
                        default:
                            throw new NotSupportedException($"Node type {node.GetType()} not yet supported.");
                    }
                }
                catch (Exception e) when (e is FileLoadException || e is FileNotFoundException)
                {
                    string assemblyName = "";
                    if (e is FileNotFoundException)
                    {
                        assemblyName = (e as FileNotFoundException).FileName;
                    }
                    else
                    {
                        assemblyName = (e as FileLoadException).FileName;
                    }

                    // TODO: first try to load the assembly from cache

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
                catch (Exception e)
                {
                    // TODO: don't fail forever. if reach MAX_TRIES abort with exception
                    var type = e.GetType();
                    throw;
                }
            }
        }

    }
}
