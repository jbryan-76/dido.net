using Dido.Utilities;
using DidoNet.IO;
using DidoNet.Test.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace DidoNet.Test
{
    public class ProxyFileTests
    {
        //public ProxyFileTests(ITestOutputHelper output)
        //{
        //    var converter = new OutputConverter(output);
        //    //var converter = new OutputConverter(output, "OUTPUT.txt");
        //    Console.SetOut(converter);
        //}

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
        public void ReadWriteSeek()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var localFile = new TemporaryFile())
            using (var remoteFile = new TemporaryFile())
            {
                ReadWriteSeekInternal(localFile.Filename, null);

                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                ReadWriteSeekInternal(remoteFile.Filename, runnerLoopbackConnection);
                WaitForProxyToFinish(ioProxy);

                AssertFilesEqual(localFile.Filename, remoteFile.Filename);
            }
        }

        [Fact]
        public void SetLength()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var localFile = new TemporaryFile())
            using (var remoteFile = new TemporaryFile())
            {
                SetLengthInternal(localFile.Filename, null);

                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                SetLengthInternal(remoteFile.Filename, runnerLoopbackConnection);
                WaitForProxyToFinish(ioProxy);

                AssertFilesEqual(localFile.Filename, remoteFile.Filename);
            }
        }

        [Fact]
        public async void Cache()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var localFile = new TemporaryFile())
            {
                var testFileName = "testfile";
                var testFileSize = 2001024;

                // create a local application file that should be cached on the runner
                using (var file = File.Open(localFile.Filename, FileMode.Create, FileAccess.Write))
                {
                    file.Write(RandomBytes(testFileSize, 0));
                }

                // create the proxies to marshal all IO requests
                var appIoProxy = new ApplicationIOProxy(appLoopbackConnection);
                var runnerProxy = new RunnerFileProxy(runnerLoopbackConnection, new RunnerConfiguration
                {
                    FileCachePath = "cache/files"
                });

                // test various use cases for the application file to be cached on the runner's file-system:
                string? cachedFilename = null;
                try
                {
                    // make sure the destination cached file does not already exist
                    // from a previous test run
                    cachedFilename = runnerProxy.GetCachedPath(testFileName);
                    if (!string.IsNullOrEmpty(cachedFilename) && File.Exists(cachedFilename))
                    {
                        File.Delete(cachedFilename);
                    }

                    // cache the file, but track all the received communication frames
                    var receivedFrames_NewFile = new List<Frame>();
                    runnerLoopbackConnection.UnitTestReceiveFrameMonitor = (frame) => receivedFrames_NewFile.Add(frame);
                    cachedFilename = await runnerProxy.CacheAsync(localFile.Filename, testFileName);
                    var totalDataNewFile = receivedFrames_NewFile.Sum(frame => frame.Payload.Length);

                    // make sure both files match
                    AssertFilesEqual(localFile.Filename, cachedFilename);

                    // try to cache the file again, but track the communication frames in a separate list
                    // to make sure the file is not actually transferred (since it hasn't changed)
                    var receivedFrames_ExistingFile = new List<Frame>();
                    runnerLoopbackConnection.UnitTestReceiveFrameMonitor = (frame) => receivedFrames_ExistingFile.Add(frame);
                    await runnerProxy.CacheAsync(localFile.Filename, testFileName);
                    var totalDataExistingFile = receivedFrames_ExistingFile.Sum(frame => frame.Payload.Length);

                    // make sure less data was transferred (reflecting that the whole file was not copied since it
                    // already existed, unchanged)
                    Assert.True(totalDataExistingFile < testFileSize);
                    Assert.True(totalDataExistingFile < totalDataNewFile);

                    // make sure both files match
                    AssertFilesEqual(localFile.Filename, cachedFilename);

                    // append additional application file content, then request to cache it again, then make sure the cached file is updated
                    using (var file = File.Open(localFile.Filename, FileMode.Append, FileAccess.Write))
                    {
                        file.Write(RandomBytes(256, 1));
                    }
                    await runnerProxy.CacheAsync(localFile.Filename, testFileName);
                    AssertFilesEqual(localFile.Filename, cachedFilename);

                    // now update the file content in place, and force the timestamp to show the file as unmodified...
                    var lastWrite = File.GetLastWriteTimeUtc(localFile.Filename);
                    using (var file = File.Open(localFile.Filename, FileMode.Open, FileAccess.ReadWrite))
                    {
                        file.Seek(10, SeekOrigin.Begin);
                        file.Write(RandomBytes(10, 2));
                    }
                    File.SetLastWriteTimeUtc(localFile.Filename, lastWrite);

                    // ...then request to cache it again WITHOUT hashing and verify the content is different
                    // (since the timestamp and size matches, the proxy assumes the cached copy is the same,
                    // and DOES NOT update it)
                    await runnerProxy.CacheAsync(localFile.Filename, testFileName);
                    Assert.False(Enumerable.SequenceEqual(File.ReadAllBytes(localFile.Filename), File.ReadAllBytes(cachedFilename)));

                    // finally, request to cache it again WITH hashing and verify it is properly transferred
                    // and the files match
                    await runnerProxy.CacheAsync(localFile.Filename, testFileName, true);
                    AssertFilesEqual(localFile.Filename, cachedFilename);
                }
                finally
                {
                    // clean up
                    if (!string.IsNullOrEmpty(cachedFilename) && File.Exists(cachedFilename))
                    {
                        File.Delete(cachedFilename);
                    }
                }
            }
        }

        [Fact]
        public async void Store()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var tempFile = new TemporaryFile())
            using (var localDirectory = new TemporaryDirectory())
            {
                // create the proxies to marshal all IO requests
                var appIoProxy = new ApplicationIOProxy(appLoopbackConnection);
                var runnerProxy = new RunnerFileProxy(runnerLoopbackConnection, new RunnerConfiguration
                {
                    FileCachePath = localDirectory.Path
                });

                // create a local runner file that should be stored back to the application
                var runnerLocalFile = runnerProxy.GetCachedPath("testfile");
                var testFileSize = 2001024;
                using (var file = File.Open(runnerLocalFile, FileMode.Create, FileAccess.Write))
                {
                    file.Write(RandomBytes(testFileSize, 0));
                }

                // test various use cases for runner's file to be stored on the application's file-system:

                // store the file, but track all the received communication frames
                var receivedFrames_NewFile = new List<Frame>();
                var sentFrames = new List<Frame>();
                runnerLoopbackConnection.UnitTestTransmitFrameMonitor = (frame) => sentFrames.Add(frame);
                appLoopbackConnection.UnitTestReceiveFrameMonitor = (frame) => receivedFrames_NewFile.Add(frame);
                await runnerProxy.StoreAsync(runnerLocalFile, tempFile.Filename);

                // block until the application finishes receiving the file
                while (!File.Exists(tempFile.Filename) ||
                    new FileInfo(runnerLocalFile).Length != new FileInfo(tempFile.Filename).Length)
                {
                    ThreadHelpers.Yield();
                }

                var totalDataNewFile = receivedFrames_NewFile.Sum(frame => frame.Payload.Length);

                // make sure both files match
                AssertFilesEqual(runnerLocalFile, tempFile.Filename);

                // try to store the file again, but track the communication frames in a separate list
                // to make sure the file is not actually transferred (since it hasn't changed)
                var receivedFrames_ExistingFile = new List<Frame>();
                appLoopbackConnection.UnitTestReceiveFrameMonitor = (frame) => receivedFrames_ExistingFile.Add(frame);
                sentFrames.Clear();
                await runnerProxy.StoreAsync(runnerLocalFile, tempFile.Filename);
                var totalDataExistingFile = receivedFrames_ExistingFile.Sum(frame => frame.Payload.Length);

                // block until the application finishes receiving the frames
                while (sentFrames.Count() != receivedFrames_ExistingFile.Count()
                    || sentFrames.Sum(f => f.Length) != receivedFrames_ExistingFile.Sum(f => f.Length))
                {
                    ThreadHelpers.Yield();
                }

                // make sure less data was transferred (reflecting that the whole file was not copied since it
                // already existed, unchanged)
                Assert.True(totalDataExistingFile < testFileSize);
                Assert.True(totalDataExistingFile < totalDataNewFile);

                // make sure both files match
                AssertFilesEqual(runnerLocalFile, tempFile.Filename);

                // append additional runner file content, then request to store it again, then make sure the application file is updated
                using (var file = File.Open(runnerLocalFile, FileMode.Append, FileAccess.Write))
                {
                    file.Write(RandomBytes(256, 1));
                }
                await runnerProxy.StoreAsync(runnerLocalFile, tempFile.Filename);

                // block until the application finishes receiving the file
                while (!File.Exists(tempFile.Filename) ||
                    new FileInfo(runnerLocalFile).Length != new FileInfo(tempFile.Filename).Length)
                {
                    ThreadHelpers.Yield();
                }

                AssertFilesEqual(runnerLocalFile, tempFile.Filename);

                // now update the file content in place, and force the timestamp to show the file as unmodified...
                var lastWrite = File.GetLastWriteTimeUtc(runnerLocalFile);
                using (var file = File.Open(runnerLocalFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    file.Seek(10, SeekOrigin.Begin);
                    file.Write(RandomBytes(10, 2));
                }
                File.SetLastWriteTimeUtc(runnerLocalFile, lastWrite);

                // ...then request to store it again WITHOUT hashing and verify the content is different
                // (since the timestamp and size matches, the proxy assumes the local copy is the same,
                // and DOES NOT update it)
                sentFrames.Clear();
                receivedFrames_ExistingFile.Clear();
                await runnerProxy.StoreAsync(runnerLocalFile, tempFile.Filename);

                // block until the application finishes receiving the frames
                while (sentFrames.Count() != receivedFrames_ExistingFile.Count()
                    || sentFrames.Sum(f => f.Length) != receivedFrames_ExistingFile.Sum(f => f.Length))
                {
                    ThreadHelpers.Yield();
                }
                // then wait a bit longer to finish any file IO
                ThreadHelpers.Yield(500);
                // then check
                Assert.False(Enumerable.SequenceEqual(File.ReadAllBytes(runnerLocalFile), File.ReadAllBytes(tempFile.Filename)));

                // finally, request to store it again WITH hashing and verify it is properly transferred
                // and the files match
                sentFrames.Clear();
                receivedFrames_ExistingFile.Clear();
                await runnerProxy.StoreAsync(runnerLocalFile, tempFile.Filename, true);

                // block until the application finishes receiving the frames
                while (sentFrames.Count() != receivedFrames_ExistingFile.Count()
                    || sentFrames.Sum(f => f.Length) != receivedFrames_ExistingFile.Sum(f => f.Length))
                {
                    ThreadHelpers.Yield();
                }
                // then wait a bit longer to finish any file IO
                ThreadHelpers.Yield(500);
                // then check
                AssertFilesEqual(runnerLocalFile, tempFile.Filename);
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
                OpenNewInternal(localFile.Filename, null);

                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                OpenNewInternal(remoteFile.Filename, runnerLoopbackConnection);
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
        public void WriteAllText()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var localFile = new TemporaryFile())
            using (var remoteFile = new TemporaryFile())
            {
                WriteAllTextInternal(localFile.Filename, null);

                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                WriteAllTextInternal(remoteFile.Filename, runnerLoopbackConnection);
                WaitForProxyToFinish(ioProxy);

                AssertFilesEqual(localFile.Filename, remoteFile.Filename);
            }
        }

        [Fact]
        public void WriteAllBytes()
        {
            using (var loopback = new Connection.LoopbackProxy())
            using (var appLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Client))
            using (var runnerLoopbackConnection = new Connection(loopback, Connection.LoopbackProxy.Role.Server))
            using (var localFile = new TemporaryFile())
            using (var remoteFile = new TemporaryFile())
            {
                WriteAllBytesInternal(localFile.Filename, null);

                var ioProxy = new ApplicationIOProxy(appLoopbackConnection);
                WriteAllBytesInternal(remoteFile.Filename, runnerLoopbackConnection);
                WaitForProxyToFinish(ioProxy);

                AssertFilesEqual(localFile.Filename, remoteFile.Filename);
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

        void OpenNewInternal(string filename, Connection? connection)
        {
            var proxy = new RunnerFileProxy(connection);
            byte[] expected;
            using (var stream = proxy.Open(filename, FileMode.Create))
            {
                expected = RandomBytes(64, 0);
                stream.Write(expected);
                Assert.Equal(expected.Length, stream.Position);
                Assert.Equal(expected.Length, stream.Length);
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
                Assert.Equal(0, stream.Position);
                Assert.Equal(expected.Length, stream.Length);
                stream.Read(actual);
                Assert.Equal(expected.Length, stream.Position);
                Assert.Equal(expected.Length, stream.Length);
                Assert.True(Enumerable.SequenceEqual(expected, actual));
            }
        }

        void SetLengthInternal(string filename, Connection? connection)
        {
            // create a new file with some content
            var proxy = new RunnerFileProxy(connection);
            byte[] expected;
            var deltaLength = 10;
            using (var stream = proxy.Open(filename, FileMode.Create))
            {
                expected = RandomBytes(64, 0);
                stream.Write(expected);

                // set the length to longer and confirm length and position
                var expectedPosition = stream.Position;
                var expectedNewLength = stream.Length + deltaLength;
                stream.SetLength(expectedNewLength);
                Assert.Equal(expectedNewLength, stream.Length);
                Assert.Equal(expectedPosition, stream.Position);

                // write some data to fill the length and confirm length and position and content
                var extraData = RandomBytes(deltaLength, 1);
                expected = expected.Concat(extraData).ToArray();
                stream.WriteBytes(extraData);
                Assert.Equal(expectedNewLength, stream.Length);
                Assert.Equal(expectedNewLength, stream.Position);

                // confirm content
                stream.Seek(0, SeekOrigin.Begin);
                var actual = new byte[stream.Length];
                stream.Read(actual, 0, actual.Length);
                Assert.True(Enumerable.SequenceEqual(expected, actual));
                Assert.Equal(expectedNewLength, stream.Position);

                // set the length to shorter and confirm length and position and content
                expectedNewLength -= deltaLength / 2;
                expectedPosition = expectedNewLength;
                expected = expected.Take((int)expectedNewLength).ToArray();
                stream.SetLength(expectedNewLength);
                Assert.Equal(expectedNewLength, stream.Length);
                Assert.Equal(expectedPosition, stream.Position);

                // confirm content
                stream.Seek(0, SeekOrigin.Begin);
                actual = new byte[stream.Length];
                stream.Read(actual, 0, actual.Length);
                Assert.True(Enumerable.SequenceEqual(expected, actual));
            }
        }

        void ReadWriteSeekInternal(string filename, Connection? connection)
        {
            // create a new file with some content
            var proxy = new RunnerFileProxy(connection);
            byte[] expected;
            using (var stream = proxy.Open(filename, FileMode.Create))
            {
                expected = RandomBytes(64, 0);
                stream.Write(expected);

                // seek/write/read in a few places.
                // also update the expected array to match content:

                // overwrite data at the beginning
                stream.Seek(0, SeekOrigin.Begin);
                Assert.Equal(0, stream.Position);
                var chunk = RandomBytes(8, 1);
                stream.Write(chunk);
                Buffer.BlockCopy(chunk, 0, expected, 0, chunk.Length);

                // copy some data to the end
                stream.Seek(4, SeekOrigin.Current);
                Buffer.BlockCopy(expected, (int)stream.Position, expected, expected.Length - chunk.Length, chunk.Length);
                stream.Read(chunk);
                stream.Seek(-chunk.Length, SeekOrigin.End);
                stream.Write(chunk);
                Assert.Equal(stream.Length, stream.Position);

                // overwrite data in the middle
                stream.Seek(-expected.Length / 4, SeekOrigin.End);
                Assert.Equal(expected.Length - expected.Length / 4, stream.Position);
                chunk = RandomBytes(8, 1);
                stream.Write(chunk);
                Buffer.BlockCopy(chunk, 0, expected, expected.Length - expected.Length / 4, chunk.Length);

                // confirm content
                stream.Seek(0, SeekOrigin.Begin);
                var actual = new byte[stream.Length];
                stream.Read(actual, 0, actual.Length);
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

        void WriteAllTextInternal(string filename, Connection? connection)
        {
            var proxy = new RunnerFileProxy(connection);
            var expected = RandomString(32, 0);
            proxy.WriteAllText(filename, expected);
            var actual = proxy.ReadAllText(filename);
            Assert.Equal(expected, actual);
        }

        void WriteAllBytesInternal(string filename, Connection? connection)
        {
            var proxy = new RunnerFileProxy(connection);
            var expected = RandomBytes(64, 0);
            proxy.WriteAllBytes(filename, expected);
            var actual = proxy.ReadAllBytes(filename);
            Assert.True(Enumerable.SequenceEqual(expected, actual));
        }

        /// <summary>
        /// An application IO proxy is a threaded resource that processes file and directory 
        /// message requests from a remote runner.
        /// This method waits until the proxy has no files open, which is necessary for unit tests
        /// in order to open or delete the file after it is no longer in use.
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
            Assert.True(Enumerable.SequenceEqual(File.ReadAllBytes(file1), File.ReadAllBytes(file2)));
        }
    }
}