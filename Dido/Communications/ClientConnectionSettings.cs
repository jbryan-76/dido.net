﻿namespace DidoNet
{
    /// <summary>
    /// Configuration settings for connecting to a remote client.
    /// </summary>
    public class ClientConnectionSettings
    {
        /// <summary>
        /// The validation policy for authenticating the remote server certificate for SSL connections.
        /// </summary>
        public ServerCertificateValidationPolicies ValidaionPolicy { get; set; }
            = ServerCertificateValidationPolicies.RootCA;

        /// <summary>
        /// For ServerCertificateValidationPolicies.Thumbprint, the specific certificate thumbprint to 
        /// validate against.
        /// </summary>
        public string Thumbprint { get; set; } = string.Empty;
    }
}
