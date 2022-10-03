using System;

namespace DidoNet
{
    /// <summary>
    /// Represents an error that occurs when multiple runners with the same Id attempt to register
    /// with the same mediator.
    /// </summary>
    public class DuplicateRunnerException : Exception
    {
        public DuplicateRunnerException() { }
        public DuplicateRunnerException(string? message) : base(message) { }
        public DuplicateRunnerException(string? message, Exception innerException) : base(message, innerException) { }
    }
}
