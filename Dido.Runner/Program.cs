using Microsoft.Extensions.Configuration;
using NLog;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;
using Topshelf;

namespace DidoNet
{
    /// <summary>
    /// Implements a basic Dido.NET Runner service.
    /// </summary>
    public class RunnerService : ServiceControl, IDisposable
    {
        /// <summary>
        /// The identity to use when running the service.
        /// </summary>
        public enum IdentityTypes
        {
            /// <summary>
            /// During service installation, prompt for the identity username and password for the service to use.
            /// </summary>
            Prompt,

            /// <summary>
            /// Use the NETWORK_SERVICE built-in account.
            /// </summary>
            Network,

            /// <summary>
            /// [DEFAULT] Use the local system account.
            /// </summary>
            LocalSystem,

            /// <summary>
            /// Use the local service account.
            /// </summary>
            LocalService
        }

        /// <summary>
        /// Runs the service using standard options and minimal configuration. 
        /// Use in the console app's Program.cs as follows:
        /// <para/>
        /// <c>
        /// ServiceBase.EasyRun&lt;MyRunnerService&gt;("My description", "My Display Name", "MyServiceName", ServiceBase.IdentityTypes.LocalSystem);
        /// </c>
        /// <para/>
        /// If more control is needed, see here: http://docs.topshelf-project.com/en/latest/configuration/config_api.html
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="description"></param>
        /// <param name="displayName"></param>
        /// <param name="serviceName"></param>
        /// <param name="type"></param>
        [ExcludeFromCodeCoverage]
        public static void EasyRun<T>(string description, string displayName, string serviceName, IdentityTypes type = IdentityTypes.LocalSystem) where T : class, ServiceControl, new()
        {
            var rc = HostFactory.Run(x =>
            {
                x.Service<T>();
                x.UseNLog();
                switch (type)
                {
                    case IdentityTypes.Prompt: x.RunAsPrompt(); break;
                    case IdentityTypes.Network: x.RunAsNetworkService(); break;
                    case IdentityTypes.LocalSystem: x.RunAsLocalSystem(); break;
                    case IdentityTypes.LocalService: x.RunAsLocalService(); break;
                }
                x.SetDescription(description);
                x.SetDisplayName(displayName);
                x.SetServiceName(serviceName);
            });

            System.Environment.ExitCode = (int)Convert.ChangeType(rc, rc.GetTypeCode());
        }

        ///// <summary>
        ///// This constructor is only used for internal testing
        ///// </summary>
        //internal void StartTest(Func<JobManagerClient> client, RunnerSettings config)
        //{
        //    internalTestClient = client;
        //    timer = new Timer(
        //        (cfg) => CheckForNextJob(cfg),
        //        config,
        //        TimeSpan.Zero,
        //        TimeSpan.FromSeconds(config.JobCheckFrequencyInSeconds));
        //}

        /// <summary>
        /// Used by TopShelf to start the service.
        /// </summary>
        /// <param name="hostControl"></param>
        /// <returns></returns>
        [ExcludeFromCodeCoverage]
        public bool Start(HostControl hostControl)
        {
            var environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            // get runner configuration
            var builder = new ConfigurationBuilder()
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                            .AddEnvironmentVariables();

            Configuration = builder.Build();
            RunnerConfig = new RunnerConfiguration();
            Configuration.Bind(RunnerConfig);
            var serverConfig = new ServerConfiguration();
            Configuration.Bind(serverConfig);

            if (Server == null)
            {
                Server = new RunnerServer(RunnerConfig);
            }
            logger.Info($"Starting with configuration from appsettings.json.");

            X509Certificate2 cert;
            if (!string.IsNullOrEmpty(serverConfig.CertFile))
            {
                cert = new X509Certificate2(serverConfig.CertFile, serverConfig.CertPass);

            }
            else if (!string.IsNullOrEmpty(serverConfig.CertBase64))
            {
                cert = new X509Certificate2(Convert.FromBase64String(serverConfig.CertBase64), serverConfig.CertPass);
            }
            else
            {
                throw new InvalidOperationException($"Missing X509 certificate configuration from appsettings");
            }

            // TODO: serverConfig.IpAddress
            Server.Start(cert, serverConfig.Port);

            return true;
        }

        /// <summary>
        /// Used by TopShelf to stop the service
        /// </summary>
        /// <param name="hostControl"></param>
        /// <returns></returns>
        [ExcludeFromCodeCoverage]
        public bool Stop(HostControl hostControl)
        {
            Server?.Stop();
            return true;
        }

        /// <summary>
        /// IDisposable implementation.
        /// </summary>
        public virtual void Dispose()
        {
            Server?.Dispose();
        }

        /// <summary>
        /// The configuration object to access service configuration.
        /// </summary>
        public IConfiguration Configuration { get; private set; }

        private RunnerServer? Server = null;

        protected RunnerConfiguration RunnerConfig;

        //private DateTimeOffset? cancelledTimestamp = null;
        private ILogger logger = NLog.LogManager.GetCurrentClassLogger();
        //private Func<JobManagerClient> internalTestClient;
        //private Timer timer;
        //private int busy = 0;
    }

    class Program
    {
        public static void Main()
        {
            RunnerService.EasyRun<RunnerService>("Dido.NET Runner Service", "My Runner", "MyRunner");
        }
    }
}