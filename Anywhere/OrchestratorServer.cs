using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace AnywhereNET
{
    // TODO: orchestrator should determine which runner in its pool
    // TODO: has resources available to run.
    // TODO: can it start up new runners? how does it monitor them?
    // TODO: orchestrator should also be able to act as a single runner
    public class OrchestratorServer
    {
        // TODO: "job mode": generate an optional id for an execution request, monitor the job, "save" the result. optional backing store?
        // TODO: 


        private long Connected = 0;

        private Thread? WorkLoopThread;

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
            var applicationChannel = connection.GetChannel(Constants.ApplicationChannel);
            var runnerChannel = connection.GetChannel(Constants.RunnerChannel);

            // TODO: determine whether the client is a runner or an application
            // TODO: if a runner, register/update it
            // TODO: if an application, process its request

            try
            {

            }
            catch (Exception ex)
            {
                // TODO: catch exceptions and transmit back to host
                // TODO: log it?
            }
        }
    }
}