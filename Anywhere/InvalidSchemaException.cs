namespace AnywhereNET
{
    /// <summary>
    /// Represents errors that occur when a combination of properties or attributes in a data model violates the rules
    /// for safe, accurate, or reliable behavior.
    /// </summary>
    public class InvalidSchemaException : Exception
    {
        public InvalidSchemaException() { }
        public InvalidSchemaException(string message) : base(message) { }
        public InvalidSchemaException(string message, Exception innerException) : base(message, innerException) { }
    }
}
