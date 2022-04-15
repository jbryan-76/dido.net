namespace AnywhereNET
{
    /// <summary>
    /// Represents an error that occurs when a task is cancelled.
    /// </summary>
    public class TaskCancelException : Exception
    {
        public TaskCancelException() { }
        public TaskCancelException(string message) : base(message) { }
        public TaskCancelException(string message, Exception innerException) : base(message, innerException) { }
    }
}
