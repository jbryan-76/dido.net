using System.Collections.Concurrent;

namespace DidoNet.IO
{
    public class RunnerFileProxy
    {
        //System.IO.File;
        // TRANSIENT, ATOMIC-LIKE OPS:
        // append; copy; delete; exists; get info; move; open; read; set info; write;
        // CREATES A STREAM RESOURCE:
        // create; open; openread; openwrite; 

        // TODO: relative files only? only files that resolve as descendents from the root app folder?

        private ConcurrentDictionary<string, FileStreamProxy> Files = new ConcurrentDictionary<string, FileStreamProxy>();

        private SortedSet<ushort> AvailableChannels = new SortedSet<ushort>();

        private SortedSet<ushort> UsedChannels = new SortedSet<ushort>();

        /// <summary>
        /// The channel to use for exchanging messages.
        /// </summary>
        //internal MessageChannel? Channel { get; set; }
        internal Connection? Connection { get; set; }

        internal MessageChannel? Channel { get; set; }

        /// <summary>
        /// Create a new instance to proxy file IO over a connection.
        /// If a connection is not provided, the class instance API will pass-through to the local filesystem.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="connection"></param>
        //internal ProxyFile(MessageChannel? channel)
        internal RunnerFileProxy(Connection? connection)
        {
            AvailableChannels = new SortedSet<ushort>(
                Enumerable.Range(Constants.AppRunner_FileChannelStart, Constants.AppRunner_MaxFileChannels).Select(i => (ushort)i)
                );
            Connection = connection;
            if (Connection != null)
            {
                Channel = new MessageChannel(Connection, Constants.AppRunner_FileChannelId);
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
            if (Connection != null)
            {
                // enter a critical section: 
                // for a given connection, only one thread can attempt to open a file at a time
                lock (Connection)
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
                        //var channel = new FileIOChannel(Connection, channelNumber);
                        // send the open file request and wait for the corresponding response.
                        // because this code block has a lock on the connection, the request/response
                        // pair will be correlated and not interleaved with another thread's attempt
                        // to open or create a file
                        Channel!.Send(new FileOpenMessage(channelNumber, path, mode, access, share));
                        var ack = Channel.ReceiveMessage<FileAckMessage>();
                        ack.ThrowOnError();

                        // on a successful open, both sides agreed to create a new dedicated channel for
                        // IO of the indicated file using the acquired channel number
                        var fileChannel = new MessageChannel(Connection, channelNumber);
                        var stream = new FileStreamProxy(path, fileChannel, (filename) => Close(filename));
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

        //public static FileStream Open(string path, FileStreamOptions options)
        //{
        //    throw new NotImplementedException();
        //}

        private void Close(string path)
        {
            if (Files.TryRemove(path, out var stream))
            {
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
