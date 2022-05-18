namespace DidoNet
{
    /// <summary>
    /// Represents errors that occur when two or more threads or contexts concurrently attempted 
    /// to manipulate the same object or data.
    /// </summary>
    public class ConcurrencyException : Exception
    {
        public ConcurrencyException() { }
        public ConcurrencyException(string message) : base(message) { }
        public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
    }
}
