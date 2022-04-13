using System.IO;
using System.Linq;
using Xunit;

namespace AnywhereNET.Test
{
    public class MessageTests
    {
        [Fact]
        public void RunnerStatusMessage()
        {
            using (var stream = new MemoryStream())
            {
                var tx = new RunnerStatusMessage(AnywhereNET.RunnerStatusMessage.Statuses.Starting,
                    3, 5, "label", new string[] { "tag1", "tag2", "tag3" });
                tx.Write(stream);
                stream.Position = 0;
                var rx = new RunnerStatusMessage();
                rx.Read(stream);
                Assert.Equal(tx.Status, rx.Status);
                Assert.Equal(tx.Platform, rx.Platform);
                Assert.Equal(tx.OSVersion, rx.OSVersion);
                Assert.Equal(tx.AvailableSlots, rx.AvailableSlots);
                Assert.Equal(tx.QueueLength, rx.QueueLength);
                Assert.Equal(tx.Label, rx.Label);
                Assert.True(Enumerable.SequenceEqual(tx.Tags, rx.Tags));
            }
        }

        // TODO: test remaining messages
    }
}