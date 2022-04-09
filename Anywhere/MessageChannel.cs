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

        public Channel Channel { get; private set; }

        public MessageChannel(Channel channel)
        {
            Channel = channel;
            Channel.BlockingReads = true;
            Channel.OnDataAvailable += (channel) => DataReceived();
        }

        public void Send(IMessage message)
        {
            var messageType = message.GetType();
            Channel.WriteString(messageType.AssemblyQualifiedName);
            message.Write(Channel);
        }

        private void DataReceived()
        {
            // TODO: send message length too so can skip on error?
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