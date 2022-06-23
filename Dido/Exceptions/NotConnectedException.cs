using System;

namespace DidoNet
{
    /// <summary>
    /// Represents errors that occur when an attempt is made to use a Connection object that is not connected.
    /// </summary>
    public class NotConnectedException : Exception
    {
        public NotConnectedException() { }
        public NotConnectedException(string? message) : base(message) { }
        public NotConnectedException(string? message, Exception innerException) : base(message, innerException) { }
    }
}
