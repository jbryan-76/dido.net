using System;

namespace DidoNet
{
    /// <summary>
    /// Represents an error that occurs when a task is invoking/running a deserialized expression.
    /// </summary>
    public class TaskInvocationException : Exception
    {
        public TaskInvocationException() { }
        public TaskInvocationException(string? message) : base(message) { }
        public TaskInvocationException(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
