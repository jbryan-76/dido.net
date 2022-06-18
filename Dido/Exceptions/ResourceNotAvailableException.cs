using System;

namespace DidoNet
{
    /// <summary>
    /// Represents errors that occur when an attempt is made to use a finite resource whose supply is fully
    /// consumed or otherwise not available.
    /// </summary>
    public class ResourceNotAvailableException : Exception
    {
        public ResourceNotAvailableException() { }
        public ResourceNotAvailableException(string message) : base(message) { }
        public ResourceNotAvailableException(string message, Exception innerException) : base(message, innerException) { }
    }
}
