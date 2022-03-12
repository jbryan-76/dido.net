using Newtonsoft.Json;
using System.Reflection;

namespace AnywhereNET
{
    /// <summary>
    /// Encapsulates a serializable model of a method call expression, including enough detail to instantiate 
    /// and invoke it in its original context, including any object it is called on, any arguments
    /// to the method, and any return type.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class MethodModel
    {
        /// <summary>
        /// Indicates whether the method is a static method (true) or a member method (false).
        /// </summary>
        [JsonProperty]
        public bool IsStatic { get; set; }

        /// <summary>
        /// The method name.
        /// </summary>
        [JsonProperty]
        public string MethodName { get; set; }

        /// <summary>
        /// For non-static (ie member) methods, the value of the object the method was called on.
        /// </summary>
        [JsonProperty]
        public ValueModel Instance { get; set; }

        /// <summary>
        /// The arguments passed to the method.
        /// </summary>
        [JsonProperty]
        public ValueModel[] Arguments { get; set; }

        /// <summary>
        /// The return type of the method.
        /// </summary>
        [JsonProperty]
        public TypeModel ReturnType { get; set; }

        /// <summary>
        /// The non-serialized, run-time metadata representation of the method which can be invoked.
        /// </summary>
        public MethodInfo Method { get; set; }

        // TODO: add AssemblyResolver assemblyResolver
        /// <summary>
        /// Invokes the run-time method represented by this instance and returning its result.
        /// </summary>
        /// <returns></returns>
        public object Invoke()
        {
            // TODO: turn this into an async? based on the result, either return the task or wrap into a Task?
            var result = Method.Invoke(Instance.Value, Arguments.Select(a => a.Value).ToArray());
            return result;
        }
    }
}