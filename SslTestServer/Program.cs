using SslTestCommon;
using System.Net;
using System.Net.Security;
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

    // TODO: task timeout
    //internal static class TaskExtensions
    //{
    //    public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
    //    {
    //        if (task == await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false))
    //        {
    //            await task.ConfigureAwait(false);
    //        }
    //        else
    //        {
    //            Task supressErrorTask = task.ContinueWith((t, s) => t.Exception.Handle(e => true), null, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    //            throw new TimeoutException();
    //        }
    //    }
    //}

    public sealed class SslTcpServer
    {
        static X509Certificate2? serverCertificate = null;

        // The certificate parameter specifies the name of the file
        // containing the machine certificate.
        public static async Task RunServer(string pfxFile, string password)
        {
            var connections = new List<Connection>();

            //serverCertificate = X509Certificate.CreateFromCertFile(certificate);
            //serverCertificate = X509Certificate2.CreateFromPemFile(pemFile, keyFile);
            serverCertificate = new X509Certificate2(pfxFile, password);
            // Create a TCP/IP (IPv4) socket and listen for incoming connections.
            TcpListener listener = new TcpListener(IPAddress.Any, 8080);
            listener.Start();
            while (true)
            {
                Console.WriteLine("Waiting for a client to connect...");
                // Application blocks while waiting for an incoming connection.
                // Type CNTL-C to terminate the server.
                TcpClient client = listener.AcceptTcpClient();

                connections.Add(new Connection(client, serverCertificate));

                //Task.Run(() => ProcessClient(client));
            }
        }

        //static async Task Heartbeat(TcpClient client)

        //static async Task ProcessClient(TcpClient client)
        //{
        //    // A client has connected. Create the
        //    // SslStream using the client's network stream.
        //    SslStream sslStream = new SslStream(client.GetStream(), false);
        //    // Authenticate the server but don't require the client to authenticate.
        //    try
        //    {
        //        sslStream.AuthenticateAsServer(serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: true);

        //        // Display the properties and settings for the authenticated stream.
        //        DisplaySecurityLevel(sslStream);
        //        DisplaySecurityServices(sslStream);
        //        DisplayCertificateInformation(sslStream);
        //        DisplayStreamProperties(sslStream);

        //        // Set timeouts for the read and write to 5 seconds.
        //        //sslStream.ReadTimeout = 5000;
        //        //sslStream.WriteTimeout = 5000;

        //        while (true)
        //        {
        //            Console.WriteLine("----\nWaiting for client message...");

        //            // Read a message from the client.
        //            var frame = await sslStream.ReadFrame();
        //            //string messageData = ReadMessage(sslStream);
        //            //Console.WriteLine("Received: {0}", messageData);
        //            Console.WriteLine("Received: {0}", frame);

        //            // Write a message to the client.
        //            //byte[] message = Encoding.UTF8.GetBytes("ack");
        //            //Console.WriteLine("Sending ack message.");
        //            //sslStream.Write(message);

        //            byte[] messsage = Encoding.UTF8.GetBytes("ack");

        //            await sslStream.WriteFrame(new Frame
        //            {
        //                Type = 1,
        //                Channel = 1,
        //                Length = messsage.Length,
        //                Payload = messsage
        //            });
        //        }
        //    }
        //    catch (AuthenticationException e)
        //    {
        //        Console.WriteLine("Exception: {0}", e.Message);
        //        if (e.InnerException != null)
        //        {
        //            Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
        //        }
        //        Console.WriteLine("Authentication failed - closing the connection...");
        //    }
        //    catch (IOException e)
        //    {
        //        Console.WriteLine("Exception: {0}", e.Message);
        //        Console.WriteLine("Client disconnected - closing the connection...");
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine($"Unhandled Exception: {e.GetType()} {e.Message}");
        //    }
        //    finally
        //    {
        //        // The client stream will be closed with the sslStream
        //        // because we specified this behavior when creating
        //        // the sslStream.
        //        sslStream.Close();
        //        client.Close();
        //        Console.WriteLine("Closed client connection.");
        //    }
        //}

        //static string ReadMessage(SslStream sslStream)
        //{
        //    // Read the  message sent by the client.
        //    // The client signals the end of the message using the
        //    // "<EOF>" marker.
        //    byte[] buffer = new byte[2048];
        //    StringBuilder messageData = new StringBuilder();
        //    int bytes = -1;
        //    do
        //    {
        //        // Read the client's test message.
        //        bytes = sslStream.Read(buffer, 0, buffer.Length);

        //        // Use Decoder class to convert from bytes to UTF8
        //        // in case a character spans two buffers.
        //        Decoder decoder = Encoding.UTF8.GetDecoder();
        //        char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
        //        decoder.GetChars(buffer, 0, bytes, chars, 0);
        //        messageData.Append(chars);
        //        //// Check for EOF or an empty message.
        //        //if (messageData.ToString().IndexOf("<EOF>") != -1)
        //        //{
        //        //    break;
        //        //}
        //    } while (bytes != 0);

        //    return messageData.ToString();
        //}

        static void DisplaySecurityLevel(SslStream stream)
        {
            Console.WriteLine("Cipher: {0} strength {1}", stream.CipherAlgorithm, stream.CipherStrength);
            Console.WriteLine("Hash: {0} strength {1}", stream.HashAlgorithm, stream.HashStrength);
            Console.WriteLine("Key exchange: {0} strength {1}", stream.KeyExchangeAlgorithm, stream.KeyExchangeStrength);
            Console.WriteLine("Protocol: {0}", stream.SslProtocol);
        }
        static void DisplaySecurityServices(SslStream stream)
        {
            Console.WriteLine("Is authenticated: {0} as server? {1}", stream.IsAuthenticated, stream.IsServer);
            Console.WriteLine("IsSigned: {0}", stream.IsSigned);
            Console.WriteLine("Is Encrypted: {0}", stream.IsEncrypted);
        }
        static void DisplayStreamProperties(SslStream stream)
        {
            Console.WriteLine("Can read: {0}, write {1}", stream.CanRead, stream.CanWrite);
            Console.WriteLine("Can timeout: {0}", stream.CanTimeout);
        }
        static void DisplayCertificateInformation(SslStream stream)
        {
            Console.WriteLine("Certificate revocation list checked: {0}", stream.CheckCertRevocationStatus);

            X509Certificate localCertificate = stream.LocalCertificate;
            if (stream.LocalCertificate != null)
            {
                Console.WriteLine("Local cert was issued to {0} and is valid from {1} until {2}.",
                    localCertificate.Subject,
                    localCertificate.GetEffectiveDateString(),
                    localCertificate.GetExpirationDateString());
            }
            else
            {
                Console.WriteLine("Local certificate is null.");
            }
            // Display the properties of the client's certificate.
            X509Certificate remoteCertificate = stream.RemoteCertificate;
            if (stream.RemoteCertificate != null)
            {
                Console.WriteLine("Remote cert was issued to {0} and is valid from {1} until {2}.",
                    remoteCertificate.Subject,
                    remoteCertificate.GetEffectiveDateString(),
                    remoteCertificate.GetExpirationDateString());
            }
            else
            {
                Console.WriteLine("Remote certificate is null.");
            }
        }
        private static void DisplayUsage()
        {
            Console.WriteLine("To start the server specify:");
            Console.WriteLine("serverSync certificateFile.pfx password");
            Environment.Exit(1);
        }
        public static int Main(string[] args)
        {
            string? pfxFile = null;
            string? pass = null;
            if (args == null || args.Length < 2)
            {
                DisplayUsage();
                return 1;
            }

            pfxFile = args[0];
            pass = args[1];

            SslTcpServer.RunServer(pfxFile, pass).GetAwaiter().GetResult();

            return 0;
        }
    }
}