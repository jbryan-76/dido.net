using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DidoNet.Test
{
    public class MessageTests
    {
        [Fact]
        public void RunnerStartMessage()
        {
            using (var stream = new MemoryStream())
            {
                var fakeEndpoint = new UriBuilder("https", "localhost", 1234).Uri.ToString();
                var tx = new RunnerStartMessage(fakeEndpoint, 4, 5, "label", new string[] { "tag1", "tag2", "tag3" });
                tx.Write(stream);
                stream.Position = 0;
                var rx = new RunnerStartMessage();
                rx.Read(stream);
                Assert.Equal(tx.Platform, rx.Platform);
                Assert.Equal(tx.OSVersion, rx.OSVersion);
                Assert.Equal(tx.Endpoint, rx.Endpoint);
                Assert.Equal(tx.MaxTasks, rx.MaxTasks);
                Assert.Equal(tx.MaxQueue, rx.MaxQueue);
                Assert.Equal(tx.Label, rx.Label);
                Assert.True(Enumerable.SequenceEqual(tx.Tags, rx.Tags));
            }
        }

        [Fact]
        public void RunnerStatusMessage()
        {
            using (var stream = new MemoryStream())
            {
                var tx = new RunnerStatusMessage(RunnerStates.Ready, 3, 5);
                tx.Write(stream);
                stream.Position = 0;
                var rx = new RunnerStatusMessage();
                rx.Read(stream);
                Assert.Equal(tx.State, rx.State);
                Assert.Equal(tx.ActiveTasks, rx.ActiveTasks);
                Assert.Equal(tx.QueueLength, rx.QueueLength);
            }
        }

        // TODO: test remaining messages
    }
}