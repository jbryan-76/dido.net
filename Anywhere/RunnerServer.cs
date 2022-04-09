using System.Net;
using System.Net.Sockets;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;

namespace AnywhereNET
{
    // TODO: configure to indicate OS environment
    // TODO: configure to indicate support for queued vs concurrent vs single requests
    // TODO: configure to indicate (optional) connection to the orchestrator
    public class RunnerConfiguration
    {

    }


    public class RunnerServer : IDisposable
    {
        private long Connected = 0;

        private Thread? WorkLoopThread;

        public RunnerServer(RunnerConfiguration? configuration = null)
        {

        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        public async Task Start(X509Certificate2 cert, int port, IPAddress? ip = null)
        {
            ip = ip ?? IPAddress.Any;

            // listen for incoming connections
            var listener = new TcpListener(ip, port);
            listener.Start();

            // start a work thread to accept and process new connections
            Connected = 1;
            WorkLoopThread = new Thread(() => WorkLoop(listener, cert));
            WorkLoopThread.Start();
        }

        public void Stop()
        {
            // signal the thread to stop
            Interlocked.Exchange(ref Connected, 0);

            // wait for it to finish
            if (WorkLoopThread != null)
            {
                WorkLoopThread.Join();
                WorkLoopThread = null;
            }
        }

        private void WorkLoop(TcpListener listener, X509Certificate2 cert)
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
                var client = listener.AcceptTcpClient();

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

            expressionChannel.BlockingReads = true;

            // create the runtime environment
            var environment = new Environment
            {
                AssemblyContext = new AssemblyLoadContext(Guid.NewGuid().ToString(), true),
                ExecutionContext = executionContext,
                ResolveRemoteAssemblyAsync = new DefaultRemoteAssemblyResolver(assembliesChannel).ResolveAssembly,
            };

            try
            {
                // receive the expression request on the expression channel
                var expressionRequest = new ExpressionRequestMessage();
                expressionRequest.Read(expressionChannel);

                // TODO: heartbeat to orchestrator

                using (var stream = new MemoryStream(expressionRequest.Bytes))
                {
                    // deserialize the expression
                    var decodedLambda = await ExpressionSerializer.DeserializeAsync<object>(stream, environment);
                    
                    // execute it
                    var result = decodedLambda.Invoke(environment.ExecutionContext);

                    // send the result back to the application on the expression channel
                    var resultMessage = new ExpressionResponseMessage(result);
                    resultMessage.Write(expressionChannel);
                }
            }
            catch (Exception ex)
            {
                // TODO: catch exceptions and transmit back to host
                // send the exception back to the application on the expression channel
                var resultMessage = new ExpressionResponseMessage(ex);
                resultMessage.Write(expressionChannel);
                // TODO: log it?
                // TODO: signal orchestrator?
            }
        }
    }
}