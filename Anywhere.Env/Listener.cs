using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace AnywhereNET.Env
{
    public class OrchestratorServer
    {

    }

    public class ContainerServer
    {
        static private Task<Connection> Start(X509Certificate2 cert, int port)
        {
            return Task.Run(async () =>
            {
                // listen for incoming connections
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();

                // block and wait for the next incoming connection
                var client = await listener.AcceptTcpClientAsync();

                // create a secure connection to the client
                var serverConnection = new Connection(client, cert);
                return serverConnection;
            });
        }

    }
}