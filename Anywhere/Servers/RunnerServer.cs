using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;

namespace AnywhereNET
{
    // NOTE: it's outside the scope of this library to decide how and when to spin up new Runners.

    public class RunnerServer : IDisposable
    {
        private long IsRunning = 0;

        private Thread? WorkLoopThread;

        private Connection? OrchestratorConnection;

        private MessageChannel? OrchestratorChannel;

        private RunnerConfiguration Configuration;

        private bool IsDisposed = false;

        private ConcurrentDictionary<Guid, Worker> ActiveWorkers = new ConcurrentDictionary<Guid, Worker>();

        private ConcurrentQueue<Worker> CompletedWorkers = new ConcurrentQueue<Worker>();

        public RunnerServer(RunnerConfiguration? configuration = null)
        {
            Configuration = configuration ?? new RunnerConfiguration();
        }

        public void Dispose()
        {
            Stop();
            if (!IsDisposed)
            {
                OrchestratorConnection?.Dispose();
                IsDisposed = true;
            }
            GC.SuppressFinalize(this);
        }

        public async Task Start(X509Certificate2 cert, int port, IPAddress? ip = null)
        {
            ip = ip ?? IPAddress.Any;

            if (Configuration.OrchestratorUri != null)
            {
                // create a secure connection to the optional orchestrator
                var uri = Configuration.OrchestratorUri;
                var client = new TcpClient(uri!.Host, uri.Port);
                OrchestratorConnection = new Connection(client, uri.Host);
                OrchestratorChannel = new MessageChannel(OrchestratorConnection.GetChannel(Constants.RunnerChannel));

                // TODO: inform the orchestrator that we exist

                // TODO: start an infrequent (eg 60s) heartbeat to orchestrator to update status and environment stats
            }

            // listen for incoming connections
            var listener = new TcpListener(ip, port);
            listener.Start();

            // start a work thread to accept and process new connections
            IsRunning = 1;
            WorkLoopThread = new Thread(() => WorkLoop(listener, cert));
            WorkLoopThread.Start();
        }

        public void Stop()
        {
            // TODO: inform the orchestrator that we're going away

            // signal the work thread to stop
            Interlocked.Exchange(ref IsRunning, 0);

            // wait for it to finish
            WorkLoopThread?.Join();
            WorkLoopThread = null;
        }

        private void WorkLoop(TcpListener listener, X509Certificate2 cert)
        {
            while (Interlocked.Read(ref IsRunning) == 1)
            {
                CleanupCompletedWorkers();

                // TODO: how to handle serial queued vs parallel requests

                // sleep unless a new connection is pending
                if (!listener.Pending())
                {
                    ThreadHelpers.Yield();
                    continue;
                }

                // block and wait for the next incoming connection
                var client = listener.AcceptTcpClient();

                // create a secure connection to the endpoint
                // and start processing it in a dedicated thread
                var connection = new Connection(client, cert);
                var id = Guid.NewGuid();
                var thread = new Thread(() => ProcessClient(id, connection));
                thread.Start();

                // track the thread/connection pair as an active worker
                ActiveWorkers.TryAdd(id, new Worker(thread, connection));
            }

            CleanupCompletedWorkers();
        }

        private void CleanupCompletedWorkers()
        {
            while (CompletedWorkers.TryDequeue(out Worker? worker))
            {
                worker?.Thread.Join(1000);
                worker?.Connection.Dispose();
            }
        }

        private async void ProcessClient(Guid id, Connection connection)
        {
            // create communication channels
            var expressionChannel = connection.GetChannel(Constants.ExpressionChannel);
            var assembliesChannel = connection.GetChannel(Constants.AssembliesChannel);
            var filesChannel = connection.GetChannel(Constants.FilesChannel);

            try
            {

                // TODO: if this runner is "too busy" send a "too busy" message

                // TODO: add support for the application to send a "cancel" message

                // create the execution context that is available to the expression while it's running
                var executionContext = new ExecutionContext
                {
                    ExecutionMode = ExecutionModes.Local,
                    FilesChannel = filesChannel,
                };

                // create the runtime environment
                var environment = new Environment
                {
                    AssemblyContext = new AssemblyLoadContext(Guid.NewGuid().ToString(), true),
                    ExecutionContext = executionContext,
                    ResolveRemoteAssemblyAsync = new DefaultRemoteAssemblyResolver(assembliesChannel).ResolveAssembly,
                };

                // TODO: signal any orchestrator that we're starting

                // receive the expression request on the expression channel
                expressionChannel.BlockingReads = true;
                var expressionRequest = new ExpressionRequestMessage();
                expressionRequest.Read(expressionChannel);

                // TODO: schedule a frequent (eg 10s) heartbeat to orchestrator to update status and environment stats

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

                // TODO: signal any orchestrator that we're finished
            }
            catch (Exception ex)
            {
                // send the exception back to the application on the expression channel
                var resultMessage = new ExpressionResponseMessage(ex);
                resultMessage.Write(expressionChannel);
                // TODO: log it?
                // TODO: signal any orchestrator that we failed
            }
            finally
            {
                // find and move the worker to the completed queue so it can
                // be disposed by the main thread
                if (ActiveWorkers.TryGetValue(id, out var worker))
                {
                    CompletedWorkers.Enqueue(worker);
                }
            }
        }

        private class Worker
        {
            public Thread Thread;
            public Connection Connection;
            public Worker(Thread thread, Connection connection)
            {
                Thread = thread;
                Connection = connection;
            }
        }
    }
}