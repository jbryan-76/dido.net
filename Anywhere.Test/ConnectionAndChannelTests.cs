using System;
using System.IO;
using System.Text;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace AnywhereNET.Test
{
    public class ConnectionAndChannelTests
    {
        public ConnectionAndChannelTests(ITestOutputHelper output)
        {
            var converter = new Converter(output);
            Console.SetOut(converter);
        }

        [Fact]
        public async void ClientServerCommunication()
        {
            // create a local client/server system
            var port = 8080;
            using (var clientServerConnection = await ClientServerConnection.CreateAsync(port))
            {
                // send a test frame from the client to the server
                var testFrame = new DebugFrame("hello world");
                testFrame.Channel = 123;
                clientServerConnection.SendClientToServer(testFrame);

                // verify the communication
                Assert.True(clientServerConnection.ClientTransmittedFrames.TryDequeue(out var actualTransmittedFrame));
                Assert.True(clientServerConnection.ServerRecievedFrames.TryDequeue(out var actualReceivedFrame));
                Assert.Equal(testFrame.Channel, actualTransmittedFrame!.Channel);
                Assert.Equal(testFrame.Channel, actualReceivedFrame!.Channel);
                Assert.Equal(testFrame.Length, actualTransmittedFrame.Length);
                Assert.Equal(testFrame.Length, actualReceivedFrame.Length);
                Assert.Equal(testFrame.FrameType, actualTransmittedFrame.FrameType);
                Assert.Equal(testFrame.FrameType, actualReceivedFrame.FrameType);
                Assert.Equal(testFrame.Message, Encoding.UTF8.GetString(actualTransmittedFrame.Payload));
                Assert.Equal(testFrame.Message, Encoding.UTF8.GetString(actualReceivedFrame.Payload));

                // send a test frame from the server to the client
                testFrame = new DebugFrame("goodbye cruel world");
                testFrame.Channel = 456;
                clientServerConnection.SendServerToClient(testFrame);

                // verify the communication
                Assert.True(clientServerConnection.ServerTransmittedFrames.TryDequeue(out actualTransmittedFrame));
                Assert.True(clientServerConnection.ClientRecievedFrames.TryDequeue(out actualReceivedFrame));
                Assert.Equal(testFrame.Channel, actualTransmittedFrame!.Channel);
                Assert.Equal(testFrame.Channel, actualReceivedFrame!.Channel);
                Assert.Equal(testFrame.Length, actualTransmittedFrame.Length);
                Assert.Equal(testFrame.Length, actualReceivedFrame.Length);
                Assert.Equal(testFrame.FrameType, actualTransmittedFrame.FrameType);
                Assert.Equal(testFrame.FrameType, actualReceivedFrame.FrameType);
                Assert.Equal(testFrame.Message, Encoding.UTF8.GetString(actualTransmittedFrame.Payload));
                Assert.Equal(testFrame.Message, Encoding.UTF8.GetString(actualReceivedFrame.Payload));

                // cleanup
                clientServerConnection.Close();
            }
        }

        private class Converter : TextWriter
        {
            ITestOutputHelper _output;
            public Converter(ITestOutputHelper output)
            {
                _output = output;
            }
            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }
            public override void WriteLine(string message)
            {
                _output.WriteLine(message);
            }
            public override void WriteLine(string format, params object[] args)
            {
                _output.WriteLine(format, args);
            }

            public override void Write(char value)
            {
                throw new NotSupportedException("This text writer only supports WriteLine(string) and WriteLine(string, params object[]).");
            }
        }

        [Fact]
        public async void Channels()
        {
            // create a local client/server system
            var port = 8081;
            using (var clientServerConnection = await ClientServerConnection.CreateAsync(port))
            {

                // create two logical channels for the client and server to communicate
                using (var channel1ClientSide = clientServerConnection.ClientConnection.GetChannel(1))
                using (var channel1ServerSide = clientServerConnection.ServerConnection.GetChannel(1))
                //var channel2ClientSide = clientServerConnection.ClientConnection.GetChannel(2);
                //var channel2ServerSide = clientServerConnection.ServerConnection.GetChannel(2);
                {
                    var testMessage = "hello world";
                    channel1ClientSide.WriteString(testMessage);

                    // block until something has been received
                    Console.WriteLine("start waiting");
                    await channel1ServerSide.WaitForDataAsync();
                    //while (!channel1ServerSide.IsDataAvailable)
                    //{
                    //    Thread.Sleep(1);

                    //    if (!channel1ServerSide.IsConnected)
                    //    {
                    //        throw new InvalidOperationException("Channel disconnected");
                    //    }
                    //}
                    Console.WriteLine("got some data");
                    ////channel1ServerSide.OnDataAvailable
                    ////await Task.Run(() =>
                    ////{

                    ////});
                    var receivedMessage = channel1ServerSide.ReadString();
                    Assert.Equal(testMessage, receivedMessage);
                    Console.WriteLine($"Confirmed: message {receivedMessage} matches");
                }

                // cleanup
                Console.WriteLine("starting connection close");
                clientServerConnection.Close();
            }
        }
    }
}