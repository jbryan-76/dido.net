using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Reflection;

namespace DidoNet
{
    /// <summary>
    /// A JSON serialization binder to locate the correct type from the set of loaded assemblies
    /// in a specific runtime Environment.
    /// </summary>
    internal class DeserializeTypeBinder : ISerializationBinder
    {
        private Environment Environment;

        /// <summary>
        /// Create a new binder instance for the specified Environment.
        /// </summary>
        /// <param name="environment"></param>
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
        public Type BindToType(string? assemblyName, string typeName)
        {
            if( string.IsNullOrEmpty(assemblyName) )
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }
            if (!Environment.TryGetLoadedAssembly(assemblyName, out var asm))
            {
                throw new FileNotFoundException($"Could not resolve assembly '{assemblyName}' from current Environment.", assemblyName);
            }
            var type = asm!.GetType(typeName);
            return type != null ? type : throw new TypeNotFoundException($"Could not resolve type '{typeName}' in assembly '{assemblyName}'.");
        }

        /// <summary>
        /// NOT USED: Only for serialization.
        /// </summary>
        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            throw new NotImplementedException();
        }
    }
}
