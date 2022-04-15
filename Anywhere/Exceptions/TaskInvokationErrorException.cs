namespace AnywhereNET
{
    /// <summary>
    /// Represents an error that occurs when a task is invoking/running a deserialized expression.
    /// </summary>
    public class TaskInvokationErrorException : Exception
    {
        public TaskInvokationErrorException() { }
        public TaskInvokationErrorException(string message) : base(message) { }
        public TaskInvokationErrorException(string message, Exception innerException) : base(message, innerException) { }
    }
}
