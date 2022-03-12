using Newtonsoft.Json;

namespace AnywhereNET
{
    /// <summary>
    /// Encapsulates a serializable model of the properties of a Type necessary to create a new instance 
    /// of the type using the Activator class.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class TypeModel
    {
        /// <summary>
        /// Create an empty new instance. 
        /// Used during deserialization.
        /// </summary>
        public TypeModel() { }

        /// <summary>
        /// Create a new instance to encapsulate the given type.
        /// </summary>
        /// <param name="type"></param>
        public TypeModel(Type type)
        {
            Name = type.FullName;
            AssemblyName = type.Assembly.FullName;
            RuntimeVersion = type.Assembly.ImageRuntimeVersion;
        }

        /// <summary>
        /// The fully qualified name of the type.
        /// </summary>
        [JsonProperty]
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// The name of the assembly containing the type.
        /// </summary>
        [JsonProperty]
        public string AssemblyName { get; set; } = String.Empty;

        /// <summary>
        /// The runtime version of the assembly containing the type.
        /// </summary>
        [JsonProperty]
        public string RuntimeVersion { get; set; } = String.Empty;
    }
}
