using System.Reflection;
using System.Runtime.Loader;

namespace AnywhereNET
{
    /// <summary>
    /// Specifies the global static runtime configuration for an Anywhere Environment service application.
    /// </summary>
    public class Environment
    {
        // TODO: replace RemoteAssemblyResolver with an interface: Resolve() and Close()

        // TODO: should this yield a stream or byte[]?
        /// <summary>
        /// Signature for a method that resolves a provided assembly by name,
        /// returning a stream containing the assembly bytecode.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public delegate Task<Stream?> RemoteAssemblyResolver(Environment env, string assemblyName);

        /// <summary>
        /// A delegate method for resolving remote runtime assemblies used by the host application.
        /// </summary>
        public RemoteAssemblyResolver? ResolveRemoteAssemblyAsync { get; set; }

        /// <summary>
        /// An optional runtime AssemblyLoadContext which serves as an isolated container
        /// for all needed assemblies.
        /// </summary>
        public AssemblyLoadContext? AssemblyContext { get; set; }

        /// <summary>
        /// A bi-directional communications channel to the application.
        /// </summary>
        public Stream? ApplicationChannel { get; set; }

        /// <summary>
        /// The context available to the expression to access configuration and utilities.
        /// </summary>
        public ExecutionContext? ExecutionContext { get; set; }

        public Dictionary<string, Assembly> LoadedAssemblies { get; set; }
            = new Dictionary<string, Assembly>();

        // TODO: cache previously fetched assemblies to disk to avoid a network round trip
        public string AssemblyCachePath { get; set; }
    }
}