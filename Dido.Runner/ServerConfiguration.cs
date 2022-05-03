namespace DidoNet
{
    public class ServerConfiguration
    {
        public int Port { get; set; } = 4940;

        public string? CertFile { get; set; }

        public string? CertBase64 { get; set; }

        public string? CertPass { get; set; }

        // TODO: IpAddress in form "X.X.X.X"
    }
}