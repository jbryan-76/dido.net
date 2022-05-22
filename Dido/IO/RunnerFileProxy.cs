using System.Collections.Concurrent;
using System.Text;

namespace DidoNet.IO
{
    /// <summary>
    /// Provides a limited replica API for System.IO.File which is implemented over a network
    /// connection from a Runner to a remote application.
    /// </summary>
    public class RunnerFileProxy
    {
        //System.IO.File;

        /// <summary>
        /// The dedicated message channel marshalling file IO requests by the executing expression on a
        /// Runner to the remote application.
        /// </summary>
        internal MessageChannel? Channel { get; set; }

        /// <summary>
        /// The set of currently open files managed by this instance, keyed to the file path.
        /// </summary>
        private ConcurrentDictionary<string, RunnerFileStreamProxy> Files = new ConcurrentDictionary<string, RunnerFileStreamProxy>();

        // TODO: use a <ushort,bool> dictionary for channels instead? with the value indicating whether it is used?

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

        /// <summary>
        /// Appends lines to a file, and then closes the file. If the specified file does
        /// not exist, this method creates a file, writes the specified lines to the file,
        /// and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        public void AppendAllLines(string path, IEnumerable<string> contents)
        {
            AppendAllLines(path, contents, new UTF8Encoding(false));
        }

        /// <summary>
        /// Appends lines to a file by using a specified encoding, and then closes the file.
        /// If the specified file does not exist, this method creates a file, writes the
        /// specified lines to the file, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        /// <param name="encoding"></param>
        public void AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding)
        {
            if (Channel != null)
            {
                Channel.Send(new FileAtomicWriteMessage(path, contents, append: true, encoding));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
            }
            else
            {
                File.AppendAllLines(path, contents, encoding);
            }
        }

        /// <summary>
        /// Opens a file, appends the specified string to the file, and then closes the file.
        /// If the file does not exist, this method creates a file, writes the specified
        /// string to the file, then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        public void AppendAllText(string path, string contents)
        {
            AppendAllText(path, contents, new UTF8Encoding(false));
        }

        /// <summary>
        /// Appends the specified string to the file using the specified encoding, creating
        /// the file if it does not already exist.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        /// <param name="encoding"></param>
        public void AppendAllText(string path, string contents, Encoding encoding)
        {
            if (Channel != null)
            {
                Channel.Send(new FileAtomicWriteMessage(path, contents, append: true, encoding));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
            }
            else
            {
                File.AppendAllText(path, contents, encoding);
            }
        }

        /// <summary>
        /// Creates a System.IO.StreamWriter that appends UTF-8 encoded text to an existing
        /// file, or to a new file if the specified file does not exist.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public StreamWriter AppendText(string path)
        {
            if (Channel != null)
            {
                return new StreamWriter(Open(path, FileMode.Append), new UTF8Encoding(false));
            }
            else
            {
                return File.AppendText(path);
            }
        }

        // void Copy(string sourceFileName, string destFileName)
        // void Copy(string sourceFileName, string destFileName, bool overwrite)

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

        //Stream Create(string path, int bufferSize, FileOptions options)

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

        /// <summary>
        /// Opens a file stream on the specified path with read/write access with no sharing.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public Stream Open(string path, FileMode mode)
        {
            return Open(path, mode, (mode == FileMode.Append ? FileAccess.Write : FileAccess.ReadWrite), FileShare.None);
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
            return Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
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
        /// Opens a binary file, reads the contents of the file into a byte array, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="IOException"></exception>
        public byte[] ReadAllBytes(string path)
        {
            using (var stream = OpenRead(path))
            {
                if (stream.Length > Int32.MaxValue)
                {
                    throw new IOException("File length is greater than the 2GB limit.");
                }
                return stream.ReadBytes((int)stream.Length);
            }
        }

        /// <summary>
        /// Opens a text file, reads all lines of the file, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public string[] ReadAllLines(string path)
        {
            return ReadAllLines(path, Encoding.UTF8);
        }

        /// <summary>
        /// Opens a file, reads all lines of the file with the specified encoding, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public string[] ReadAllLines(string path, Encoding encoding)
        {
            using (var reader = new StreamReader(new BufferedStream(OpenRead(path)), encoding))
            {
                // implementation copied from .NET mscorlib
                string? line;
                var lines = new List<string>();
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
                return lines.ToArray();
            }
        }

        /// <summary>
        /// Opens a text file, reads all the text in the file, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public string ReadAllText(string path)
        {
            return ReadAllText(path, Encoding.UTF8);
        }

        /// <summary>
        /// Opens a file, reads all text in the file with the specified encoding, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public string ReadAllText(string path, Encoding encoding)
        {
            using (var reader = new StreamReader(OpenRead(path), encoding))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Reads the lines of a file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public IEnumerable<string> ReadLines(string path)
        {
            return ReadLines(path, Encoding.UTF8);
        }

        /// <summary>
        /// Read the lines of a file that has a specified encoding.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public IEnumerable<string> ReadLines(string path, Encoding encoding)
        {
            using (var reader = new StreamReader(new BufferedStream(OpenRead(path)), encoding))
            {
                for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    yield return line;
                }
            }
        }

        // void SetAttributes(string path, FileAttributes fileAttributes)
        // void SetCreationTime(string path, DateTime creationTime)
        // void SetCreationTimeUtc(string path, DateTime creationTimeUtc)
        // void SetLastAccessTime(string path, DateTime lastAccessTime)
        // void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)
        // void SetLastWriteTime(string path, DateTime lastWriteTime)
        // void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)

        /// <summary>
        /// Creates a new file, writes the specified byte array to the file, and then closes
        /// the file. If the target file already exists, it is overwritten.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="bytes"></param>
        public void WriteAllBytes(string path, byte[] bytes)
        {
            if (Channel != null)
            {
                Channel.Send(new FileAtomicWriteMessage(path, bytes, append: true));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
            }
            else
            {
                File.WriteAllBytes(path, bytes);
            }
        }

        /// <summary>
        /// Creates a new file, writes a collection of strings to the file, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        public void WriteAllLines(string path, IEnumerable<string> contents)
        {
            WriteAllLines(path, contents, new UTF8Encoding(false));
        }

        /// <summary>
        /// Creates a new file by using the specified encoding, writes a collection of strings
        /// to the file, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        /// <param name="encoding"></param>
        public void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding)
        {
            if (Channel != null)
            {
                Channel.Send(new FileAtomicWriteMessage(path, contents, append: false, encoding));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
            }
            else
            {
                File.WriteAllLines(path, contents, encoding);
            }
        }

        /// <summary>
        /// Creates a new file, write the specified string array to the file, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        public void WriteAllLines(string path, string[] contents)
        {
            WriteAllLines(path, contents, new UTF8Encoding(false));
        }

        /// <summary>
        /// Creates a new file, writes the specified string array to the file by using the
        /// specified encoding, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        /// <param name="encoding"></param>
        public void WriteAllLines(string path, string[] contents, Encoding encoding)
        {
            if (Channel != null)
            {
                Channel.Send(new FileAtomicWriteMessage(path, contents, append: false, encoding));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
            }
            else
            {
                File.WriteAllLines(path, contents, encoding);
            }
        }

        /// <summary>
        /// Creates a new file, writes the specified string to the file, and then closes
        /// the file. If the target file already exists, it is overwritten.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        public void WriteAllText(string path, string contents)
        {
            WriteAllText(path, contents, new UTF8Encoding(false));
        }

        /// <summary>
        /// Creates a new file, writes the specified string to the file using the specified
        /// encoding, and then closes the file. If the target file already exists, it is
        /// overwritten.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        /// <param name="encoding"></param>
        public void WriteAllText(string path, string contents, Encoding encoding)
        {
            if (Channel != null)
            {
                Channel.Send(new FileAtomicWriteMessage(path, contents, append: false, encoding));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
            }
            else
            {
                File.WriteAllText(path, contents, encoding);
            }
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
