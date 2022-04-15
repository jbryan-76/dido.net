namespace AnywhereNET
{
    /// <summary>
    /// Represents a general error that occurs when a task is running on a runner.
    /// </summary>
    public class TaskGeneralErrorException : Exception
    {
        public TaskGeneralErrorException() { }
        public TaskGeneralErrorException(string message) : base(message) { }
        public TaskGeneralErrorException(string message, Exception innerException) : base(message, innerException) { }
    }
}
