using Dido.Utilities;
using DidoNet.IO;
using System.IO;
using System.Linq;
using Xunit;

namespace DidoNet.Test
{
    public class ProxyFileTests
    {
        //[Fact]
        public void OpenNewRemoteFile()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var ioProxy = new ApplicationIOProxy(appLoopbackConnection))
            {
                NewFile(runnerLoopbackConnection);
            }
        }

        [Fact]
        public void OpenNewLocalFile()
        {
            NewFile(null);
        }

        //[Fact]
        public void OpenExistingRemoteFile()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var ioProxy = new ApplicationIOProxy(appLoopbackConnection))
            {
                ExistingFile(runnerLoopbackConnection);
            }
        }

        [Fact]
        public void OpenExistingLocalFile()
        {
            ExistingFile(null);
        }

        public void NewFile(Connection? connection = null)
        {
            using (var tempFile = new TemporaryFile())
            {
                var proxy = new RunnerFileProxy(connection);
                byte[] data;
                using (var stream = proxy.Open(tempFile.Filename, FileMode.Create))
                {
                    data = WriteRandomData(stream);
                }
                using (var stream = proxy.Open(tempFile.Filename, FileMode.Open))
                {
                    var target = ReadAllBytes(stream);
                    Assert.True(Enumerable.SequenceEqual(data, target));
                }
            }
        }

        public void ExistingFile(Connection? connection = null)
        {
            using (var tempFile = new TemporaryFile())
            {
                byte[] data;
                using (var file = File.Create(tempFile.Filename))
                {
                    data = WriteRandomData(file);
                }
                var proxy = new RunnerFileProxy(connection);
                using (var stream = proxy.Open(tempFile.Filename, FileMode.Open))
                {
                    var target = ReadAllBytes(stream);
                    Assert.True(Enumerable.SequenceEqual(data, target));
                }
            }
        }

        internal byte[] WriteRandomData(Stream stream)
        {
            var rand = new System.Random();
            var data = Enumerable.Range(0, 64).Select(x => (byte)rand.Next(256)).ToArray();
            stream.Write(data);
            return data;
        }

        internal byte[] ReadAllBytes(Stream stream)
        {
            var data = new byte[stream.Length];
            int count = stream.Read(data);
            return data;
        }
    }
}