using AnywhereNET.Test.Common;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace AnywhereNET.Test
{
    public class ConnectionAndChannelTests
    {
        static long NextPort = 8000;

        /// <summary>
        /// Gets a unique port number so multiple client/server tests can run simultaneously.
        /// </summary>
        /// <returns></returns>
        internal static int GetNextAvailablePort()
        {
            return (int)Interlocked.Increment(ref NextPort);
        }

        public ConnectionAndChannelTests(ITestOutputHelper output)
        {
            var converter = new OutputConverter(output);
            Console.SetOut(converter);
        }

        [Fact]
        public async void ClientServerCommunication()
        {
            // create a local client/server system
            using (var clientServerConnection = await ClientServerConnection.CreateAsync(GetNextAvailablePort()))
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

        [Fact]
        public async void Channel()
        {
            // create a local client/server system
            using (var clientServerConnection = await ClientServerConnection.CreateAsync(GetNextAvailablePort()))
            {
                // create a logical channel for the client and server to communicate
                using (var channel1ClientSide = clientServerConnection.ClientConnection.GetChannel(1))
                using (var channel1ServerSide = clientServerConnection.ServerConnection.GetChannel(1))
                {
                    // indicate the channel should block reads until data is available
                    channel1ServerSide.BlockingReads = true;

                    // send a test message to the server
                    var testMessage = "hello world";
                    channel1ClientSide.WriteString(testMessage);

                    // wait until the server receives the message, then read it back
                    //await channel1ServerSide.WaitForDataAsync();
                    var receivedMessage = channel1ServerSide.ReadString();
                    Assert.Equal(testMessage, receivedMessage);
                }
            }
        }

        [Fact]
        public async void Channels()
        {
            // create a local client/server system
            using (var clientServerConnection = await ClientServerConnection.CreateAsync(GetNextAvailablePort()))
            {
                // create two logical channels for the client and server to communicate
                using (var channel1ClientSide = clientServerConnection.ClientConnection.GetChannel(1))
                using (var channel1ServerSide = clientServerConnection.ServerConnection.GetChannel(1))
                using (var channel2ClientSide = clientServerConnection.ClientConnection.GetChannel(2))
                using (var channel2ServerSide = clientServerConnection.ServerConnection.GetChannel(2))
                {
                    // send test messages on each channel
                    var test1_c2s = "test 1 - c2s";
                    var test1_s2c = "test 1 - s2c";
                    channel1ClientSide.WriteString(test1_c2s);
                    channel1ServerSide.WriteString(test1_s2c);
                    var test2_c2s = "test 2 - c2s";
                    var test2_s2c = "test 2 - s2c";
                    channel2ClientSide.WriteString(test2_c2s);
                    channel2ServerSide.WriteString(test2_s2c);

                    // await data received on all channels
                    Task.WaitAll(channel1ClientSide.WaitForDataAsync(),
                        channel1ServerSide.WaitForDataAsync(),
                        channel2ClientSide.WaitForDataAsync(),
                        channel2ServerSide.WaitForDataAsync());

                    // confirm all messages
                    Assert.Equal(test1_c2s, channel1ServerSide.ReadString());
                    Assert.Equal(test1_s2c, channel1ClientSide.ReadString());
                    Assert.Equal(test2_c2s, channel2ServerSide.ReadString());
                    Assert.Equal(test2_s2c, channel2ClientSide.ReadString());
                }
            }
        }

        [Fact]
        public async void BigFrames()
        {
            // create a local client/server system
            using (var clientServerConnection = await ClientServerConnection.CreateAsync(GetNextAvailablePort()))
            using (var channelClientSide = clientServerConnection.ClientConnection.GetChannel(1))
            using (var channelServerSide = clientServerConnection.ServerConnection.GetChannel(1))
            {
                int maxFrameSize = Frame.MaxFrameSize;

                // write enough data to generate 3 frames
                var data = new byte[2 * maxFrameSize + 1];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = (byte)i;
                }
                channelClientSide.Write(data, 0, data.Length);
        
                // read the data
                channelServerSide.BlockingReads = true;
                var resultData = channelServerSide.ReadBytes(data.Length);

                // verify the communication
                Assert.Equal(3, clientServerConnection.ClientTransmittedFrames.Count);
                Assert.Equal(3, clientServerConnection.ServerRecievedFrames.Count);
                Assert.True(Enumerable.SequenceEqual(data, resultData));
            }
        }

        [Fact]
        public async void MessageChannel()
        {
            // create a local client/server system
            using (var clientServerConnection = await ClientServerConnection.CreateAsync(GetNextAvailablePort()))
            {
                // create both sides of a logical channel for the client and server to communicate
                var channel1ClientSide = clientServerConnection.ClientConnection.GetChannel(1);
                var channel1ServerSide = clientServerConnection.ServerConnection.GetChannel(1);

                // wrap in a message channel
                var messageChannel1ClientSide = new MessageChannel(channel1ClientSide);
                var messageChannel1ServerSide = new MessageChannel(channel1ServerSide);

                // create a test message to send to the server from the client
                var testMessage = new TestMessage { MyIntValue = 123, MyStringValue = "hello world" };

                // create a reset event to wait until the server receives the message
                var reset = new AutoResetEvent(false);

                // set a handler to receive messages on the server
                TestMessage? receivedMessage = null;
                messageChannel1ServerSide.OnMessageReceived += (message, channel) =>
                {
                    // the underlying type of the received message should be the same,
                    // so simply cast it
                    receivedMessage = message as TestMessage;
                    // signal the main thread to continue
                    reset.Set();
                };

                // send a test message to the server
                messageChannel1ClientSide.Send(testMessage);

                // block here until the message is received
                reset.WaitOne();

                // confirm the messages match
                Assert.NotNull(receivedMessage);
                Assert.Equal(testMessage.MyIntValue, receivedMessage?.MyIntValue);
                Assert.Equal(testMessage.MyStringValue, receivedMessage?.MyStringValue);
            }
        }

        class TestMessage : IMessage
        {
            public int MyIntValue { get; set; }

            public string MyStringValue { get; set; }

            public void Read(Stream stream)
            {
                MyIntValue = stream.ReadInt32BE();
                MyStringValue = stream.ReadString();
            }

            public void Write(Stream stream)
            {
                stream.WriteInt32BE(MyIntValue);
                stream.WriteString(MyStringValue);
            }
        }
    }
}