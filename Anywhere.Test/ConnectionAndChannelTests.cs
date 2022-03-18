using System;
using System.Linq;
using System.Text;
using Xunit;

namespace AnywhereNET.Test
{
    public class ConnectionAndChannelTests
    {
        [Fact]
        public async void ConfirmClientServerCommunication()
        {
            // create a local client/server system
            var port = 8080;
            var clientServerConnection = await ClientServerConnection.CreateAsync(port);

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
            await clientServerConnection.CloseAsync();
        }
    }
}