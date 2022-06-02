namespace DidoNet
{
    /// <summary>
    /// Represents an error that occurs when a task is deserializing an expression,
    /// including resolving any dependent assemblies used by the expression.
    /// </summary>
    public class TaskDeserializationException : Exception
    {
        public TaskDeserializationException() { }
        public TaskDeserializationException(string? message) : base(message) { }
        public TaskDeserializationException(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
