namespace DidoNet
{
    /// <summary>
    /// Security authentication policies used when establishing a new client connection to a 
    /// remote server.
    /// </summary>
    public enum ServerCertificateValidationPolicies
    {
        /// <summary>
        /// Indicates the server's certificate must exist in the root CA on the client's machine.
        /// </summary>
        RootCA,

        /// <summary>
        /// Indicates the thumbprint of the server's certificate will be matched against a configured
        /// thumbprint.
        /// </summary>
        Thumbprint,

        /// <summary>
        /// Indicates the server's certificate is accepted as authentic without ANY independent verification.
        /// <para/>WARNING: this option is to support more efficient development and testing, but should never
        /// be used in production.
        /// </summary>
        _SKIP_,
    }
}
