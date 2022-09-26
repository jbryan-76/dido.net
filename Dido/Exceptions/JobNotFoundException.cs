using System;

namespace DidoNet
{
    /// <summary>
    /// Represents errors that occur when querying for a job that does not exist.
    /// </summary>
    public class JobNotFoundException : Exception
    {
        public JobNotFoundException() { }
        public JobNotFoundException(string? message) : base(message) { }
        public JobNotFoundException(string? message, Exception innerException) : base(message, innerException) { }
    }
}
