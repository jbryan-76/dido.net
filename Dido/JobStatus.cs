// generate self-signed certs automatically:
// https://stackoverflow.com/questions/695802/using-ssl-and-sslstream-for-peer-to-peer-authentication
// view installed certificates: certmgr.msc
// install a cert programmatically:
// https://docs.microsoft.com/en-us/troubleshoot/developer/dotnet/framework/general/install-pfx-file-using-x509certificate
// install a cert manually:
// https://community.spiceworks.com/how_to/1839-installing-self-signed-ca-certificate-in-windows

// 1) generate a cert
// openssl req -newkey rsa:2048 -new -nodes -keyout test.key -x509 -days 365 -out test.pem
// NOTE: the "Common Name"/FQDN (fully qualified domain name) must match the "targetHost" parameter of the Connection
// constructor specialized for connecting a client to a server.
// 2) convert to a pkcs12 pfx
// openssl pkcs12 -export -out cert.pfx -inkey test.key -in test.pem -password pass:1234

namespace DidoNet
{
    public enum JobStatus
    {
        Running,
        Complete,
        Cancelled,
        Error,
        Timeout,
        Abandoned
    }
}
