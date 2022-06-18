using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
        /// The unique id of the application.
        /// This is used exclusively to help manage file caching on a runner.
        /// If not provided, a default id derived from the executing application's name and version
        /// is used.
        /// </summary>
        public string Id { get; set; } = GetDefaultApplicationId();

        /// <summary>
        /// The maximum number of attempts to make when executing a remote task.
        /// Use a value &lt;= 0 to retry forever.
        /// </summary>
        public int MaxTries { get; set; } = 3;

        /// <summary>
        /// How long (in milliseconds) to wait before canceling a task
        /// and throwing a TimeoutException.
        /// </summary>
        public int TimeoutInMs { get; set; } = Timeout.Infinite;

        /// <summary>
        /// The default mode that will be used for executing tasks when using Run() or RunAsync().
        /// <para/>NOTE: if neither MediatorUri nor RunnerUri are defined, the execution mode will default to Local.
        /// </summary>
        public ExecutionModes ExecutionMode { get; set; } = ExecutionModes.Remote;

        /// <summary>
        /// The uri for the mediator service used to select the best available specific runner service
        /// to remotely execute a task.
        /// </summary>
        public Uri? MediatorUri { get; set; } = null;

        /// <summary>
        /// The uri for a dedicated runner service used to remotely execute tasks.
        /// If set, any configured mediator will not be used.
        /// </summary>
        public Uri? RunnerUri { get; set; } = null;

        /// <summary>
        /// When a mediator is used to select the best available runner, this option filters all
        /// available runners by a specific set of allowed operating systems.
        /// When provided, the task can only run on a runner where the set intersection
        /// with the runner's platform is non-empty.
        /// </summary>
        public OSPlatforms[] RunnerOSPlatforms { get; set; } = new OSPlatforms[0];

        /// <summary>
        /// When a mediator is used to select the best available runner, this option filters all
        /// available runners by a specific label.
        /// When provided, the task can only run on a runner with a matching label.
        /// </summary>
        public string RunnerLabel { get; set; } = string.Empty;

        /// <summary>
        /// When a mediator is used to select the best available runner, this option filters all
        /// available runners by a specific set of tags.
        /// When provided, the task can only run on a runner where the set intersection
        /// with the runner's tags is non-empty.
        /// </summary>
        public string[] RunnerTags { get; set; } = new string[0];

        /// <summary>
        /// A delegate method for resolving local runtime assemblies used by the host application.
        /// </summary>
        public LocalAssemblyResolver ResolveLocalAssemblyAsync { get; set; }
            = new DefaultLocalAssemblyResolver().ResolveAssembly;

        /// <summary>
        /// The validation policy for authenticating the remote server certificate for SSL connections.
        /// </summary>
        public ServerCertificateValidationPolicies ServerCertificateValidationPolicy { get; set; } = ServerCertificateValidationPolicies.RootCA;

        /// <summary>
        /// For ServerCertificateValidationPolicies.Thumbprint, the specific certificate thumbprint to validate against.
        /// </summary>
        public string ServerCertificateThumbprint { get; set; } = string.Empty;

        // TODO: provide an api to create custom MessageChannels so the application can optionally support interprocess communication
        //public MessageChannel MessageChannel { get; internal set; }

        /// <summary>
        /// The assembly caching policy the runner should use while executing a task.
        /// </summary>
        public AssemblyCachingPolicies AssemblyCaching { get; set; } = AssemblyCachingPolicies.Auto;

        /// <summary>
        /// The optional encryption key to use when caching assemblies on a remote runner.
        /// </summary>
        public string CachedAssemblyEncryptionKey { get; set; } = string.Empty;

        /// <summary>
        /// A local file-system path used in debug mode to cache proxied files requested by a remotely executing task.
        /// </summary>
        public string DebugCachePath { get; set; } = string.Empty;

        /// <summary>
        /// The maximum time to wait when communicating with the mediator before throwing a TimeoutException.
        /// </summary>
        public TimeSpan MediatorTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Get a default application id derived from the entry assembly name and version.
        /// </summary>
        /// <returns></returns>
        internal static string GetDefaultApplicationId()
        {
            var appName = System.Reflection.Assembly.GetEntryAssembly()!.GetName();
            return $"{appName.Name}.{appName.Version}";
        }
    }
}
