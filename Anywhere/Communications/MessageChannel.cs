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
        /// Messages are delivered serially and in order. The handler should consume 
        /// the message as quickly as possible to avoid creating a backlog.
        /// <para/>Note: The handler is run in a separate thread and any thrown exception
        /// will not be available until the channel is disposed, where it will be wrapped
        /// in an AggregateException.
        /// </summary>
        public MessageReceivedHandler? OnMessageReceived = null;

        /// <summary>
        /// The underlying channel messages are exchanged on.
        /// </summary>
        public Channel Channel { get; private set; }

        /// <summary>
        /// The unique id for the underlying channel messages are exchanged on.
        /// </summary>
        public ushort ChannelNumber { get { return Channel.ChannelNumber; } }

        //private Thread Thread;

        /// <summary>
        /// Create a new message channel that uses the provided Channel.
        /// </summary>
        /// <param name="channel"></param>
        public MessageChannel(Channel channel)
        {
            Channel = channel;
            Channel.BlockingReads = true;
            //Channel.OnDataAvailable += (channel) => DataReceived();
            Channel.OnDataAvailable = (channel) => DataReceived();
            //Thread = new Thread(() =>
            //{
            //    while(true)
            //    {
            //        Channel.OnDataAvailable.WaitOne();
            //        DataReceived();
            //    }
            //});
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
            // lock the channel to only write one contiguous message at a time
            lock (Channel)
            {
                var messageType = message.GetType();
                ThreadHelpers.Debug($"{ChannelNumber} {Channel.Name} sending message {messageType.AssemblyQualifiedName}");
                Channel.WriteString(messageType.AssemblyQualifiedName);
                message.Write(Channel);
                ThreadHelpers.Debug($"{ChannelNumber} {Channel.Name} sent message {messageType.AssemblyQualifiedName}");
                Channel.Flush();
                var checkType = Type.GetType(messageType.AssemblyQualifiedName);
                ThreadHelpers.Debug($"{ChannelNumber} {Channel.Name} confirmed:  {checkType}");
            }
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
            ThreadHelpers.Debug($"{ChannelNumber} {Channel.Name} reading message type");
            var typeName = Channel.ReadString();
            ThreadHelpers.Debug($"{ChannelNumber} {Channel.Name} receiving message {typeName}");
            var messageType = Type.GetType(typeName);
            if (messageType == null)
            {
                ThreadHelpers.Debug($"{ChannelNumber} {Channel.Name} EXCEPTION: empty message type");

                throw new InvalidOperationException($"Unknown message type '{typeName}'");
            }
            var message = Activator.CreateInstance(messageType) as IMessage;
            if (message == null)
            {
                ThreadHelpers.Debug($"{ChannelNumber} {Channel.Name} EXCEPTION: can't create instance");
                throw new InvalidOperationException($"Cannot create instance of message type '{typeName}'");
            }
            ThreadHelpers.Debug($"{ChannelNumber} {Channel.Name} starting read message {typeName}");
            message.Read(Channel);
            ThreadHelpers.Debug($"{ChannelNumber} {Channel.Name} received message {typeName}");
            // this needs to be done in a separate thread so as not to block any event processing loops
            OnMessageReceived?.Invoke(message, this);
            ThreadHelpers.Debug($"{ChannelNumber} {Channel.Name} invoked message receiver");
            //if (OnMessageReceived != null)
            //{
            //    Task.Run(() => OnMessageReceived.Invoke(message, this));
            //}
        }
    }

}