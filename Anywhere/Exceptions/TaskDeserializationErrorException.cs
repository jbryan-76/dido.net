namespace AnywhereNET
{
    /// <summary>
    /// Represents an error that occurs when a task is deserializing an expression,
    /// including resolving any dependent assemblies used by the expression.
    /// </summary>
    public class TaskDeserializationErrorException : Exception
    {
        public TaskDeserializationErrorException() { }
        public TaskDeserializationErrorException(string message) : base(message) { }
        public TaskDeserializationErrorException(string message, Exception innerException) : base(message, innerException) { }
    }
}
