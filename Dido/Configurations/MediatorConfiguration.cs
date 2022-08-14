using System;

namespace DidoNet
{
    /// <summary>
    /// Configures a mediator.
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

        // TODO: "job mode": generate an optional id for an execution request, monitor the job, "save" the result
        // TODO: delegate for optional data persistence. default is in-memory
    }
}