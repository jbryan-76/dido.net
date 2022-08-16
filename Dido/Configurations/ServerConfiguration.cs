namespace DidoNet
{
    public class ServerConfiguration
    {
        /// <summary>
        /// The port the server will listen to for incoming connections.
        /// </summary>
        public int? Port { get; set; } = 4940;

        /// <summary>
        /// The optional IpAddress the server will bind to.
        /// If provided, must be in dotted-quad notation for IPv4 and in colon-hexadecimal notation for IPv6.
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// The explicit path to a certificate to use for network encryption.
        /// </summary>
        public string? CertFile { get; set; }

        /// <summary>
        /// A base-64 encoded binary certificate to use for network encryption.
        /// </summary>
        public string? CertBase64 { get; set; }

        /// <summary>
        /// The certificate password. Required when using CertFile or CertBase64.
        /// </summary>
        public string? CertPass { get; set; }

        /// <summary>
        /// If set, indicates a system root CA certificate will be used.
        /// <para/>Must be a legal enumeration value for X509FindType.
        /// </summary>
        public string? FindBy { get; set; }

        /// <summary>
        /// If set, indicates a system root CA certificate will be used.
        /// <para/>Must be a value corresponding to FindBy which is used to locate
        /// a specific certificate.
        /// </summary>
        public string? FindValue { get; set; }

    }
}