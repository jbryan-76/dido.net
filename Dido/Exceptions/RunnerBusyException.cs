using System;

namespace DidoNet
{
    /// <summary>
    /// Represents an error that occurs when a runner is busy processing and cannot take new tasks.
    /// </summary>
    public class RunnerBusyException : Exception
    {
        public RunnerBusyException() { }
        public RunnerBusyException(string? message) : base(message) { }
        public RunnerBusyException(string? message, Exception innerException) : base(message, innerException) { }
    }
}
