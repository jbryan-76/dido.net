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
        /// Use a value &lt;= 0 to retry forever.
        /// </summary>
        public int MaxTries { get; set; } = 3;

        /// <summary>
        /// How long (in milliseconds) to wait before cancelling a task
        /// and throwing a TimeoutException.
        /// </summary>
        public int TimeoutInMs { get; set; } = Timeout.Infinite;

        /// <summary>
        /// The default mode that will be used for executing tasks when using Run() or RunAsync().
        /// </summary>
        public ExecutionModes ExecutionMode { get; set; }
            = ExecutionModes.Local;

        /// <summary>
        /// The uri for the mediator service used to negotiate the specific runner service
        /// that remotely executes tasks.
        /// </summary>
        public Uri? MediatorUri { get; set; } = null;

        /// <summary>
        /// The uri for a dedicated runner service used to remotely execute tasks.
        /// If set, any configured mediator will not be used.
        /// </summary>
        public Uri? RunnerUri { get; set; } = null;

        /// <summary>
        /// A delegate method for resolving local runtime assemblies used by the host application.
        /// </summary>
        public LocalAssemblyResolver ResolveLocalAssemblyAsync { get; set; }
            = new DefaultLocalAssemblyResolver().ResolveAssembly;

        /// <summary>
        /// The validation policy for authenticating the remote server certificate for SSL connections.
        /// </summary>
        public ServerCertificateValidationPolicies ServerValidationPolicy { get; set; } = ServerCertificateValidationPolicies.RootCA;

        /// <summary>
        /// For ServerCertificateValidationPolicies.Thumbprint, the specific certificate thumbprint to validate against.
        /// </summary>
        public string ServerCertificateThumbprint { get; set; } = String.Empty;

        // TODO: provide an api to create custom MessageChannels so the application can optionally support interprocess communication
        //public MessageChannel MessageChannel { get; internal set; }
    }
}
