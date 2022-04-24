using DidoNet.Test.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DidoNet.Test
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
            var converter = new OutputConverter(output);//, "OUTPUT.txt");
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
        public async void Disconnect()
        {
            // create a local client/server system
            using (var clientServerConnection = await ClientServerConnection.CreateAsync(GetNextAvailablePort()))
            {
                // disconnect the server explicitly and confirm the client gets implicitly disconnected
                clientServerConnection.ServerConnection.Disconnect();
                var now = DateTime.UtcNow;
                while (clientServerConnection.ClientConnection.IsConnected)
                {
                    Thread.Sleep(1);
                    if ((DateTime.UtcNow - now) >= TimeSpan.FromSeconds(1))
                    {
                        throw new TimeoutException("Connection did not terminate within the alotted time.");
                    }
                }
            }

            // repeat the test from the other direction
            using (var clientServerConnection = await ClientServerConnection.CreateAsync(GetNextAvailablePort()))
            {
                clientServerConnection.ClientConnection.Disconnect();
                var now = DateTime.UtcNow;
                while (clientServerConnection.ServerConnection.IsConnected)
                {
                    Thread.Sleep(1);
                    if ((DateTime.UtcNow - now) >= TimeSpan.FromSeconds(1))
                    {
                        throw new TimeoutException("Connection did not terminate within the alotted time.");
                    }
                }
            }
        }

        [Fact]
        public async void Channel()
        {
            // create a local client/server system
            using (var clientServerConnection = await ClientServerConnection.CreateAsync(GetNextAvailablePort()))
            {
                // create both sides of a logical channel for the client and server to communicate
                var channel1ClientSide = clientServerConnection.ClientConnection.GetChannel(1);
                var channel1ServerSide = clientServerConnection.ServerConnection.GetChannel(1);

                // indicate the channel should block reads until data is available
                channel1ServerSide.BlockingReads = true;

                // send a test message to the server
                var testMessage = "hello world";
                channel1ClientSide.WriteString(testMessage);

                // wait until the server receives the message, then read it back
                var receivedMessage = channel1ServerSide.ReadString();
                Assert.Equal(testMessage, receivedMessage);
            }
        }

        [Fact]
        public async void Channels()
        {
            // create a local client/server system
            using (var clientServerConnection = await ClientServerConnection.CreateAsync(GetNextAvailablePort()))
            {
                // create both sides of two logical channels for the client and server to communicate
                var channel1ClientSide = clientServerConnection.ClientConnection.GetChannel(1);
                var channel1ServerSide = clientServerConnection.ServerConnection.GetChannel(1);
                var channel2ClientSide = clientServerConnection.ClientConnection.GetChannel(2);
                var channel2ServerSide = clientServerConnection.ServerConnection.GetChannel(2);

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

        [Fact]
        public async void BigFrames()
        {
            // create a local client/server system
            using (var clientServerConnection = await ClientServerConnection.CreateAsync(GetNextAvailablePort()))
            {
                // create both sides of a logical channel for the client and server to communicate
                var channelClientSide = clientServerConnection.ClientConnection.GetChannel(1);
                var channelServerSide = clientServerConnection.ServerConnection.GetChannel(1);

                // write enough data to generate 3 frames
                var data = new byte[2 * Frame.MaxFrameSize + 1];
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
                messageChannel1ServerSide.OnMessageReceived = (message, channel) =>
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

        [Fact]
        public async void MessageChannels()
        {
            // create a local client/server system
            using (var clientServerConnection = await ClientServerConnection.CreateAsync(GetNextAvailablePort()))
            {
                // create both sides of 2 logical channels for the client and server to communicate
                var channel1ClientSide = new MessageChannel(clientServerConnection.ClientConnection, 1);
                var channel1ServerSide = new MessageChannel(clientServerConnection.ServerConnection, 1);
                var channel2ClientSide = new MessageChannel(clientServerConnection.ClientConnection, 2);
                var channel2ServerSide = new MessageChannel(clientServerConnection.ServerConnection, 2);

                // create test messages
                var testRequestMessage = new TestMessage { MyIntValue = 123, MyStringValue = "my request" };
                var testResponseMessage = new TestMessage { MyIntValue = 456, MyStringValue = "my response" };

                // storage for all received messages
                var clientMessages = new ConcurrentBag<Tuple<TestMessage, int>>();
                var serverMessages = new ConcurrentBag<Tuple<TestMessage, int>>();

                long messageCount = 0;

                // message handlers
                MessageChannel.MessageReceivedHandler serverHandler = (message, channel) =>
                {
                    serverMessages.Add(new Tuple<TestMessage, int>(message as TestMessage, channel.ChannelNumber));
                    channel.Send(testResponseMessage);
                    Interlocked.Increment(ref messageCount);
                };
                MessageChannel.MessageReceivedHandler clientHandler = (message, channel) =>
                {
                    clientMessages.Add(new Tuple<TestMessage, int>(message as TestMessage, channel.ChannelNumber));
                    Interlocked.Increment(ref messageCount);
                };

                // set handlers
                channel1ClientSide.OnMessageReceived = clientHandler;
                channel2ClientSide.OnMessageReceived = clientHandler;
                channel1ServerSide.OnMessageReceived = serverHandler;
                channel2ServerSide.OnMessageReceived = serverHandler;

                // send messages
                channel1ClientSide.Send(testRequestMessage);
                channel2ClientSide.Send(testRequestMessage);

                // block here until all messages have been exchanged
                while (Interlocked.Read(ref messageCount) != 4)
                {
                    Thread.Sleep(1);
                }

                // confirm the messages match
                Assert.Equal(2, clientMessages.Count);
                Assert.Equal(2, serverMessages.Count);
                Assert.Contains(1, clientMessages.Select(x => x.Item2));
                Assert.Contains(2, clientMessages.Select(x => x.Item2));
                Assert.Contains(1, serverMessages.Select(x => x.Item2));
                Assert.Contains(2, serverMessages.Select(x => x.Item2));
                Assert.All(clientMessages.Select(x => x.Item1), m => Assert.Equal(m.MyIntValue, testResponseMessage.MyIntValue));
                Assert.All(clientMessages.Select(x => x.Item1), m => Assert.Equal(m.MyStringValue, testResponseMessage.MyStringValue));
                Assert.All(serverMessages.Select(x => x.Item1), m => Assert.Equal(m.MyIntValue, testRequestMessage.MyIntValue));
                Assert.All(serverMessages.Select(x => x.Item1), m => Assert.Equal(m.MyStringValue, testRequestMessage.MyStringValue));
            }
        }

        [Fact]
        public async void MessageProducerConsumers()
        {
            // ordered storage for all sent and received messages
            var clientSentMessages = new ConcurrentQueue<Tuple<TestMessage, int>>();
            var clientReceivedMessages = new ConcurrentQueue<Tuple<TestMessage, int>>();
            var serverSentMessages = new ConcurrentQueue<Tuple<TestMessage, int>>();
            var serverReceivedMessages = new ConcurrentQueue<Tuple<TestMessage, int>>();

            // how many messages to send
            var rand = new Random();
            int channel1ClientNumMessages = rand.Next(10, 30);
            int channel1ServerNumMessages = rand.Next(10, 30);
            int channel2ClientNumMessages = rand.Next(10, 30);
            int channel2ServerNumMessages = rand.Next(10, 30);

            // create a local client/server system
            using (var clientServerConnection = await ClientServerConnection.CreateAsync(GetNextAvailablePort()))
            {
                // create both sides of 2 logical channels for the client and server to communicate
                var channel1ClientSide = new MessageChannel(clientServerConnection.ClientConnection, 1);
                var channel1ServerSide = new MessageChannel(clientServerConnection.ServerConnection, 1);
                var channel2ClientSide = new MessageChannel(clientServerConnection.ClientConnection, 2);
                var channel2ServerSide = new MessageChannel(clientServerConnection.ServerConnection, 2);

                // create message handlers to track received messages
                MessageChannel.MessageReceivedHandler serverHandler = (message, channel) =>
                {
                    serverReceivedMessages.Enqueue(new Tuple<TestMessage, int>((TestMessage)message, channel.ChannelNumber));
                };
                MessageChannel.MessageReceivedHandler clientHandler = (message, channel) =>
                {
                    clientReceivedMessages.Enqueue(new Tuple<TestMessage, int>((TestMessage)message, channel.ChannelNumber));
                };
                channel1ClientSide.OnMessageReceived = clientHandler;
                channel2ClientSide.OnMessageReceived = clientHandler;
                channel1ServerSide.OnMessageReceived = serverHandler;
                channel2ServerSide.OnMessageReceived = serverHandler;

                // create an action to send and track random messages
                var producerAction = (int numMessages, MessageChannel channel, ConcurrentQueue<Tuple<TestMessage, int>> queue) =>
                {
                    var rand = new Random();
                    for (int i = 0; i < numMessages; ++i)
                    {
                        Thread.Sleep(rand.Next(5, 50));
                        var message = new TestMessage { MyIntValue = rand.Next(), MyStringValue = Guid.NewGuid().ToString() };
                        queue.Enqueue(new Tuple<TestMessage, int>(message, channel.ChannelNumber));
                        channel.Send(message);
                    }
                };

                // start threads to randomly produce messages
                var producerTasks = new Task[]
                {
                    Task.Run(() => producerAction(channel1ClientNumMessages, channel1ClientSide, clientSentMessages)),
                    Task.Run(() => producerAction(channel1ServerNumMessages, channel1ServerSide, serverSentMessages)),
                    Task.Run(() => producerAction(channel2ClientNumMessages, channel2ClientSide, clientSentMessages)),
                    Task.Run(() => producerAction(channel2ServerNumMessages, channel2ServerSide, serverSentMessages)),
                };

                // wait for all producer threads to finish
                Task.WaitAll(producerTasks);

                // wait for all consumers to finish receiving all messages
                while (serverReceivedMessages.Count != clientSentMessages.Count &&
                    clientReceivedMessages.Count != serverSentMessages.Count)
                {
                    Thread.Sleep(1);
                }
            }

            // confirm total message counts
            Assert.Equal(channel1ClientNumMessages + channel2ClientNumMessages, clientSentMessages.Count);
            Assert.Equal(channel1ServerNumMessages + channel2ServerNumMessages, serverSentMessages.Count);

            var confirmMessageOrder = (List<TestMessage> sentMessages, List<TestMessage> receivedMessages) =>
            {
                Assert.Equal(sentMessages.Count, receivedMessages.Count);
                for (int i = 0; i < sentMessages.Count; ++i)
                {
                    Assert.Equal(sentMessages[i].MyStringValue, receivedMessages[i].MyStringValue);
                    Assert.Equal(sentMessages[i].MyIntValue, receivedMessages[i].MyIntValue);
                }
            };

            // confirm message order
            confirmMessageOrder(
                clientSentMessages.Where(x => x.Item2 == 1).Select(x => x.Item1).ToList(), // channel1ClientSent
                serverReceivedMessages.Where(x => x.Item2 == 1).Select(x => x.Item1).ToList() // channel1ServerReceived
            );
            confirmMessageOrder(
                serverSentMessages.Where(x => x.Item2 == 1).Select(x => x.Item1).ToList(), // channel1ServerSent
                clientReceivedMessages.Where(x => x.Item2 == 1).Select(x => x.Item1).ToList() // channel1ClientReceived
            );
            confirmMessageOrder(
                clientSentMessages.Where(x => x.Item2 == 2).Select(x => x.Item1).ToList(), // channel2ClientSent
                serverReceivedMessages.Where(x => x.Item2 == 2).Select(x => x.Item1).ToList() // channel2ServerReceived
            );
            confirmMessageOrder(
                serverSentMessages.Where(x => x.Item2 == 2).Select(x => x.Item1).ToList(), // channel2ServerSent
                clientReceivedMessages.Where(x => x.Item2 == 2).Select(x => x.Item1).ToList() // channel2ClientReceived
            );
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