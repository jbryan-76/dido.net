namespace DidoNet
{
    public class MessageChannel
    {
        /// <summary>
        /// Signature for a method that handles when a new message is received on a channel.
        /// </summary>
        /// <param name="message"></param>
        public delegate void MessageReceivedHandler(IMessage message, MessageChannel channel);

        /// <summary>
        /// An event handler that is triggered when a new message is received.
        /// </summary>
        public event MessageReceivedHandler? OnMessageReceived = null;

        /// <summary>
        /// The underlying channel messages are exchanged on.
        /// </summary>
        public Channel Channel { get; private set; }

        /// <summary>
        /// The unique id for the underlying channel messages are exchanged on.
        /// </summary>
        public ushort ChannelNumber { get { return Channel.ChannelNumber; } }

        /// <summary>
        /// Create a new message channel that uses the provided Channel.
        /// </summary>
        /// <param name="channel"></param>
        public MessageChannel(Channel channel)
        {
            Channel = channel;
            Channel.BlockingReads = true;
            Channel.OnDataAvailable += (channel) => DataReceived();
        }

        /// <summary>
        /// Creates a new message channel that uses the given channel number on the given connection.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="channelNumber"></param>
        public MessageChannel(Connection connection, ushort channelNumber)
            : this(connection.GetChannel(channelNumber)) { }

        /// <summary>
        /// Write the given message to the underlying channel.
        /// </summary>
        /// <param name="message"></param>
        public void Send(IMessage message)
        {
            var messageType = message.GetType();
            Channel.WriteString(messageType.AssemblyQualifiedName);
            message.Write(Channel);
        }

        // TODO: explore in future. an interesting idea, but there are some timing concerns
        // TODO: that probably don't make it very reliable
        //public async Task<T> WaitForMessageAsync<T>() where T : IMessage
        //{
        //    var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        //    Task.Run(() =>
        //    {
        //        while (!IsDataAvailable)
        //        {
        //            ThreadHelpers.Yield();

        //            if (!IsConnected)
        //            {
        //                if (throwIfClosed)
        //                {
        //                    throw new IOException("Connection closed.");
        //                }
        //                source.SetResult(false);
        //                return;
        //            }
        //        }
        //        source.SetResult(true);
        //    });
        //    return source.Task;
        //}

        /// <summary>
        /// When more data is available, read the message and invoke the handler.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        private void DataReceived()
        {
            // TODO: send message length too so can read+discard on error?
            var typeName = Channel.ReadString();
            var messageType = Type.GetType(typeName);
            if (messageType == null)
            {
                throw new InvalidOperationException($"Unknown message type '{typeName}'");
            }
            var message = Activator.CreateInstance(messageType) as IMessage;
            if (message == null)
            {
                throw new InvalidOperationException($"Cannot create instance of message type '{typeName}'");
            }
            message.Read(Channel);
            OnMessageReceived?.Invoke(message, this);
        }
    }

}