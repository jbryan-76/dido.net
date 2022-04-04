using System.Net;
using System.Net.Sockets;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;

namespace AnywhereNET.Env
{
    public class OrchestratorServer
    {

    }

    public class ContainerServer
    {
        static readonly ushort RequestChannel = 0;
        static readonly ushort AssembliesChannel = 1;
        static readonly ushort FilesChannel = 2;

        public async Task Run(X509Certificate2 cert, int port)
        {
            var connections = new List<Connection>();
            var threads = new List<Thread>();

            // listen for incoming connections
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            // TODO: add some kind of cancellation support
            while (true)
            {
                // block and wait for the next incoming connection
                var client = await listener.AcceptTcpClientAsync();

                // create a secure connection to the client
                var connection = new Connection(client, cert);
                connections.Add(connection);

                // start processing the connection in a dedicated thread
                var thread = new Thread(() => ClientLoop(connection));
                threads.Add(thread);
                thread.Start();
            }

            // cleanup
            foreach (var connection in connections)
            {
                connection.Disconnect();
                connection.Dispose();
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
        }

        // TODO: add some kind of Stop() or Cancel()?

        private void ClientLoop(Connection connection)
        {
            var requestChannel = connection.GetChannel(RequestChannel);
            var assembliesChannel = connection.GetChannel(AssembliesChannel);
            var filesChannel = connection.GetChannel(FilesChannel);

            var executionContext = new ExecutionContext
            {
                ExecutionMode = ExecutionModes.Local
            };

            // TODO: create the runtime environment
            var environment = new Environment
            {
                AssemblyContext = new AssemblyLoadContext(Guid.NewGuid().ToString(), true),
                Context = executionContext,
                //ResolveRemoteAssemblyAsync
            };

            while (connection.IsConnected)
            {
                Thread.Sleep(1);
            }
        }
    }
}