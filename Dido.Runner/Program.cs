using Microsoft.Extensions.Configuration;
using NLog;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Topshelf;
using Topshelf.Runtime.DotNetCore;

namespace DidoNet
{
    /// <summary>
    /// Implements a basic Dido.NET Runner service for windows.
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
            var rc = HostFactory.Run(c =>
            {
                // change the environment builder on non-windows systems
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    c.UseEnvironmentBuilder(
                      target => new DotNetCoreEnvironmentBuilder(target)
                    );
                }
                c.Service<T>();
                c.UseNLog();
                c.SetDescription(description);
                c.SetDisplayName(displayName);
                c.SetServiceName(serviceName);
                switch (type)
                {
                    case IdentityTypes.Prompt: c.RunAsPrompt(); break;
                    case IdentityTypes.Network: c.RunAsNetworkService(); break;
                    case IdentityTypes.LocalSystem: c.RunAsLocalSystem(); break;
                    case IdentityTypes.LocalService: c.RunAsLocalService(); break;
                }
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

            X509Certificate2? cert;
            if (!string.IsNullOrEmpty(serverConfig.CertFile))
            {
                cert = new X509Certificate2(serverConfig.CertFile, serverConfig.CertPass);

            }
            else if (!string.IsNullOrEmpty(serverConfig.CertBase64))
            {
                cert = new X509Certificate2(Convert.FromBase64String(serverConfig.CertBase64), serverConfig.CertPass);
            }
            else if (!string.IsNullOrEmpty(serverConfig.FindBy))
            {
                if (string.IsNullOrEmpty(serverConfig.FindValue))
                {
                    throw new InvalidOperationException($"When '{nameof(ServerConfiguration.FindBy)}' is provided '{nameof(ServerConfiguration.FindValue)}' is also required.");
                }

                if (!Enum.TryParse<X509FindType>(serverConfig.FindBy, out var findType))
                {
                    throw new InvalidOperationException($"Value '{serverConfig.FindBy}' could not be parsed to a legal value for type '{nameof(X509FindType)}'. Legal values are: {string.Join(',', Enum.GetValues<X509FindType>())}");
                }

                // find the certificate from the machine root CA
                var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);
                var certs = store.Certificates.Find(findType, serverConfig.FindValue, false);
                cert = certs.Cast<X509Certificate2>().FirstOrDefault();
                if (cert == null)
                {
                    throw new InvalidOperationException($"No certificate corresponding to {serverConfig.FindBy}:{serverConfig.FindValue} could be found in the system root CA.");
                }
            }
            else
            {
                throw new InvalidOperationException($"Missing X509 certificate configuration from appsettings");
            }

            IPAddress? ipAddress = null;
            if (!string.IsNullOrEmpty(serverConfig.IpAddress))
            {
                ipAddress = IPAddress.Parse(serverConfig.IpAddress);
            }

            Server.Start(cert, serverConfig.Port, ipAddress);

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
        public IConfiguration? Configuration { get; private set; }

        private RunnerServer? Server = null;

        protected RunnerConfiguration? RunnerConfig;

        private ILogger logger = NLog.LogManager.GetCurrentClassLogger();
    }

    class Program
    {
        public static void Main()
        {
            RunnerService.EasyRun<RunnerService>("Dido.NET Runner Service", "Dido.NET Runner", "Dido.NET.Runner.Win");
        }
    }
}