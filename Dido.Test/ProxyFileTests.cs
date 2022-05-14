using Xunit;

namespace DidoNet.Test
{
    public class ProxyFileTests
    {
        [Fact]
        public void Foo()
        {
            var loopbackConnection = new Connection();
            var filesChannel = new MessageChannel(loopbackConnection, Constants.FileChannelNumber);
            var file = new IO.ProxyFile(filesChannel);
            file.Open()
        }

    }
}