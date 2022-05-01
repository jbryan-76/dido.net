namespace DidoNet
{
    /// <summary>
    /// Represents a general error that occurs when a task is running on a runner.
    /// </summary>
    public class TaskGeneralException : Exception
    {
        public TaskGeneralException() { }
        public TaskGeneralException(string message) : base(message) { }
        public TaskGeneralException(string message, Exception innerException) : base(message, innerException) { }
    }
}
