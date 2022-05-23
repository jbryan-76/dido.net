namespace DidoNet.IO
{
    /// <summary>
    /// Encapsulates a file on the application's local file-system that is being accessed by a remotely
    /// executing expression on a Runner.
    /// </summary>
    internal class ApplicationFileStreamProxy : IDisposable
    {
        /// <summary>
        /// The underlying file stream on the local file-system.
        /// </summary>
        public FileStream Stream { get; private set; }

        /// <summary>
        /// The dedicated message channel marshaling file IO requests by the executing expression on a
        /// remote Runner to the local file stream.
        /// </summary>
        private MessageChannel Channel { get; set; }

        /// <summary>
        /// Create a new proxy to a local file identified by the provided message and using
        /// the provided connection to marshal IO requests.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="connection"></param>
        public ApplicationFileStreamProxy(FileOpenMessage message, Connection connection)
        {
            Stream = File.Open(message.Filename, message.Mode, message.Access, message.Share);
            Channel = new MessageChannel(connection, message.ChannelNumber);
            Channel.OnMessageReceived = FileMessageHandler;
        }

        public void Dispose()
        {
            Channel.Dispose();
            Stream.Dispose();
        }

        /// <summary>
        /// Processes received messages to manipulate the proxied file.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="channel"></param>
        /// <exception cref="InvalidOperationException"></exception>
        private void FileMessageHandler(IMessage message, MessageChannel channel)
        {
            switch (message)
            {
                case FileReadRequestMessage read:
                    try
                    {
                        var data = new byte[read.Count];
                        var count = Stream.Read(data, 0, data.Length);
                        if (count < data.Length)
                        {
                            // truncate data if necessary
                            var trunc = new byte[count];
                            Buffer.BlockCopy(data, 0, trunc, 0, count);
                            data = trunc;
                        }
                        Channel!.Send(new FileReadResponseMessage(read.Filename, Stream.Position, Stream.Length, data));
                    }
                    catch (Exception ex)
                    {
                        Channel!.Send(new FileReadResponseMessage(read.Filename, ex));
                    }
                    break;

                case FileWriteMessage write:
                    try
                    {
                        Stream.Write(write.Bytes);
                        Channel!.Send(new FileAckMessage(write.Filename, Stream.Position, Stream.Length));
                    }
                    catch (Exception ex)
                    {
                        Channel!.Send(new FileAckMessage(write.Filename, ex));
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unknown message type '{message.GetType()}'");
            }
        }
    }
}
