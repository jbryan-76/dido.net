using System.Collections.Concurrent;

namespace DidoNet.IO
{
    /// <summary>
    /// Processes file and directory IO requests from a remotely executing expression, which is using 
    /// a RunnerFileProxy or RunnerDirectoryProxy instance in an ExecutionContext.
    /// </summary>
    internal class ApplicationIOProxy : MessageChannel
    {
        private ConcurrentDictionary<string, ApplicationFileProxy> Files = new ConcurrentDictionary<string, ApplicationFileProxy>();

        private Connection Connection;

        /// <summary>
        /// Create a new io proxy to process file and directory IO requests.
        /// </summary>
        /// <param name="connection"></param>
        public ApplicationIOProxy(Connection connection)
            : base(connection, Constants.AppRunner_FileChannelId)
        {
            Connection = connection;
            OnMessageReceived = FileMessageHandler;
        }

        internal async void FileMessageHandler(IMessage message, MessageChannel channel)
        {
            switch (message)
            {
                case FileOpenMessage open:
                    try
                    {
                        var file = new ApplicationFileProxy(Connection, open, file => Files.TryRemove(open.Filename, out _));
                        Files.TryAdd(open.Filename, file);
                    }
                    catch (Exception ex)
                    {
                        channel.Send(new FileAckMessage(open.Filename, ex));
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unknown message type '{message.GetType()}'");
            }
        }
    }
}
