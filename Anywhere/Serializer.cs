using Newtonsoft.Json;
using System.Linq.Expressions;
using System.Reflection;

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

    public class Serializer
    {
        public static Node SerializeGeneric<TReturn>(Expression<Func<ExecutionContext, TReturn>> expression)
        {
            return SerializeOtherGeneric(expression);
        }

        public static Func<ExecutionContext, TResult> DeserializeGeneric<TResult>(Node node)
        {
            // deserialize the encoded expression tree, which by design will be a lambda expression
            var exp = DeserializeOtherGeneric(node);
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

        public class Node
        {
            public ExpressionType ExpressionType { get; set; }
        }

        public class ConstantNode : Node
        {
            public TypeNode Type { get; set; }
            public object Value { get; set; }
        }

        public class TypeNode
        {
            public TypeNode(Type type)
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
        }

        public class MethodInfoNode
        {
            public string Name { get; set; }
            public TypeNode DeclaringType { get; set; }
            public TypeNode ReturnType { get; set; }
            public MethodInfoNode(MethodInfo info)
            {
                Name = info.Name;
                DeclaringType = new TypeNode(info.DeclaringType);
                ReturnType = new TypeNode(info.ReturnType);
            }

            public MethodInfo ToInfo()
            {
                var declaringType = DeclaringType.ToType();
                return declaringType.GetMethod(Name);
            }
        }

        public class MemberInfoNode
        {
            public string Name { get; set; }
            public TypeNode DeclaringType { get; set; }

            public MemberInfoNode(MemberInfo info)
            {
                Name = info.Name;
                DeclaringType = new TypeNode(info.DeclaringType);
            }

            public MemberInfo ToInfo()
            {
                var declaringType = DeclaringType.ToType();
                return declaringType.GetMember(Name).FirstOrDefault();
            }
        }

        public class LambdaNode : Node
        {
            public Node Body { get; set; }
            public Node[] Parameters { get; set; }
            public string? Name { get; set; }
            public TypeNode ReturnType { get; set; }
        }

        public class MemberNode : Node
        {
            public Node Expression { get; set; }
            public MemberInfoNode Member { get; set; }
        }

        public class MethodCallNode : Node
        {
            public MethodInfoNode Method { get; set; }
            public Node[] Arguments { get; set; }
            // the instance object the method is called on, or null if it's a static method
            public Node Object { get; set; }
        }

        public class ParameterNode : Node
        {
            public string? Name { get; set; }
            public TypeNode Type { get; set; }
        }

        public class UnaryNode : Node
        {
            public MethodInfoNode? Method { get; set; }
            public Node Operand { get; set; }
            public TypeNode Type { get; set; }
        }

        public static Node SerializeOtherGeneric(Expression expression, Expression? parent = null)
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
                        Type = new TypeNode(exp.Type),
                        Value = exp.Value
                    };
                    // TODO: if expression is a constant, get the value
                    //object container = exp.Value;
                    //if (parent is MemberExpression)
                    //{
                    //    var memberExpression = parent as MemberExpression;
                    //    switch (memberExpression.Member)
                    //    {
                    //        case FieldInfo info:
                    //            node.Type = new TypeNode(((FieldInfo)memberExpression.Member).FieldType);
                    //            node.Value = ((FieldInfo)memberExpression.Member).GetValue(node.Value);
                    //            break;
                    //        case PropertyInfo info:
                    //            node.Type = new TypeNode(((PropertyInfo)memberExpression.Member).PropertyType);
                    //            node.Value = ((PropertyInfo)memberExpression.Member).GetValue(node.Value);
                    //            break;
                    //        default:
                    //            throw new NotImplementedException($"While converting {nameof(ConstantExpression)}: member type {memberExpression.Member.GetType()} is not supported.");
                    //    }
                    //}
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
                        Body = SerializeOtherGeneric(exp.Body, exp),
                        Parameters = exp.Parameters.Select(p => SerializeOtherGeneric(p, exp)).ToArray(),
                        Name = exp.Name,
                        ReturnType = new TypeNode(exp.ReturnType),
                    };
                case ListInitExpression exp:
                    throw new NotImplementedException();
                    break;
                case LoopExpression exp:
                    throw new NotImplementedException();
                    break;
                case MemberExpression exp:
                    // TODO: convert a closure member reference to a constant value
                    if (exp.Expression.Type.IsCompilerGeneratedType())
                    {
                        if( exp.GetConstantValue(out Type type, out object value))
                        {
                            return new ConstantNode
                            {
                                ExpressionType = ExpressionType.Constant,
                                Type = new TypeNode(type),
                                Value = value
                            };
                        }
                    }

                    return new MemberNode
                    {
                        ExpressionType = exp.NodeType,
                        Expression = SerializeOtherGeneric(exp.Expression, exp),
                        Member = new MemberInfoNode(exp.Member)
                    };
                case MemberInitExpression exp:
                    throw new NotImplementedException();
                    break;
                case MethodCallExpression exp:
                    return new MethodCallNode
                    {
                        ExpressionType = exp.NodeType,
                        Object = exp.Object != null ? SerializeOtherGeneric(exp.Object, exp) : null,
                        Arguments = exp.Arguments.Select(a => SerializeOtherGeneric(a, exp)).ToArray(),
                        Method = new MethodInfoNode(exp.Method)
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
                        Type = new TypeNode(exp.Type)
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
                        Operand = SerializeOtherGeneric(exp.Operand, exp),
                        Method = exp.Method != null ? new MethodInfoNode(exp.Method) : null,
                        Type = new TypeNode(exp.Type)
                    };
                default:
                    throw new NotSupportedException();
            }
        }

        public static Expression DeserializeOtherGeneric(Node node)
        {
            switch (node)
            {
                case ConstantNode n:
                    return Expression.Constant(n.Value, n.Type.ToType());
                case LambdaNode n:
                    var body = DeserializeOtherGeneric(n.Body);
                    var parameters = n.Parameters.Select(p => (ParameterExpression)DeserializeOtherGeneric(p)).ToArray();
                    return Expression.Lambda(body, n.Name, parameters);
                case MemberNode n:
                    return Expression.MakeMemberAccess(DeserializeOtherGeneric(n.Expression), n.Member.ToInfo());
                case MethodCallNode n:
                    return Expression.Call(DeserializeOtherGeneric(n.Object), n.Method.ToInfo(), n.Arguments.Select(a => DeserializeOtherGeneric(a)).ToArray());
                case ParameterNode n:
                    return Expression.Parameter(n.Type.ToType(), n.Name);
                case UnaryNode n:
                    return Expression.MakeUnary(n.ExpressionType, DeserializeOtherGeneric(n.Operand), n.Type.ToType());
                default:
                    throw new NotSupportedException($"Node type {node.GetType()} not yet supported.");
            }
        }

    }
}
