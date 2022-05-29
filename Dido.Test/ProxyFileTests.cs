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
            using (var localFile = new TemporaryFile())
            using (var localDirectory = new TemporaryFile())
            {
                // create a local temp directory to act as the runner cache location
                Directory.CreateDirectory(localDirectory.Filename);

                // create the proxies to marshal all IO requests
                var appIoProxy = new ApplicationIOProxy(appLoopbackConnection);
                var runnerProxy = new RunnerFileProxy(runnerLoopbackConnection, new RunnerConfiguration
                {
                    FileCachePath = localDirectory.Filename
                });

                var testFileName = "testfile";
                var runnerCachedPath = runnerProxy.GetCachedPath(testFileName);
                var testFileSize = 2001024;

                // create a local runner file that should be stored back to the application
                using (var file = File.Open(runnerCachedPath, FileMode.Create, FileAccess.Write))
                {
                    file.Write(RandomBytes(testFileSize, 0));
                }

                // test various use cases for runner's file to be stored on the application's file-system:
                try
                {
                    // store the file, but track all the received communication frames
                    var receivedFrames_NewFile = new List<Frame>();
                    var sentFrames = new List<Frame>();
                    runnerLoopbackConnection.UnitTestTransmitFrameMonitor = (frame) => sentFrames.Add(frame);
                    appLoopbackConnection.UnitTestReceiveFrameMonitor = (frame) => receivedFrames_NewFile.Add(frame);
                    await runnerProxy.StoreAsync(testFileName, localFile.Filename);

                    // block until the application finishes receiving the file
                    while (!File.Exists(localFile.Filename) ||
                        new FileInfo(runnerCachedPath).Length != new FileInfo(localFile.Filename).Length)
                    {
                        ThreadHelpers.Yield();
                    }

                    var totalDataNewFile = receivedFrames_NewFile.Sum(frame => frame.Payload.Length);

                    // make sure both files match
                    AssertFilesEqual(runnerCachedPath, localFile.Filename);

                    // try to store the file again, but track the communication frames in a separate list
                    // to make sure the file is not actually transferred (since it hasn't changed)
                    var receivedFrames_ExistingFile = new List<Frame>();
                    appLoopbackConnection.UnitTestReceiveFrameMonitor = (frame) => receivedFrames_ExistingFile.Add(frame);
                    sentFrames.Clear();
                    await runnerProxy.StoreAsync(testFileName, localFile.Filename);
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
                    AssertFilesEqual(runnerCachedPath, localFile.Filename);

                    // append additional runner file content, then request to store it again, then make sure the application file is updated
                    using (var file = File.Open(runnerCachedPath, FileMode.Append, FileAccess.Write))
                    {
                        file.Write(RandomBytes(256, 1));
                    }
                    await runnerProxy.StoreAsync(testFileName, localFile.Filename);

                    // block until the application finishes receiving the file
                    while (!File.Exists(localFile.Filename) ||
                        new FileInfo(runnerCachedPath).Length != new FileInfo(localFile.Filename).Length)
                    {
                        ThreadHelpers.Yield();
                    }

                    AssertFilesEqual(runnerCachedPath, localFile.Filename);

                    // now update the file content in place, and force the timestamp to show the file as unmodified...
                    var lastWrite = File.GetLastWriteTimeUtc(runnerCachedPath);
                    using (var file = File.Open(runnerCachedPath, FileMode.Open, FileAccess.ReadWrite))
                    {
                        file.Seek(10, SeekOrigin.Begin);
                        file.Write(RandomBytes(10, 2));
                    }
                    File.SetLastWriteTimeUtc(runnerCachedPath, lastWrite);

                    // ...then request to cache it again WITHOUT hashing and verify the content is different
                    // (since the timestamp and size matches, the proxy assumes the cached copy is the same,
                    // and DOES NOT update it)
                    sentFrames.Clear();
                    receivedFrames_ExistingFile.Clear();
                    await runnerProxy.StoreAsync(testFileName, localFile.Filename);

                    // block until the application finishes receiving the frames
                    while (sentFrames.Count() != receivedFrames_ExistingFile.Count()
                        || sentFrames.Sum(f => f.Length) != receivedFrames_ExistingFile.Sum(f => f.Length))
                    {
                        ThreadHelpers.Yield();
                    }
                    // then wait a bit longer to finish any file IO
                    ThreadHelpers.Yield(500);
                    // then check
                    Assert.False(Enumerable.SequenceEqual(File.ReadAllBytes(runnerCachedPath), File.ReadAllBytes(localFile.Filename)));

                    // finally, request to cache it again WITH hashing and verify it is properly transferred
                    // and the files match
                    sentFrames.Clear();
                    receivedFrames_ExistingFile.Clear();
                    await runnerProxy.StoreAsync(testFileName, localFile.Filename, true);

                    // block until the application finishes receiving the frames
                    while (sentFrames.Count() != receivedFrames_ExistingFile.Count()
                        || sentFrames.Sum(f => f.Length) != receivedFrames_ExistingFile.Sum(f => f.Length))
                    {
                        ThreadHelpers.Yield();
                    }
                    // then wait a bit longer to finish any file IO
                    ThreadHelpers.Yield(500);
                    // then check
                    AssertFilesEqual(runnerCachedPath, localFile.Filename);
                }
                finally
                {
                    // clean up
                    Directory.Delete(localDirectory.Filename, true);
                }
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