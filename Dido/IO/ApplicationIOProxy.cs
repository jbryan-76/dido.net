using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace DidoNet.IO
{
    /// <summary>
    /// Processes file and directory IO requests from a remotely executing expression, which is using 
    /// a RunnerFileProxy or RunnerDirectoryProxy instance in an ExecutionContext.
    /// </summary>
    internal class ApplicationIOProxy : MessageChannel
    {
        /// <summary>
        /// The readonly set of currently open files managed by this instance, keyed to the file path.
        /// </summary>
        public ReadOnlyDictionary<string, ApplicationFileStreamProxy> Files
        {
            get { return new ReadOnlyDictionary<string, ApplicationFileStreamProxy>(files); }
        }

        /// <summary>
        /// The set of currently open files managed by this instance, keyed to the file path.
        /// </summary>
        private ConcurrentDictionary<string, ApplicationFileStreamProxy> files = new ConcurrentDictionary<string, ApplicationFileStreamProxy>();

        /// <summary>
        /// Create a new io proxy to process file and directory IO requests.
        /// </summary>
        /// <param name="connection"></param>
        public ApplicationIOProxy(Connection connection)
            : base(connection, Constants.AppRunner_FileChannelId)
        {
            OnMessageReceived = FileMessageHandler;
        }

        /// <summary>
        /// Processes received messages to perform file and directory IO operations for the remotely
        /// executing expression.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="channel"></param>
        /// <exception cref="InvalidOperationException"></exception>
        void FileMessageHandler(IMessage message, MessageChannel channel)
        {
            switch (message)
            {
                case FileOpenMessage open:
                    try
                    {
                        var openFile = new ApplicationFileStreamProxy(open, Channel.Connection);
                        files.TryAdd(open.Filename, openFile);
                        channel.Send(new FileAckMessage(open.Filename, openFile.Stream.Position, openFile.Stream.Length));
                    }
                    catch (Exception ex)
                    {
                        channel.Send(new FileAckMessage(open.Filename, ex));
                    }

                    break;

                case FileCloseMessage close:
                    if (files.TryGetValue(close.Filename, out var closeFile))
                    {
                        // dispose the file before removing it
                        closeFile.Dispose();
                        files.TryRemove(close.Filename, out _);
                    }
                    break;

                case FileAtomicWriteMessage write:
                    try
                    {
                        using (var file = File.Open(write.Filename, write.Append ? FileMode.Append : FileMode.Create))
                        {
                            file.Write(write.Bytes);
                            channel.Send(new FileAckMessage(write.Filename, file.Position, file.Length));
                        }
                    }
                    catch (Exception ex)
                    {
                        channel.Send(new FileAckMessage(write.Filename, ex));
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unknown message type '{message.GetType()}'");
            }
        }
    }
}
