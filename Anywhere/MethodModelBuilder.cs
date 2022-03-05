﻿using Newtonsoft.Json;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;

namespace Anywhere
{
    public class MethodModelBuilder
    {
        //static MethodModelBuilder()
        //{
        //    AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        //}

        //private static Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
        //{
        //    throw new NotImplementedException();
        //}

        public static string Serialize<Tprop>(Expression<Func<Tprop>> expression)
        {
            // convert the expression into a serializable model and serialize it
            var model = BuildModelFromExpression(expression);
            return JsonConvert.SerializeObject(model);
        }

        public static MethodModel BuildModelFromExpression<Tprop>(Expression<Func<Tprop>> expression)
        {
            // https://stackoverflow.com/questions/3607464/how-to-get-the-instance-of-a-reffered-instance-from-a-lambda-expression
            // https://stackoverflow.com/questions/35231897/get-name-and-value-of-static-class-properties-using-expression-trees

            // The expression is a lambda expression...
            var lambdaExp = (LambdaExpression)expression;

            // ...with a method call body.
            var methodCallExp = (MethodCallExpression)lambdaExp.Body;

            // The method call has a list of arguments.
            var args = new List<ArgumentModel>();
            foreach (var a in methodCallExp.Arguments)
            {
                var type = a.Type;
                if (a is ConstantExpression)
                {
                    // argument is an expression with a constant value
                    var exp = a as ConstantExpression;
                    args.Add(new ArgumentModel
                    {
                        Value = exp.Value,
                        Type = new TypeModel(type)
                    });
                }
                else if (a is MemberExpression)
                {
                    // argument is an expression accessing a field or property
                    var exp = a as MemberExpression;
                    var constExp = (ConstantExpression)exp.Expression;
                    var fieldInfo = (FieldInfo)exp.Member;
                    var obj = ((FieldInfo)exp.Member).GetValue((exp.Expression as ConstantExpression).Value);
                    args.Add(new ArgumentModel
                    {
                        Value = obj,
                        Type = new TypeModel(type)
                    });
                }
            }

            // The method is called on a member of some instance.
            var memberExp = (MemberExpression)methodCallExp.Object;

            // With some return type.
            var retType = methodCallExp.Method.ReturnType;

            // Start building the invoker.
            var invoker = new MethodModel
            {
                ReturnType = new TypeModel(retType),
                Method = methodCallExp.Method,
                MethodName = methodCallExp.Method.Name,
                Arguments = args.ToArray()
            };

            // The member expression is either null or not null.
            if (memberExp == null)
            {
                // If null, then the method is a static method (and therefore not called on an actual instance).
                invoker.IsStatic = true;
                invoker.Instance = new ArgumentModel
                {
                    Value = null,
                    Type = new TypeModel(methodCallExp.Method.DeclaringType)
                };
            }
            else
            {
                // If not null, then the member contains an instance of the class that defines the method.
                var constant = (ConstantExpression)memberExp.Expression;
                var anonymousClassInstance = constant.Value;
                var calledClassField = (FieldInfo)memberExp.Member;

                var underlyingType = calledClassField.FieldType;
                var instanceMethodIsCalledOn = calledClassField.GetValue(anonymousClassInstance);

                invoker.Instance = new ArgumentModel
                {
                    Value = instanceMethodIsCalledOn,
                    Type = new TypeModel(underlyingType)
                };
            }

            return invoker;
        }

        public delegate Task<Stream?> AssemblyResolver(string assemblyName);

        // TODO: add helper to execute code and dynamically resolve assemblies?

        // TODO: add "context" to allow caching assemblies associated with a particular app
        public static async Task<MethodModel?> DeserializeAsync(string data, AssemblyResolver assemblyResolver)
        {
            var triedAssemblies = new HashSet<string>();

            MethodModel? model = null;
            bool created = false;
            while (!created)
            {
                // deserialize to the strongly typed model to start trying to load the assemblies needed
                // to execute the encoded lambda
                model = JsonConvert.DeserializeObject<MethodModel>(data);

                if (model == null)
                {
                    return model;
                }

                try
                {
                    var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();

                    // get the instance assembly and type
                    var assembly = Assembly.Load(model.Instance.Type.AssemblyName);
                    var type = assembly.GetType(model.Instance.Type.Name, true);

                    // instantiate the instance if necessary
                    if (!model.IsStatic)
                    {
                        model.Instance.Value = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(model.Instance.Value), type);
                    }
                    // TODO: should the then-current static state of the instance be (de)serialized too?

                    // find the method
                    model.Method = type.GetMethod(model.MethodName);

                    // instantiate the arguments
                    foreach (var arg in model.Arguments)
                    {
                        var currentType = arg.Value.GetType();
                        assembly = Assembly.Load(arg.Type.AssemblyName);
                        type = assembly.GetType(arg.Type.Name, true);
                        arg.Value = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(arg.Value), type);
                    }

                    // make sure the return type is available too
                    assembly = Assembly.Load(model.ReturnType.AssemblyName);
                    type = assembly.GetType(model.ReturnType.Name, true);

                    created = true;
                }
                catch (FileNotFoundException e)
                {
                    var assemblyName = e.FileName;

                    // TODO: first try to load the assembly from cache

                    if (triedAssemblies.Contains(assemblyName))
                    {
                        throw new InvalidOperationException($"Could not resolve assembly '{assemblyName}'", e);
                    }

                    // assembly not found. try to resolve it
                    var stream = await assemblyResolver(assemblyName);

                    if (stream == null)
                    {
                        throw new InvalidOperationException($"Could not resolve assembly '{assemblyName}'", e);
                    }

                    AssemblyLoadContext.Default.LoadFromStream(stream);
                    //using (var memstr = new MemoryStream())
                    //{
                    //    stream.CopyTo(memstr);
                    //    var assembly = Assembly.Load(memstr.ToArray());
                    //}

                    stream.Dispose();

                    triedAssemblies.Add(assemblyName);

                    var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                }
                catch (Exception e)
                {
                    // TODO: don't fail forever. if reach MAX_TRIES abort with exception
                    var type = e.GetType();
                    throw;
                }
            }

            return model;

        }

        public static MethodModel Deserialize(string data)
        {
            MethodModel model = null;
            bool created = false;
            while (!created)
            {
                try
                {
                    model = JsonConvert.DeserializeObject<MethodModel>(data);

                    // get the instance assembly and type
                    var assembly = Assembly.Load(model.Instance.Type.AssemblyName);
                    var type = assembly.GetType(model.Instance.Type.Name, true);

                    // instantiate the instance if necessary
                    if (!model.IsStatic)
                    {
                        model.Instance.Value = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(model.Instance.Value), type);
                    }

                    // find the method
                    model.Method = type.GetMethod(model.MethodName);

                    // instantiate the arguments
                    foreach (var arg in model.Arguments)
                    {
                        var currentType = arg.Value.GetType();
                        assembly = Assembly.Load(arg.Type.AssemblyName);
                        type = assembly.GetType(arg.Type.Name, true);
                        arg.Value = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(arg.Value), type);
                    }

                    // make sure the return type is available too
                    assembly = Assembly.Load(model.ReturnType.AssemblyName);
                    type = assembly.GetType(model.ReturnType.Name, true);

                    created = true;
                }
                catch (Exception e)
                {
                    // TODO: if assembly not found, fetch and cache it and try again
                    var type = e.GetType();
                    // FileNotFoundException
                    // TODO: don't fail forever. if reach MAX_TRIES abort with exception
                }
            }

            return model;
        }

        public static T DeserializeAndExecute<T>(string data)
        {
            var method = Deserialize(data);
            var result = method.Invoke();
            return (T)result;
        }
    }

}
