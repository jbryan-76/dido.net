namespace DidoNet
{
    /// <summary>
    /// Represents errors that occur when an attempt is made to resolve a specific Type from an assembly
    /// but the type does not exist in the assembly.
    /// </summary>
    public class TypeNotFoundException : Exception
    {
        public TypeNotFoundException() { }
        public TypeNotFoundException(string message) : base(message) { }
        public TypeNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }
}
