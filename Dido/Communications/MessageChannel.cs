using Dido.Utilities;
using System;
using System.Threading.Tasks;

namespace DidoNet
{
    // TODO: create an interface for client/server communication that uses messages and allow apps to provide their own implementation?

    /// <summary>
    /// Wraps a single unique bidirectional communications channel on a Connection
    /// and specializes it for sending and receiving atomic messages conforming to the IMessage interface.
    /// Messages should be relatively small, and the underlying channel should EXCLUSIVELY be used by the MessageChannel
    /// (ie do not send both messages and general stream traffic or the data may be interleaved).
    /// <para/>WARNING: While this class is thread-safe and reading and writing can be done on separate threads,
    /// never use more than 1 thread per direction (read or write), as it may result in data interleaving. If the
    /// application design necessitates multiple threads, it MUST also implement the necessary logic to enforce reading 
    /// or writing only a single message at a time in critical sections.
    /// </summary>
    public class MessageChannel : IDisposable
    {
        /// <summary>
        /// Signature for a method that handles when a new message is received on a channel.
        /// </summary>
        /// <param name="message"></param>
        public delegate void MessageReceivedHandler(IMessage message, MessageChannel channel);

        /// <summary>
        /// Signature for a method that is invoked when a message is transmitted or received. 
        /// </summary>
        /// <param name="frame"></param>
        internal delegate void MessageMonitor(IMessage message);

        /// <summary>
        /// An event handler that is triggered when a new message is received.
        /// Messages are delivered serially and in order. The handler should consume 
        /// the message as quickly as possible to avoid creating a backlog.
        /// <para/>Note: The handler is run in a separate thread and any thrown exception
        /// will not be available until the channel is disposed, where it will be wrapped
        /// in an AggregateException.
        /// </summary>
        public MessageReceivedHandler? OnMessageReceived
        {
            get { return MessageReceived; }
            set
            {
                MessageReceived = value;
                Channel.OnDataAvailable = MessageReceived == null ? (Channel.ChannelDataAvailableHandler?)null : (channel) => DataReceived();
            }
        }

        /// <summary>
        /// The underlying channel messages are exchanged on.
        /// </summary>
        public Channel Channel { get; private set; }

        /// <summary>
        /// The unique id for the underlying communications channel.
        /// </summary>
        public string ChannelId { get { return Channel.ChannelId; } }

        /// <summary>
        /// Internal handler for unit tests to monitor received messages.
        /// </summary>
        internal MessageMonitor? UnitTestReceiveMessageMonitor;

        /// <summary>
        /// Internal handler for unit tests to monitor transmitted messages.
        /// </summary>
        internal MessageMonitor? UnitTestTransmitMessageMonitor;

        /// <summary>
        /// The event handler triggered when a new message is received.
        /// </summary>
        private MessageReceivedHandler? MessageReceived = null;

        /// <summary>
        /// Create a new message channel that uses the provided Channel to send and receive messages.
        /// </summary>
        /// <param name="channel"></param>
        public MessageChannel(Channel channel)
        {
            Channel = channel;
            Channel.BlockingReads = true;
        }

        /// <summary>
        /// Creates a new message channel that uses the given channel id on the given connection.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="channelId"></param>
        public MessageChannel(Connection connection, string channelId)
            : this(connection.GetChannel(channelId)) { }

        public void Dispose()
        {
            Channel.Dispose();
        }

        /// <summary>
        /// Write the given message to the underlying channel.
        /// </summary>
        /// <param name="message"></param>
        public void Send(IMessage message)
        {
            UnitTestTransmitMessageMonitor?.Invoke(message);
            var messageType = message.GetType();
            Channel.WriteString(messageType.AssemblyQualifiedName!);
            message.Write(Channel);
            Channel.Flush();
        }

        // TODO: add SendAsync
        ///// <summary>
        ///// Write the given message to the underlying channel.
        ///// </summary>
        ///// <param name="message"></param>
        //public async Task SendAsync(IMessage message)
        //{
        //    var messageType = message.GetType();
        //    await Channel.WriteStringAsync(messageType.AssemblyQualifiedName!);
        //    message.Write(Channel);
        //    Channel.Flush();
        //}

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
        /// Block and receive a message from the underlying channel.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="TimeoutException"></exception>
        public IMessage ReceiveMessage(TimeSpan? timeout = null)
        {
            if (timeout != null)
            {
                // TODO: any way to do this without using a thread?
                var task = Task.Run(() =>
                {
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
                    return message;

                });
                var result = task.TimeoutAfter(timeout.Value).GetAwaiter().GetResult();
                UnitTestReceiveMessageMonitor?.Invoke(result);
                return result;
            }

            // TODO: send message length too so can read+discard on error?
            ThreadHelpers.Debug($"{ChannelId} reading message type");
            var typeName = Channel.ReadString();
            ThreadHelpers.Debug($"{ChannelId} receiving message {typeName}");
            var messageType = Type.GetType(typeName);
            if (messageType == null)
            {
                ThreadHelpers.Debug($"{ChannelId} EXCEPTION: empty message type");

                throw new InvalidOperationException($"Unknown message type '{typeName}'");
            }
            var message = Activator.CreateInstance(messageType) as IMessage;
            if (message == null)
            {
                ThreadHelpers.Debug($"{ChannelId} EXCEPTION: can't create instance");
                throw new InvalidOperationException($"Cannot create instance of message type '{typeName}'");
            }
            ThreadHelpers.Debug($"{ChannelId} starting read message {typeName}");
            message.Read(Channel);
            ThreadHelpers.Debug($"{ChannelId} received message {typeName}");

            UnitTestReceiveMessageMonitor?.Invoke(message);
            return message;
        }

        // TODO: add ReceiveMessageAsync

        /// <summary>
        /// Block and receive a message of the indicated type from the underlying channel.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="TimeoutException"></exception>
        public T ReceiveMessage<T>(TimeSpan? timeout = null) where T : class, IMessage
        {
            var message = ReceiveMessage(timeout);
            return message is T t
                ? t
                : throw new InvalidOperationException($"Received message is type '{message.GetType()}', which cannot be assigned to intended type '{typeof(T)}'.");
        }

        /// <summary>
        /// When more data is available, read the message and invoke the handler.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        private void DataReceived()
        {
            var message = ReceiveMessage();
            OnMessageReceived?.Invoke(message, this);
            ThreadHelpers.Debug($"{ChannelId} invoked message receiver");
        }
    }
}