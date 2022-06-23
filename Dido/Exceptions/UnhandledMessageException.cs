using System;

namespace DidoNet
{
    /// <summary>
    /// Represents errors that occur when an unknown or unhandled message is received.
    /// </summary>
    public class UnhandledMessageException : Exception
    {
        public UnhandledMessageException() { }
        public UnhandledMessageException(IMessage message) : base($"Unknown or unhandled message type '{message.GetType()}'") { }
        public UnhandledMessageException(IMessage message, Exception innerException) : base($"Unknown or unhandled message type '{message.GetType()}'", innerException) { }
    }
}
