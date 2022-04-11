using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace AnywhereNET
{
    public class OrchestratorConfiguration
    {
        // TODO: "job mode": generate an optional id for an execution request, monitor the job, "save" the result. optional backing store?
        // TODO: delegate for optional data persistance
    }

    // TODO: orchestrator should determine which runner in its pool has resources available to run.
    // TODO: can it start up new runners? how does it monitor them?
    // TODO: orchestrator should also be able to act as a single runner
    public class OrchestratorServer
    {
        private long Connected = 0;

        private Thread? WorkLoopThread;

        private bool IsDisposed = false;

        private ConcurrentBag<Connection> RunnerPool = new ConcurrentBag<Connection>();

        private OrchestratorConfiguration Configuration;

        public OrchestratorServer(OrchestratorConfiguration? configuration = null)
        {
            Configuration = configuration ?? new OrchestratorConfiguration();
        }

        public void Dispose()
        {
            Stop();
            if (!IsDisposed)
            {
                IsDisposed = true;
            }
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
            WorkLoopThread?.Join();
            WorkLoopThread = null;
        }

        private void WorkLoop(TcpListener listener, X509Certificate2 cert)
        {
            while (Interlocked.Read(ref Connected) == 1)
            {
                // TODO: if any runner connection hasn't had activity for more than
                // TODO: a specific amount of time, assume it's gone and remove it

                if (!listener.Pending())
                {
                    ThreadHelpers.Yield();
                    continue;
                }

                // block and wait for the next incoming connection
                var client = listener.AcceptTcpClient();

                // create a secure connection to the client.
                // the thread below will dispose it.
                var connection = new Connection(client, cert);

                // start processing the connection in a dedicated thread
                var thread = new Thread(() => ProcessClient(connection));
                thread.Start();
            }
        }

        private async void ProcessClient(Connection connection)
        {
            // create communication channels
            var applicationChannel = connection.GetChannel(Constants.ApplicationChannel);
            var runnerChannel = new MessageChannel(connection.GetChannel(Constants.RunnerChannel));

            try
            {
                // applications will contact the orchestrator to make requests,
                // eg get a runner, or check job status
                applicationChannel.OnDataAvailable += (channel) =>
                {

                };

                // runners will contact the orchestrator to update their status
                runnerChannel.OnMessageReceived += (message, channel) =>
                {
                    //RunnerPool
                };

                while (Interlocked.Read(ref Connected) == 1 && connection.IsConnected)
                {
                    ThreadHelpers.Yield();
                }

                // TODO: if the connection was for a runner, remove it from the pool
            }
            catch (Exception ex)
            {
                // TODO: catch exceptions and transmit back to application
                // TODO: log it?
            }
            finally
            {
                connection.Dispose();
            }
        }
    }
}