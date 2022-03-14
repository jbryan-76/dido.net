using SslTestCommon;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace foo
{
    // 1) run
    // ./SslTestClient.exe localhost

    public class SslTcpClient
    {
        // The following method is invoked by the RemoteCertificateValidationDelegate.
        public static bool ValidateServerCertificate(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            Console.WriteLine("Validating server certificate...");
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            // for self-signed, just return true
            // TODO: make all this better for production use
            return true;

            Console.WriteLine("Certificate error: {0}", sslPolicyErrors);

            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }

        public static async Task RunClient(string machineName, string serverName)
        {
            Console.WriteLine($"Connecting to {machineName}, {serverName}...");
            // Create a TCP/IP client socket.
            // machineName is the host running the server application.
            TcpClient client = new TcpClient(machineName, 8080);
            Console.WriteLine("Client connected.");
            // Create an SSL stream that will close the client's stream.
            SslStream sslStream = new SslStream(
                client.GetStream(),
                false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate),
                null
                );
            // The server name must match the name on the server certificate.
            try
            {
                Console.WriteLine("Authenticating to host {0}.", serverName);
                sslStream.AuthenticateAsClient(serverName);
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }
                Console.WriteLine("Authentication failed - closing the connection.");
                client.Close();
                return;
            }

            while (true)
            {
                Console.WriteLine("Enter some text:");

                var input = Console.ReadLine();
                Console.WriteLine(String.Join(" ", Encoding.UTF8.GetBytes(input)));

                // Encode a test message into a byte array.
                // Signal the end of the message using the "<EOF>".
                byte[] messsage = Encoding.UTF8.GetBytes(input);

                if (messsage.Length == 0)
                {
                    break;
                }

                await sslStream.WriteFrame(new Frame
                {
                    Type = 1,
                    Channel = 1,
                    Length = messsage.Length,
                    Payload = messsage
                });

                // Send hello message to the server.
                //sslStream.Write(messsage);
                //sslStream.Flush();
                // Read message from the server.
                //string serverMessage = ReadMessage(sslStream);
                var frame = await sslStream.ReadFrame();

                //Console.WriteLine("Server says: {0}", serverMessage);
                Console.WriteLine("Server says: {0}", frame);
            }

            // Close the client connection.
            client.Close();
            Console.WriteLine("Client closed.");
        }
        static string ReadMessage(SslStream sslStream)
        {
            // Read the  message sent by the server.
            // The end of the message is signaled using the
            // "<EOF>" marker.
            byte[] buffer = new byte[2048];
            StringBuilder messageData = new StringBuilder();
            int bytes = -1;
            do
            {
                bytes = sslStream.Read(buffer, 0, buffer.Length);

                // Use Decoder class to convert from bytes to UTF8
                // in case a character spans two buffers.
                Decoder decoder = Encoding.UTF8.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);
                // Check for EOF.
                if (messageData.ToString().IndexOf("<EOF>") != -1)
                {
                    break;
                }
            } while (bytes != 0);

            return messageData.ToString();
        }
        private static void DisplayUsage()
        {
            Console.WriteLine("To start the client specify:");
            Console.WriteLine("clientSync machineName [serverName]");
            Environment.Exit(1);
        }

        public static int Main(string[] args)
        {
            string serverCertificateName = null;
            string machineName = null;
            if (args == null || args.Length < 1)
            {
                DisplayUsage();
            }
            // User can specify the machine name and server name.
            // Server name must match the name on the server's certificate.
            machineName = args[0];
            if (args.Length < 2)
            {
                serverCertificateName = machineName;
            }
            else
            {
                serverCertificateName = args[1];
            }

            SslTcpClient.RunClient(machineName, serverCertificateName).GetAwaiter().GetResult();
            
            return 0;
        }
    }
}