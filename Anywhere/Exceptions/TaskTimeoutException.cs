namespace AnywhereNET
{
    /// <summary>
    /// Represents an error that occurs when a task has timed out before completing execution.
    /// </summary>
    public class TaskTimeoutException : Exception
    {
        public TaskTimeoutException() { }
        public TaskTimeoutException(string message) : base(message) { }
        public TaskTimeoutException(string message, Exception innerException) : base(message, innerException) { }
    }
}
