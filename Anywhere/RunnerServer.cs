using System.Net;
using System.Net.Sockets;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;

namespace AnywhereNET
{
    public class RunnerServer : IDisposable
    {

        private long Connected = 0;

        private Thread? WorkLoopThread;

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        public async Task Start(X509Certificate2 cert, int port)
        {
            // listen for incoming connections
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            // start a work thread to accept and process new connections
            Connected = 1;
            WorkLoopThread = new Thread(() => WorkLoop(listener, cert));
        }

        public void Stop()
        {
            Interlocked.Exchange(ref Connected, 0);
            if (WorkLoopThread != null)
            {
                WorkLoopThread.Join();
                WorkLoopThread = null;
            }
        }

        private async void WorkLoop(TcpListener listener, X509Certificate2 cert)
        {
            var connections = new List<Connection>();
            var threads = new List<Thread>();

            while (Interlocked.Read(ref Connected) == 1)
            {
                if (!listener.Pending())
                {
                    Thread.Sleep(1);
                    continue;
                }

                // block and wait for the next incoming connection
                var client = await listener.AcceptTcpClientAsync();

                // create a secure connection to the client
                var connection = new Connection(client, cert);
                connections.Add(connection);

                // start processing the connection in a dedicated thread
                var thread = new Thread(() => ProcessClient(connection));
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

        private async void ProcessClient(Connection connection)
        {
            // create communication channels
            var expressionChannel = connection.GetChannel(Constants.ExpressionChannel);
            var assembliesChannel = connection.GetChannel(Constants.AssembliesChannel);
            var filesChannel = connection.GetChannel(Constants.FilesChannel);

            // TODO: ExecutionContext should have filesChannel

            var executionContext = new ExecutionContext
            {
                ExecutionMode = ExecutionModes.Local
            };

            // TODO: DefaultRemoteAssemblyResolver should have assembliesChannel

            //assembliesChannel.BlockingReads = true;
            expressionChannel.BlockingReads = true;

            // TODO: create the runtime environment
            var environment = new Environment
            {
                AssemblyContext = new AssemblyLoadContext(Guid.NewGuid().ToString(), true),
                ExecutionContext = executionContext,
                ResolveRemoteAssemblyAsync = new DefaultRemoteAssemblyResolver(assembliesChannel).ResolveAssembly,
            };

            try
            {
                // TODO: probably don't need a loop here
                //while (connection.IsConnected)
                {
                    // TODO: receive expression request on expression channel
                    var expressionRequest = new ExpressionRequestMessage();
                    expressionRequest.Read(expressionChannel);

                    //byte[] bytes;
                    using (var stream = new MemoryStream(expressionRequest.Bytes))
                    {
                        // TODO: deserialize it
                        var decodedLambda = await ExpressionSerializer.DeserializeAsync<object>(stream, environment);
                        // TODO: execute it
                        var result = decodedLambda.Invoke(environment.ExecutionContext);
                        // TODO: send the result on requestChannel
                        var resultMessage = new ExpressionResultMessage(result);
                        resultMessage.Write(expressionChannel);
                    }

                    Thread.Sleep(1);
                }

            }
            catch (Exception ex)
            {
                // TODO: catch exceptions and transmit back to host
            }
        }
    }
}