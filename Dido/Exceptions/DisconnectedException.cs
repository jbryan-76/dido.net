using System;

namespace DidoNet
{
    /// <summary>
    /// Represents errors that occur when a Connection unexpectedly closes.
    /// </summary>
    public class DisconnectedException : Exception
    {
        public DisconnectedException() { }
        public DisconnectedException(string? message) : base(message) { }
        public DisconnectedException(string? message, Exception innerException) : base(message, innerException) { }
    }

}
