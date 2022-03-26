using Newtonsoft.Json;
using System.Reflection;
using System.Runtime.Loader;

namespace AnywhereNET
{
    public class MethodModelDeserializer
    {
        // TODO: add helper to execute code and dynamically resolve assemblies?

        // TODO: add "context" to allow caching assemblies associated with a particular app
        public static async Task<MethodModel?> DeserializeAsync(Environment env, string data)
        {
            var triedAssemblies = new HashSet<string>();

            MethodModel? model = null;
            bool created = false;
            while (!created)
            {
                try
                {
                    // deserialize to the strongly typed model to start trying to load the assemblies needed
                    // to execute the encoded lambda
                    model = JsonConvert.DeserializeObject<MethodModel>(data);

                    if (model == null)
                    {
                        return model;
                    }

                    // TODO: explore using AssemblyDependencyResolver to load assemblies instead of doing through try-catch

                    // get the instance assembly and type
                    var assembly = Assembly.Load(model.Instance.Type.AssemblyName);
                    var type = assembly.GetType(model.Instance.Type.Name, true);

                    // instantiate the target object instance if necessary
                    // (ie the instance that the method will operate on)
                    if (!model.IsStatic)
                    {
                        model.Instance.Value = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(model.Instance.Value), type);
                    }
                    // TODO: should the then-current static state of the instance be (de)serialized too?

                    // find the method
                    model.Method = type.GetMethod(model.MethodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                    // TODO: throw if can't find the method

                    // instantiate the arguments
                    foreach (var arg in model.Arguments)
                    {
                        assembly = Assembly.Load(arg.Type.AssemblyName);
                        type = assembly.GetType(arg.Type.Name, true);

                        // if the argument type is ExecutionContext, substitute the current environment context instance
                        if (type == typeof(ExecutionContext))
                        {
                            arg.Value = env.Context;
                        }
                        else
                        {
                            arg.Value = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(arg.Value), type);
                        }
                    }

                    // make sure the return type is available too
                    assembly = Assembly.Load(model.ReturnType.AssemblyName);
                    type = assembly.GetType(model.ReturnType.Name, true);

                    created = true;
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

                    if (triedAssemblies.Contains(assemblyName))
                    {
                        throw new InvalidOperationException($"Could not resolve assembly '{assemblyName}'", e);
                    }

                    // assembly not found. try to resolve it
                    var stream = await env.ResolveRemoteAssemblyAsync(env, assemblyName);

                    if (stream == null)
                    {
                        throw new InvalidOperationException($"Could not resolve assembly '{assemblyName}'", e);
                    }

                    AssemblyLoadContext.Default.LoadFromStream(stream);

                    stream.Dispose();

                    triedAssemblies.Add(assemblyName);
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

        public static async Task<T> DeserializeAndExecuteAsync<T>(Environment env, string data)
        {
            var method = await DeserializeAsync(env, data);
            if (method == null)
            {
                throw new InvalidOperationException("Could not deserialize");
            }
            // TODO: catch other dynamic assembly exceptions?
            var result = method.Invoke();
            return (T)result;
        }

    }
}