using Microsoft.Extensions.Configuration;
using NLog;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Topshelf;
using Topshelf.Runtime.DotNetCore;

namespace DidoNet.Runner.Windows
{
    /// <summary>
    /// Implements a basic Dido.NET Runner service for Windows.
    /// Note this is a reference implementation of a standard runner service.
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
        /// When running the service from the command line, allows specifying the port to use.
        /// </summary>
        private static int? CommandLinePort = null;

        /// <summary>
        /// When running the service from the command line, allows specifying the runner id.
        /// This sets the RunnerConfiguration.Id value, overriding any appsettings.json configured value.
        /// </summary>
        private static string? CommandLineId = null;

        /// <summary>
        /// When running the service from the command line, allows specifying the runner label.
        /// This sets the RunnerConfiguration.Label value, overriding any appsettings.json configured value.
        /// </summary>
        private static string? CommandLineLabel = null;

        /// <summary>
        /// When running the service from the command line, allows specifying the mediator
        /// that schedules tasks for this runner.
        /// This sets the RunnerConfiguration.MediatorUri value, overriding any appsettings.json configured value.
        /// </summary>
        private static string? CommandLineMediator = null;

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
                // add support to override certain configuration from the command line
                c.AddCommandLineDefinition("port", v => CommandLinePort = Int32.Parse(v));
                c.AddCommandLineDefinition("id", v => CommandLineId = v);
                c.AddCommandLineDefinition("label", v => CommandLineLabel = v);
                c.AddCommandLineDefinition("mediator", v => CommandLineMediator = v);
                c.ApplyCommandLine();

                // change the environment builder on non-windows systems
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    c.UseEnvironmentBuilder(
                      target => new DotNetCoreEnvironmentBuilder(target)
                    );
                }

                // now start the service
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

        /// <summary>
        /// Used by TopShelf to start the service.
        /// </summary>
        /// <param name="hostControl"></param>
        /// <returns></returns>
        [ExcludeFromCodeCoverage]
        public bool Start(HostControl hostControl)
        {
            try
            {
                // issue warning if nlog configuration is not found
                if (!File.Exists(Path.Combine(System.Environment.CurrentDirectory, "nlog.config")))
                {
                    Console.WriteLine($"Warning: 'nlog.config' does not exist.");
                }

                var environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

                // get runner configuration
                var builder = new ConfigurationBuilder()
                                .SetBasePath(Directory.GetCurrentDirectory())
                                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                                .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
                                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                                .AddEnvironmentVariables();

                Configuration = builder.Build();
                RunnerConfig = new RunnerConfiguration();
                Configuration.Bind("Runner", RunnerConfig);
                ServerConfig = new ServerConfiguration();
                Configuration.Bind("Server", ServerConfig);

                if (CommandLinePort != null)
                {
                    ServerConfig.Port = CommandLinePort.Value;
                }
                if (!string.IsNullOrEmpty(CommandLineId))
                {
                    RunnerConfig.Id = CommandLineId;
                }
                if (!string.IsNullOrEmpty(CommandLineLabel))
                {
                    RunnerConfig.Label = CommandLineLabel;
                }
                if (!string.IsNullOrEmpty(CommandLineMediator))
                {
                    RunnerConfig.MediatorUri = CommandLineMediator;
                }

                Server = new RunnerServer(RunnerConfig);

                // load the certificate to use for encrypting connections
                X509Certificate2? cert;
                if (!string.IsNullOrEmpty(ServerConfig.CertFile))
                {
                    cert = new X509Certificate2(ServerConfig.CertFile, ServerConfig.CertPass);

                }
                else if (!string.IsNullOrEmpty(ServerConfig.CertBase64))
                {
                    cert = new X509Certificate2(Convert.FromBase64String(ServerConfig.CertBase64), ServerConfig.CertPass);
                }
                else if (!string.IsNullOrEmpty(ServerConfig.FindBy))
                {
                    if (string.IsNullOrEmpty(ServerConfig.FindValue))
                    {
                        throw new InvalidConfigurationException($"When '{nameof(ServerConfiguration.FindBy)}' is provided '{nameof(ServerConfiguration.FindValue)}' is also required.");
                    }

                    if (!Enum.TryParse<X509FindType>(ServerConfig.FindBy, out var findType))
                    {
                        throw new InvalidConfigurationException($"Value '{ServerConfig.FindBy}' could not be parsed to a legal value for type '{nameof(X509FindType)}'. Legal values are: {string.Join(',', Enum.GetValues<X509FindType>())}");
                    }

                    // find the certificate from the machine root CA
                    var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                    store.Open(OpenFlags.ReadOnly);
                    var certs = store.Certificates.Find(findType, ServerConfig.FindValue, false);
                    cert = certs.Cast<X509Certificate2>().FirstOrDefault();
                    if (cert == null)
                    {
                        throw new InvalidConfigurationException($"No certificate corresponding to {ServerConfig.FindBy}:{ServerConfig.FindValue} could be found in the system root CA.");
                    }
                }
                else
                {
                    throw new InvalidConfigurationException($"Missing X509 certificate configuration from appsettings");
                }

                IPAddress? ipAddress = null;
                if (!string.IsNullOrEmpty(ServerConfig.IpAddress))
                {
                    ipAddress = IPAddress.Parse(ServerConfig.IpAddress);
                }

                Server.Start(cert, ServerConfig.Port, ipAddress);

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                Console.WriteLine($"Fatal error: {ex.ToString()}");
                return false;
            }
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
            Server?.Dispose();
            Server = null;
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

        protected ServerConfiguration? ServerConfig;

        private readonly ILogger logger = NLog.LogManager.GetCurrentClassLogger();
    }

    class Program
    {
        public static void Main(string[] args)
        {
            RunnerService.EasyRun<RunnerService>("Dido.NET Runner Service", "Dido.NET Runner", "Dido.NET.Runner");
        }
    }
}