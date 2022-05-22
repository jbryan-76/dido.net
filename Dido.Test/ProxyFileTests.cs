using Dido.Utilities;
using DidoNet.IO;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace DidoNet.Test
{
    public class ProxyFileTests
    {
        [Fact]
        public void AppendAllText()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var localFile = new TemporaryFile())
            using (var remoteFile = new TemporaryFile())
            {
                AppendAllTextInternal(localFile.Filename);

                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                AppendAllTextInternal(remoteFile.Filename, runnerLoopbackConnection, ioProxy);
                WaitForProxyToFinish(ioProxy);

                AssertFilesEqual(localFile.Filename, remoteFile.Filename);
            }
        }

        [Fact]
        public void AppendAllLines()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var localFile = new TemporaryFile())
            using (var remoteFile = new TemporaryFile())
            {
                AppendAllLinesInternal(localFile.Filename);

                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                AppendAllLinesInternal(remoteFile.Filename, runnerLoopbackConnection, ioProxy);
                WaitForProxyToFinish(ioProxy);

                AssertFilesEqual(localFile.Filename, remoteFile.Filename);
            }
        }

        [Fact]
        public void AppendText()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var localFile = new TemporaryFile())
            using (var remoteFile = new TemporaryFile())
            {
                AppendTextInternal(localFile.Filename);

                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                AppendTextInternal(remoteFile.Filename, runnerLoopbackConnection, ioProxy);
                WaitForProxyToFinish(ioProxy);

                AssertFilesEqual(localFile.Filename, remoteFile.Filename);
            }
        }

        [Fact]
        public void Create()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var localFile = new TemporaryFile())
            using (var remoteFile = new TemporaryFile())
            {
                CreateInternal(localFile.Filename, null);

                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                CreateInternal(remoteFile.Filename, runnerLoopbackConnection);
                WaitForProxyToFinish(ioProxy);

                AssertFilesEqual(localFile.Filename, remoteFile.Filename);
            }
        }

        [Fact]
        public void OpenNew()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var localFile = new TemporaryFile())
            using (var remoteFile = new TemporaryFile())
            {
                OpenNew(localFile.Filename, null);

                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                OpenNew(remoteFile.Filename, runnerLoopbackConnection);
                WaitForProxyToFinish(ioProxy);

                AssertFilesEqual(localFile.Filename, remoteFile.Filename);
            }
        }

        [Fact]
        public void OpenExisting()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var localFile = new TemporaryFile())
            using (var remoteFile = new TemporaryFile())
            {
                OpenExistingInternal(localFile.Filename, null);

                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                OpenExistingInternal(remoteFile.Filename, runnerLoopbackConnection);
                WaitForProxyToFinish(ioProxy);

                AssertFilesEqual(localFile.Filename, remoteFile.Filename);
            }
        }

        [Fact]
        public void ReadLines()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var localFile = new TemporaryFile())
            using (var remoteFile = new TemporaryFile())
            {
                ReadLinesInternal(localFile.Filename, null);

                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                ReadLinesInternal(remoteFile.Filename, runnerLoopbackConnection);
                WaitForProxyToFinish(ioProxy);

                AssertFilesEqual(localFile.Filename, remoteFile.Filename);
            }
        }

        [Fact]
        public void WriteAllTextRemote()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var tempFile = new TemporaryFile())
            {
                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                WriteAllText(tempFile.Filename, runnerLoopbackConnection);
                WaitForProxyToFinish(ioProxy);
            }
        }

        [Fact]
        public void WriteAllTextLocal()
        {
            using (var tempFile = new TemporaryFile())
            {
                WriteAllText(tempFile.Filename, null);
            }
        }

        [Fact]
        public void WriteAllBytesRemote()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var tempFile = new TemporaryFile())
            {
                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                WriteAllBytes(tempFile.Filename, runnerLoopbackConnection);
                WaitForProxyToFinish(ioProxy);
            }
        }

        [Fact]
        public void WriteAllBytesLocal()
        {
            using (var tempFile = new TemporaryFile())
            {
                WriteAllBytes(tempFile.Filename, null);
            }
        }

        void AppendAllTextInternal(string filename, Connection? connection = null, ApplicationIOProxy? ioProxy = null)
        {
            var proxy = new RunnerFileProxy(connection);
            // create a new file and write some text
            var expected = RandomString(32, 0);
            proxy.WriteAllText(filename, expected);
            // wait for the write to complete
            if (ioProxy != null) { WaitForProxyToFinish(ioProxy); }
            // now append some more text
            var additional = RandomString(32, 1);
            proxy.AppendAllText(filename, additional);
            expected += additional;
            // now confirm
            var actual = proxy.ReadAllText(filename);
            Assert.Equal(expected, actual);
        }

        void AppendAllLinesInternal(string filename, Connection? connection = null, ApplicationIOProxy? ioProxy = null)
        {
            var proxy = new RunnerFileProxy(connection);
            // create a new file and write some lines
            var expected = RandomStrings(32, 5, 0);
            proxy.WriteAllLines(filename, expected);
            // wait for the write to complete
            if (ioProxy != null) { WaitForProxyToFinish(ioProxy); }
            // now append some more lines
            var additional = RandomStrings(32, 5, 1);
            proxy.AppendAllLines(filename, additional);
            expected = expected.Concat(additional).ToArray();
            // now confirm
            var actual = proxy.ReadAllLines(filename);
            Assert.True(Enumerable.SequenceEqual(expected, actual));
        }

        void AppendTextInternal(string filename, Connection? connection = null, ApplicationIOProxy? ioProxy = null)
        {
            var proxy = new RunnerFileProxy(connection);
            // create a new file and write some text
            var expected = RandomString(32, 0);
            proxy.WriteAllText(filename, expected);
            // wait for the write to complete
            if (ioProxy != null) { WaitForProxyToFinish(ioProxy); }
            // now open for appending and write some more text
            using (var stream = proxy.AppendText(filename))
            {
                var additional = RandomString(32, 1);
                stream.Write(additional);
                expected += additional;
            }
            // now confirm
            var actual = proxy.ReadAllText(filename);
            Assert.Equal(expected, actual);
        }

        void CreateInternal(string filename, Connection? connection)
        {
            var proxy = new RunnerFileProxy(connection);
            byte[] expected;
            using (var stream = proxy.Create(filename))
            {
                expected = RandomBytes(64, 0);
                stream.Write(expected);
            }
            var actual = proxy.ReadAllBytes(filename);
            Assert.True(Enumerable.SequenceEqual(expected, actual));
        }

        void OpenNew(string filename, Connection? connection)
        {
            var proxy = new RunnerFileProxy(connection);
            byte[] expected;
            using (var stream = proxy.Open(filename, FileMode.Create))
            {
                expected = RandomBytes(64, 0);
                stream.Write(expected);
            }
            var actual = proxy.ReadAllBytes(filename);
            Assert.True(Enumerable.SequenceEqual(expected, actual));
        }

        void OpenExistingInternal(string filename, Connection? connection)
        {
            byte[] expected;
            using (var file = File.Create(filename))
            {
                expected = RandomBytes(64, 0);
                file.Write(expected);
            }
            var proxy = new RunnerFileProxy(connection);
            using (var stream = proxy.Open(filename, FileMode.Open))
            {
                var actual = new byte[(int)stream.Length];
                stream.Read(actual);
                Assert.True(Enumerable.SequenceEqual(expected, actual));
            }
        }

        void ReadLinesInternal(string filename, Connection? connection)
        {
            var proxy = new RunnerFileProxy(connection);
            var expected = RandomStrings(32, 100, 0);
            proxy.WriteAllLines(filename, expected);
            var actual = proxy.ReadLines(filename).ToList();
            Assert.True(Enumerable.SequenceEqual(expected, actual));
        }

        void WriteAllText(string filename, Connection? connection)
        {
            var proxy = new RunnerFileProxy(connection);
            var expected = Guid.NewGuid().ToString();
            proxy.WriteAllText(filename, expected);
            var actual = proxy.ReadAllText(filename);
            Assert.Equal(expected, actual);
        }

        void WriteAllBytes(string filename, Connection? connection)
        {
            var proxy = new RunnerFileProxy(connection);
            var rand = new System.Random();
            var expected = Enumerable.Range(0, 64).Select(x => (byte)rand.Next(256)).ToArray();
            proxy.WriteAllBytes(filename, expected);
            var actual = proxy.ReadAllBytes(filename);
            Assert.True(Enumerable.SequenceEqual(expected, actual));
        }

        /// <summary>
        /// An application IO proxy is a threaded resource that processes file and directory 
        /// message requests from a remote runner.
        /// This method waits until the proxy has no files open, which is necessary for unit tests
        /// in order to open or delete the file after when it is no longer in use.
        /// </summary>
        /// <param name="proxy"></param>
        void WaitForProxyToFinish(ApplicationIOProxy proxy)
        {
            while (proxy.Files.Any())
            {
                ThreadHelpers.Yield();
            }
        }

        byte[] RandomBytes(int count, int? seed = null)
        {
            var rand = new Random(seed ?? 0);
            return Enumerable.Range(0, count).Select(x => (byte)rand.Next(256)).ToArray();
        }

        string RandomString(int length, int? seed = null)
        {
            var rand = new Random(seed ?? 0);
            return Encoding.ASCII.GetString(Enumerable.Range(0, length).Select(x => (byte)rand.Next(32, 126)).ToArray());
        }

        string[] RandomStrings(int length, int count, int? seed = null)
        {
            var rand = new Random(seed ?? 0);
            return Enumerable.Range(0, count)
                .Select(x =>
                    Encoding.ASCII.GetString(Enumerable.Range(0, length).Select(x => (byte)rand.Next(32, 126)).ToArray())
                ).ToArray();
        }

        void AssertFilesEqual(string file1, string file2)
        {
            var b1 = File.ReadAllBytes(file1);
            var b2 = File.ReadAllBytes(file2);
            Assert.True(Enumerable.SequenceEqual(File.ReadAllBytes(file1), File.ReadAllBytes(file2)));
        }
    }
}