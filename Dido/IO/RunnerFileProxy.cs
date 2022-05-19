using System.Collections.Concurrent;

namespace DidoNet.IO
{
    /// <summary>
    /// Provides a limited replica API for System.IO.File which is implemented over a network
    /// connection from a Runner to a remote application.
    /// </summary>
    public class RunnerFileProxy
    {
        System.IO.File;
        // TRANSIENT, ATOMIC-LIKE OPS:
        // append; copy; delete; exists; get info; move; open; read; set info; write;
        // CREATES A STREAM RESOURCE:
        // create; open; openread; openwrite; 

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
        /// The dedicated message channel marshalling file IO requests by the executing expression on a
        /// Runner to the remote application.
        /// </summary>
        internal MessageChannel? Channel { get; set; }

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

        public Stream Open(string path, FileMode mode)
        {
            return Open(path, mode, FileAccess.ReadWrite, FileShare.None);
        }

        public Stream Open(string path, FileMode mode, FileAccess access)
        {
            return Open(path, mode, access, FileShare.None);
        }

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

        public Stream OpenRead(string path)
        {
            return Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
        }

        public StreamReader OpenText(string path)
        {
            return new StreamReader(OpenRead(path));
        }

        public Stream OpenWrite(string path)
        {
            return Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
        }

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
                    throw new ResourceNotAvailableException("No more File channels are available on the underlying connection.");
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
