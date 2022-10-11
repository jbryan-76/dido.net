using System;

namespace DidoNet
{
    /// <summary>
    /// Configuration for a mediator.
    /// </summary>
    public class MediatorConfiguration
    {
        /// <summary>
        /// The unique id of the server instance.
        /// If not provided, a random unique id is used.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The uri for applications to use to connect to a runner.
        /// Setting this explicitly may be necessary for proper routing when
        /// load balancers and network translation services are in use.
        /// If not provided, will default to the endpoint (ip address + port) the 
        /// runner server starts on.
        /// </summary>
        public string? Endpoint { get; set; } = null;

        /// <summary>
        /// The validation policy for authenticating the remote server certificate for SSL connections.
        /// </summary>
        public ServerCertificateValidationPolicies ServerValidationPolicy { get; set; } = ServerCertificateValidationPolicies.RootCA;

        /// <summary>
        /// For ServerCertificateValidationPolicies.Thumbprint, the specific certificate thumbprint to validate against.
        /// </summary>
        public string ServerCertificateThumbprint { get; set; } = string.Empty;

        /// <summary>
        /// A backing store for jobs.
        /// </summary>
        public IJobStore JobStore { get; set; } = new MemoryJobStore();

        /// <summary>
        /// Specifies how long "expired" job records are retained by the mediator before being automatically deleted.
        /// An "expired" job is a job that has finished executing (i.e. its Finished property is not null), regardless
        /// of whether it completed nominally or with an error or timeout.
        /// A value of zero indicates records never expire and are not automatically deleted.
        /// The default value is zero (i.e. records never expire).
        /// <para/>NOTE for best practice an application should explicitly delete jobs after they have completed and
        /// their results retrieved, but this lifetime setting can be used to help prevent unchecked and unbounded job retention.
        /// </summary>
        public TimeSpan JobLifetime = TimeSpan.Zero;

        /// <summary>
        /// Specifies how often the mediator should check for and delete expired jobs.
        /// The default value is 1 hour.
        /// </summary>
        public TimeSpan JobExpirationFrequency = TimeSpan.FromMinutes(60);
    }
}