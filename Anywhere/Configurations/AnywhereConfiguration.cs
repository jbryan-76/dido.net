namespace AnywhereNET
{
    /// <summary>
    /// Configures an Anywhere instance.
    /// </summary>
    public class AnywhereConfiguration
    {
        /// <summary>
        /// Signature for a method that resolves a provided assembly by name,
        /// returning a stream containing the assembly bytecode, or null if 
        /// the assembly could not be resolved.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public delegate Task<Stream?> LocalAssemblyResolver(string assemblyName);

        /// <summary>
        /// 
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// The default mode that will be used for executing all expressions.
        /// </summary>
        public ExecutionModes ExecutionMode { get; set; }
            = ExecutionModes.Remote;

        /// <summary>
        /// The uri for the orchestrator service used to negotiate the specific runner service
        /// that remotely executes expressions.
        /// </summary>
        public Uri? OrchestratorUri { get; set; } = null;

        /// <summary>
        /// The uri for a dedicated runner service used to remotely execute expressions.
        /// If set, this overrides any configured orchestrator.
        /// </summary>
        public Uri? RunnerUri { get; set; } = null;

        /// <summary>
        /// A delegate method for resolving local runtime assemblies used by the host application.
        /// </summary>
        public LocalAssemblyResolver ResolveLocalAssemblyAsync { get; set; }
            = new DefaultLocalAssemblyResolver().ResolveAssembly;
    }
}
