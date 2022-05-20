using System.Collections.Concurrent;

namespace DidoNet.IO
{
    /// <summary>
    /// Provides a limited replica API for System.IO.File which is implemented over a network
    /// connection from a Runner to a remote application.
    /// </summary>
    public class RunnerFileProxy
    {
        //System.IO.File;
        // TRANSIENT, ATOMIC-LIKE OPS:
        // append; copy; delete; exists; get info; move; open; read; set info; write;

        /// <summary>
        /// The dedicated message channel marshalling file IO requests by the executing expression on a
        /// Runner to the remote application.
        /// </summary>
        internal MessageChannel? Channel { get; set; }

        /// <summary>
        /// The set of currently open files managed by this instance, keyed to the file path.
        /// </summary>
        private ConcurrentDictionary<string, RunnerFileStreamProxy> Files = new ConcurrentDictionary<string, RunnerFileStreamProxy>();

        /// <summary>
        /// The set of available channel numbers that can be used to create dedicated message channels
        /// to proxy virtualized file access.
        /// </summary>
        private SortedSet<ushort> AvailableChannels = new SortedSet<ushort>();

        /// <summary>
        /// The set of used channel numbers used for dedicated message channels that proxy virtual file access.
        /// </summary>
        private SortedSet<ushort> UsedChannels = new SortedSet<ushort>();

        /// <summary>
        /// Create a new instance to proxy file IO over a connection.
        /// If a connection is not provided, the class instance API will pass-through to the local filesystem.
        /// </summary>
        /// <param name="connection"></param>
        internal RunnerFileProxy(Connection? connection)
        {
            AvailableChannels = new SortedSet<ushort>(
                Enumerable.Range(Constants.AppRunner_FileChannelStart, Constants.AppRunner_MaxFileChannels).Select(i => (ushort)i)
                );
            if (connection != null)
            {
                Channel = new MessageChannel(connection, Constants.AppRunner_FileChannelId);
            }
        }

        // void AppendAllLines(string path, IEnumerable<string> contents)
        // void AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding)
        // void AppendAllText(string path, string? contents)
        // void AppendAllText(string path, string? contents, Encoding encoding)
        // StreamWriter AppendText(string path)

        // void Copy(string sourceFileName, string destFileName)
        // void Copy(string sourceFileName, string destFileName, bool overwrite)
        // void Delete(string path)
        // bool Exists([NotNullWhen(true)] string? path)

        // FileAttributes GetAttributes(string path)
        // DateTime GetCreationTime(string path)
        // DateTime GetCreationTimeUtc(string path)
        // DateTime GetLastAccessTime(string path)
        // DateTime GetLastAccessTimeUtc(string path)
        // DateTime GetLastWriteTime(string path)
        // DateTime GetLastWriteTimeUtc(string path)

        // void Move(string sourceFileName, string destFileName)
        // void Move(string sourceFileName, string destFileName, bool overwrite)

        // byte[] ReadAllBytes(string path)
        // string[] ReadAllLines(string path)
        // string[] ReadAllLines(string path, Encoding encoding)
        // string ReadAllText(string path)
        // string ReadAllText(string path, Encoding encoding)
        // IEnumerable<string> ReadLines(string path)
        // IEnumerable<string> ReadLines(string path, Encoding encoding)

        // void SetAttributes(string path, FileAttributes fileAttributes)
        // void SetCreationTime(string path, DateTime creationTime)
        // void SetCreationTimeUtc(string path, DateTime creationTimeUtc)
        // void SetLastAccessTime(string path, DateTime lastAccessTime)
        // void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)
        // void SetLastWriteTime(string path, DateTime lastWriteTime)
        // void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)

        // void WriteAllBytes(string path, byte[] bytes)
        // void WriteAllLines(string path, IEnumerable<string> contents)
        // void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding)
        // void WriteAllLines(string path, string[] contents)
        // void WriteAllLines(string path, string[] contents, Encoding encoding)
        // void WriteAllText(string path, string? contents)
        // void WriteAllText(string path, string? contents, Encoding encoding)

        /// <summary>
        /// Creates or overwrites a file in the specified path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Stream Create(string path)
        {
            return Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        }

        /// <summary>
        /// Creates or overwrites a file in the specified path.
        /// <para/>NOTE: bufferSize is not used due to the underlying proxied IO over a network connection.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="bufferSize"></param>
        /// <returns></returns>
        public Stream Create(string path, int bufferSize)
        {
            return Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        }

        /// <summary>
        /// Creates or opens a file for writing UTF-8 encoded text. If the file already exists,
        /// its contents are overwritten.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public StreamWriter CreateText(string path)
        {
            return new StreamWriter(Create(path));
        }

        /// <summary>
        /// Opens a file stream on the specified path with read/write access with no sharing.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public Stream Open(string path, FileMode mode)
        {
            return Open(path, mode, FileAccess.ReadWrite, FileShare.None);
        }

        /// <summary>
        /// Opens a file stream on the specified path, with the specified mode and
        /// access with no sharing.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <param name="access"></param>
        /// <returns></returns>
        public Stream Open(string path, FileMode mode, FileAccess access)
        {
            return Open(path, mode, access, FileShare.None);
        }

        /// <summary>
        /// Opens a file stream on the specified path, having the specified mode
        /// with read, write, or read/write access and the specified sharing option.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <param name="access"></param>
        /// <param name="share"></param>
        /// <returns></returns>
        /// <exception cref="ConcurrencyException"></exception>
        public Stream Open(string path, FileMode mode, FileAccess access, FileShare share)
        {
            if (Channel != null)
            {
                // enter a critical section: 
                // for a given connection, only one thread can attempt to open a file at a time
                lock (Channel)
                {
                    // confirm another thread has not already opened the file or failed to close it
                    if (Files.ContainsKey(path))
                    {
                        throw new ConcurrencyException($"File '{path}' was opened or created by another thread and is not yet closed.");
                    }

                    // opening a file: create a dedicated channel
                    ushort channelNumber = AcquireChannel();
                    try
                    {
                        // send the open file request and wait for the corresponding response.
                        // because this code block has a lock on the connection, the request/response
                        // pair will be correlated and not interleaved with another thread's attempt
                        // to open or create a file
                        Channel.Send(new FileOpenMessage(channelNumber, path, mode, access, share));
                        var ack = Channel.ReceiveMessage<FileAckMessage>();
                        ack.ThrowOnError();

                        // on a successful open, both sides agreed to create a new dedicated channel for
                        // IO of the indicated file using the acquired channel number
                        var fileChannel = new MessageChannel(Channel.Channel.Connection, channelNumber);
                        var stream = new RunnerFileStreamProxy(path, ack.Position, ack.Length, fileChannel, (filename) => Close(filename));
                        Files.TryAdd(path, stream);
                        return stream;
                    }
                    catch (Exception)
                    {
                        ReleaseChannel(channelNumber);
                        throw;
                    }
                }
            }
            else
            {
                return File.Open(path, mode, access, share);
            }
        }

        /// <summary>
        /// Opens a file stream on the specified path and with the specified options.
        /// <para/>NOTE: Only options Mode, Access, and Share are honored.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public Stream Open(string path, FileStreamOptions options)
        {
            return Open(path, options.Mode, options.Access, options.Share);
        }

        /// <summary>
        /// Opens an existing file for reading.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Stream OpenRead(string path)
        {
            return Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
        }

        /// <summary>
        /// Opens an existing UTF-8 encoded text file for reading.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public StreamReader OpenText(string path)
        {
            return new StreamReader(OpenRead(path));
        }

        /// <summary>
        /// Opens an existing file or creates a new file for writing.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Stream OpenWrite(string path)
        {
            return Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
        }

        /// <summary>
        /// Closes the indicated file.
        /// </summary>
        /// <param name="path"></param>
        private void Close(string path)
        {
            if (Files.TryRemove(path, out var stream))
            {
                // inform the remote side the file connection is closing
                Channel?.Send(new FileCloseMessage(path));
                // make the utilized channel available again
                ReleaseChannel(stream.Channel.ChannelNumber);
            }
        }

        private ushort AcquireChannel()
        {
            lock (AvailableChannels)
            {
                // opening a file: create a dedicated channel
                if (AvailableChannels.Count == 0)
                {
                    throw new ResourceNotAvailableException("No more communication channels are available on the underlying connection.");
                }

                var channelNumber = AvailableChannels.First();
                AvailableChannels.Remove(channelNumber);
                UsedChannels.Add(channelNumber);

                return channelNumber;
            }
        }

        private void ReleaseChannel(ushort channelNumber)
        {
            lock (AvailableChannels)
            {
                UsedChannels.Remove(channelNumber);
                AvailableChannels.Add(channelNumber);
            }
        }
    }
}
