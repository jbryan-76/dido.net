namespace DidoNet.IO
{
    /// <summary>
    /// Encapsulates a file on the application's local filesystem that is being accessed by a remotely
    /// executing expression on a Runner.
    /// </summary>
    internal class ApplicationFileProxy
    {
        private FileStream? Stream { get; set; }

        private MessageChannel? Channel { get; set; }

        private Action<ApplicationFileProxy>? OnClose { get; set; }

        public ApplicationFileProxy(Connection connection, FileOpenMessage message, Action<ApplicationFileProxy>? onClose = null)
        {
            Stream = File.Open(message.Filename, message.Mode, message.Access, message.Share);
            Channel = new MessageChannel(connection, message.ChannelNumber);
            Channel.OnMessageReceived = FileMessageHandler;
        }

        private void FileMessageHandler(IMessage message, MessageChannel channel)
        {
            switch (message)
            {
                case FileReadRequestMessage read:
                    if (Stream != null)
                    {
                        try
                        {
                            var data = new byte[read.Count];
                            var count = Stream.Read(data, 0, data.Length);
                            Channel!.Send(new FileReadResponseMessage(read.Filename, data));
                        }
                        catch (Exception ex)
                        {
                            Channel!.Send(new FileReadResponseMessage(read.Filename, ex));
                        }
                    }
                    break;

                case FileWriteMessage write:
                    if (Stream != null)
                    {
                        try
                        {
                            Stream.Write(write.Bytes);
                            Channel!.Send(new FileAckMessage(write.Filename));
                        }
                        catch (Exception ex)
                        {
                            Channel!.Send(new FileAckMessage(write.Filename, ex));
                        }
                    }
                    break;


                case FileCloseMessage close:
                    OnClose?.Invoke(this);
                    Channel?.Dispose();
                    Channel = null;
                    Stream?.Dispose();
                    Stream = null;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown message type '{message.GetType()}'");
            }
        }
    }
}
