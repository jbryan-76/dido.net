using System;

namespace DidoNet
{
    /// <summary>
    /// Represents errors that occur when an attempt is made to load or resolve an assembly.
    /// </summary>
    public class MissingAssemblyException : Exception
    {
        public MissingAssemblyException() { }
        public MissingAssemblyException(string? message) : base(message) { }
        public MissingAssemblyException(string? message, Exception innerException) : base(message, innerException) { }
    }
}
