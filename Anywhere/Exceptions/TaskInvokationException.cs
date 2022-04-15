namespace AnywhereNET
{
    /// <summary>
    /// Represents an error that occurs when a task is invoking/running a deserialized expression.
    /// </summary>
    public class TaskInvokationException : Exception
    {
        public TaskInvokationException() { }
        public TaskInvokationException(string message) : base(message) { }
        public TaskInvokationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
