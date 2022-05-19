using Dido.Utilities;
using DidoNet.IO;
using System.IO;
using System.Linq;
using Xunit;

namespace DidoNet.Test
{
    public class ProxyFileTests
    {
        [Fact]
        public void OpenNewRemoteFile()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var tempFile = new TemporaryFile())
            {
                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                NewFile(tempFile.Filename, runnerLoopbackConnection);
                // ensure all files are closed before deleting the temp file
                while (ioProxy.Files.Any())
                {
                    ThreadHelpers.Yield();
                }
            }
        }

        [Fact]
        public void OpenNewLocalFile()
        {
            using (var tempFile = new TemporaryFile())
            {
                NewFile(tempFile.Filename, null);
            }
        }

        [Fact]
        public void OpenExistingRemoteFile()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var tempFile = new TemporaryFile())
            {
                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                ExistingFile(tempFile.Filename, runnerLoopbackConnection);
                // ensure all files are closed before deleting the temp file
                while (ioProxy.Files.Any())
                {
                    ThreadHelpers.Yield();
                }
            }
        }

        [Fact]
        public void OpenExistingLocalFile()
        {
            using (var tempFile = new TemporaryFile())
            {
                ExistingFile(tempFile.Filename, null);
            }
        }

        void NewFile(string filename, Connection? connection)
        {
            var proxy = new RunnerFileProxy(connection);
            byte[] data;
            using (var stream = proxy.Open(filename, FileMode.Create))
            {
                data = WriteRandomData(stream);
            }
            using (var stream = proxy.Open(filename, FileMode.Open))
            {
                var target = ReadAllBytes(stream);
                Assert.True(Enumerable.SequenceEqual(data, target));
            }
        }

        void ExistingFile(string filename, Connection? connection)
        {
            byte[] data;
            using (var file = File.Create(filename))
            {
                data = WriteRandomData(file);
            }
            var proxy = new RunnerFileProxy(connection);
            using (var stream = proxy.Open(filename, FileMode.Open))
            {
                var target = ReadAllBytes(stream);
                Assert.True(Enumerable.SequenceEqual(data, target));
            }
        }

        byte[] WriteRandomData(Stream stream)
        {
            var rand = new System.Random();
            var data = Enumerable.Range(0, 64).Select(x => (byte)rand.Next(256)).ToArray();
            stream.Write(data);
            return data;
        }

        byte[] ReadAllBytes(Stream stream)
        {
            var data = new byte[stream.Length];
            int count = stream.Read(data);
            return data;
        }
    }
}