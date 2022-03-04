using System.Net;
using System.Net.Sockets;

namespace Anywhere.Env
{
    public class Listener
    {

        public static void Go(int port = 11000)
        {
            Task.Run(() => Run(port));
        }

        public static void Run(int port = 11000)
        {
            //IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            //IPAddress ipAddress = ipHostInfo.AddressList[0];

            TcpListener listener = new TcpListener(IPAddress.Any, port);
            TcpClient client;
            //listener.Start();

            // TODO run this in another thread

            while (true) // TODO: add exit condition
            {
                listener.BeginAcceptTcpClient(
                    new AsyncCallback(DoAcceptTcpClientCallback),
                    listener);
                //client = listener.AcceptTcpClient();
                //ThreadPool.QueueUserWorkItem(DoWork, client);
            }
        }

        public static void DoAcceptTcpClientCallback(IAsyncResult ar)
        {
            // Get the listener that handles the client request.
            TcpListener listener = (TcpListener)ar.AsyncState;

            // End the operation and display the received data on
            // the console.
            TcpClient client = listener.EndAcceptTcpClient(ar);

            // Process the connection here. (Add the client to a
            // server table, read data, etc.)
            Console.WriteLine("Client connected completed");

            ThreadPool.QueueUserWorkItem(DoWork, client);

            // Signal the calling thread to continue.
            //tcpClientConnected.Set();
        }

        public void Close()
        {

        }

        internal static void DoWork(object obj)
        {
            var client = (TcpClient)obj;
            Console.WriteLine("Did work");
        }

        //public static void StartListening(int port = 11000)
        //{
        //    // create the local socket endpoint
        //    IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
        //    IPAddress ipAddress = ipHostInfo.AddressList[0];
        //    IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

        //    // create the socket 
        //    Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        //    // Bind the socket to the local endpoint and listen for incoming connections.  
        //    try
        //    {
        //        listener.Bind(localEndPoint);
        //        listener.Listen(100);

        //        while (true)
        //        {
        //            // Set the event to nonsignaled state.  
        //            allDone.Reset();

        //            // Start an asynchronous socket to listen for connections.  
        //            Console.WriteLine("Waiting for a connection...");
        //            listener.BeginAccept(
        //                new AsyncCallback(AcceptCallback),
        //                listener);

        //            // Wait until a connection is made before continuing.  
        //            allDone.WaitOne();
        //        }

        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine(e.ToString());
        //    }

        //    Console.WriteLine("\nPress ENTER to continue...");
        //    Console.Read();
        //}

        //public static void AcceptCallback(IAsyncResult ar)
        //{
        //    // Signal the main thread to continue.  
        //    allDone.Set();

        //    // Get the socket that handles the client request.  
        //    Socket listener = (Socket)ar.AsyncState;
        //    Socket handler = listener.EndAccept(ar);

        //    // Create the state object.  
        //    StateObject state = new StateObject();
        //    state.workSocket = handler;
        //    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
        //        new AsyncCallback(ReadCallback), state);
        //}

        //public static void ReadCallback(IAsyncResult ar)
        //{
        //    String content = String.Empty;

        //    // Retrieve the state object and the handler socket  
        //    // from the asynchronous state object.  
        //    StateObject state = (StateObject)ar.AsyncState;
        //    Socket handler = state.workSocket;

        //    // Read data from the client socket.
        //    int bytesRead = handler.EndReceive(ar);

        //    if (bytesRead > 0)
        //    {
        //        // There  might be more data, so store the data received so far.  
        //        state.sb.Append(Encoding.ASCII.GetString(
        //            state.buffer, 0, bytesRead));

        //        // Check for end-of-file tag. If it is not there, read
        //        // more data.  
        //        content = state.sb.ToString();
        //        if (content.IndexOf("<EOF>") > -1)
        //        {
        //            // All the data has been read from the
        //            // client. Display it on the console.  
        //            Console.WriteLine("Read {0} bytes from socket. \n Data : {1}",
        //                content.Length, content);
        //            // Echo the data back to the client.  
        //            Send(handler, content);
        //        }
        //        else
        //        {
        //            // Not all data received. Get more.  
        //            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
        //            new AsyncCallback(ReadCallback), state);
        //        }
        //    }
        //}

        //private static void Send(Socket handler, String data)
        //{
        //    // Convert the string data to byte data using ASCII encoding.  
        //    byte[] byteData = Encoding.ASCII.GetBytes(data);

        //    // Begin sending the data to the remote device.  
        //    handler.BeginSend(byteData, 0, byteData.Length, 0,
        //        new AsyncCallback(SendCallback), handler);
        //}

        //private static void SendCallback(IAsyncResult ar)
        //{
        //    try
        //    {
        //        // Retrieve the socket from the state object.  
        //        Socket handler = (Socket)ar.AsyncState;

        //        // Complete sending the data to the remote device.  
        //        int bytesSent = handler.EndSend(ar);
        //        Console.WriteLine("Sent {0} bytes to client.", bytesSent);

        //        handler.Shutdown(SocketShutdown.Both);
        //        handler.Close();

        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine(e.ToString());
        //    }
        //}
    }
}