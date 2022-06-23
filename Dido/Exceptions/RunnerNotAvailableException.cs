using System;

namespace DidoNet
{
    /// <summary>
    /// Represents an error that occurs when no runners are available to process tasks.
    /// </summary>
    public class RunnerNotAvailableException : Exception
    {
        public RunnerNotAvailableException() { }
        public RunnerNotAvailableException(string? message) : base(message) { }
        public RunnerNotAvailableException(string? message, Exception innerException) : base(message, innerException) { }
    }
}
