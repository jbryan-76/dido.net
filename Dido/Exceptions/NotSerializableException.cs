using System;

namespace DidoNet
{
    /// <summary>
    /// Represents errors that occur in a data model where one or more fields or properties cannot be serialized.
    /// </summary>
    public class NotSerializableException : Exception
    {
        public NotSerializableException() { }
        public NotSerializableException(string message) : base(message) { }
        public NotSerializableException(string message, Exception innerException) : base(message, innerException) { }
    }
}
