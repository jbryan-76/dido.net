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
        public RemoteAssemblyResolver ResolveRemoteAssemblyAsync = DefaultRemoteAssemblyResolver.ResolveAssembly;

        // TODO: the actual resolver needs to call back to the current ambient Anywhere library running in the application
        // TODO: this stream can be an SslStream for normal ops, or eg a custom loopback stream for unit testing

        // TODO: this is worth pursuing for proper code isolation,
        // TODO: but JsonConvert.DeserializeObject will not be able to locate the assemblies to instantiate
        // TODO: type instances unless the assemblies are in AssemblyLoadContext.Default
        public string? AssemblyLoadContextName;

        /// <summary>
        /// A bi-directional communications channel to the application.
        /// </summary>
        public Stream? ApplicationChannel;

        // TODO: create this when the env starts
        // TODO: make internal?
        public ExecutionContext Context;
    }
}