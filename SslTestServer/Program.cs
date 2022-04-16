using DidoNet;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace foo
{
    // 1) generate a cert
    // openssl req -newkey rsa:2048 -new -nodes -keyout test.key -x509 -days 365 -out test.pem
    // 2) convert to a pkcs12 pfx
    // openssl pkcs12 -export -out cert.pfx -inkey test.key -in test.pem -password pass:1234
    // 3) run
    // ./SslTestServer.exe cert.pfx 1234

    // tips: https://paulstovell.com/x509certificate2/

    public sealed class SslTcpServer
    {
        static X509Certificate2? serverCertificate = null;

        public static async Task RunServer(int port, string pfxFile, string password)
        {
            var connections = new List<Connection>();

            // load the server certificate
            serverCertificate = new X509Certificate2(pfxFile, password);

            // listen for incoming connections
            TcpListener listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            while (true)
            {
                Console.WriteLine("Waiting for a client to connect...");

                // block and wait for the next incoming connection
                var client = await listener.AcceptTcpClientAsync();

                // create a secure connection to the client
                Connection connection = new Connection(client, serverCertificate);
                connections.Add(connection);

#if DEBUG
                // send a debug message to confirm the client can receive
                _ = Task.Delay(0).ContinueWith((task) =>
                {
                    connection.Debug("Hello from the server");
                });
#endif
            }
        }

        private static void DisplayUsage()
        {
            Console.WriteLine("Use:");
            Console.WriteLine("serverSync certificateFile.pfx password");
            System.Environment.Exit(1);
        }

        public static int Main(string[] args)
        {
            string? pfxFile = null;
            string? password = null;
            if (args == null || args.Length < 2)
            {
                DisplayUsage();
                return 1;
            }

            pfxFile = args[0];
            password = args[1];

            SslTcpServer.RunServer(8080, pfxFile, password).GetAwaiter().GetResult();

            return 0;
        }
    }
}