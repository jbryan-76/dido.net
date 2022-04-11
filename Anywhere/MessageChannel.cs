namespace AnywhereNET
{
    public class MessageChannel
    {
        /// <summary>
        /// Signature for a method that handles when a new message is received on a channel.
        /// </summary>
        /// <param name="message"></param>
        public delegate void MessageReceivedHandler(IMessage message, Channel channel);

        /// <summary>
        /// An event handler that is triggered when a new IMessage is received.
        /// </summary>
        public event MessageReceivedHandler? OnMessageReceived = null;

        /// <summary>
        /// The underlying channel this instance is using.
        /// </summary>
        public Channel Channel { get; private set; }

        /// <summary>
        /// Create a new message channel that uses the provided channel.
        /// </summary>
        /// <param name="channel"></param>
        public MessageChannel(Channel channel)
        {
            Channel = channel;
            Channel.BlockingReads = true;
            Channel.OnDataAvailable += (channel) => DataReceived();
        }

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
            OnMessageReceived?.Invoke(message, Channel);
        }
    }

}