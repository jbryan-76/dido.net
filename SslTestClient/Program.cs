using DidoNet;
using System.Net.Sockets;

namespace foo
{
    // 1) run
    // ./SslTestClient.exe localhost

    public class SslTcpClient
    {
        public static void RunClient(string targetHost, int port)
        {
            var settings = new ClientConnectionSettings { ValidaionPolicy = ServerCertificateValidationPolicies._SKIP_ };
            Console.WriteLine($"Connecting to {targetHost}...");
        
            // create a secure connection to the server. machineName is the host running the server application
            var client = new TcpClient(targetHost, port);
            var connection = new Connection(client, targetHost, null, settings);
            
            Console.WriteLine("Client connected.");

            // enter an infinite loop to read and send text to the server
            while (true)
            {
                Console.WriteLine("Enter some text:");

                var input = Console.ReadLine();
                if (input == null || input.Length == 0)
                {
                    break;
                }

                connection.Debug(input);
            }

            // close the connection
            //await connection.DisconnectAsync();
            connection.Disconnect();
            client.Close();
            Console.WriteLine("Client closed.");
        }
        
        private static void DisplayUsage()
        {
            Console.WriteLine("Use:");
            Console.WriteLine("clientSync targetHost");
            System.Environment.Exit(1);
        }

        public static int Main(string[] args)
        {
            string? targetHost = null;

            if (args == null || args.Length < 1)
            {
                DisplayUsage();
                return 1;
            }

            // targetHost must match the name on the server's certificate
            targetHost = args[0];

            SslTcpClient.RunClient(targetHost, 8080);

            return 0;
        }
    }
}