namespace DidoNet
{
    /// <summary>
    /// Configures the execution of a task.
    /// </summary>
    public class Configuration
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
        /// The maximum number of attempts to make when executing a remote task.
        /// </summary>
        public int MaxTries { get; set; } = 3;

        /// <summary>
        /// How long (in milliseconds) to wait before cancelling a task
        /// and throwing a TimeoutException.
        /// </summary>
        public int TimeoutInMs { get; set; } = Timeout.Infinite;

        /// <summary>
        /// The default mode that will be used for executing tasks.
        /// </summary>
        public ExecutionModes ExecutionMode { get; set; }
            = ExecutionModes.Local;

        /// <summary>
        /// The uri for the orchestrator service used to negotiate the specific runner service
        /// that remotely executes tasks.
        /// </summary>
        public Uri? OrchestratorUri { get; set; } = null;

        /// <summary>
        /// The uri for a dedicated runner service used to remotely execute tasks.
        /// If set, any configured orchestrator will not be used.
        /// </summary>
        public Uri? RunnerUri { get; set; } = null;

        //public CancellationTokenSource CancellationTokenSource { get; private set; } = new CancellationTokenSource();

        /// <summary>
        /// A delegate method for resolving local runtime assemblies used by the host application.
        /// </summary>
        public LocalAssemblyResolver ResolveLocalAssemblyAsync { get; set; }
            = new DefaultLocalAssemblyResolver().ResolveAssembly;
    }
}
