using System.Reflection;
using System.Runtime.Loader;

namespace AnywhereNET
{
    /// <summary>
    /// Specifies the global static runtime configuration for an Anywhere Environment service application.
    /// </summary>
    public class Environment
    {
        /// <summary>
        /// Signature for a method that resolves a provided assembly by name,
        /// returning a stream containing the assembly bytecode.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public delegate Task<Stream?> RemoteAssemblyResolver(Environment env, string assemblyName);

        // TODO: override this for unit testing?
        /// <summary>
        /// A delegate method for resolving remote runtime assemblies used by the host application.
        /// </summary>
        public RemoteAssemblyResolver ResolveRemoteAssemblyAsync { get; set; }
            = DefaultRemoteAssemblyResolver.ResolveAssembly;

        // TODO: the actual resolver needs to call back to the current ambient Anywhere library running in the application
        // TODO: this stream can be an SslStream for normal ops, or eg a custom loopback stream for unit testing

        /// <summary>
        /// An optional runtime AssemblyLoadContext for loading all needed assemblies.
        /// </summary>
        public AssemblyLoadContext? AssemblyContext { get; set; }

        /// <summary>
        /// A bi-directional communications channel to the application.
        /// </summary>
        public Stream? ApplicationChannel { get; set; }

        // TODO: create this when the env starts
        // TODO: make internal?
        public ExecutionContext Context { get; set; }

        public Dictionary<string, Assembly> LoadedAssemblies { get; set; }
            = new Dictionary<string, Assembly>();

        // TODO: cache previously fetched assemblies to disk to avoid a network round trip
        public string AssemblyCachePath { get; set; }
    }
}