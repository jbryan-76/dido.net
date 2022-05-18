namespace DidoNet
{
    /// <summary>
    /// Represents errors that occur in a misconfigured or invalid configuration object.
    /// </summary>
    public class InvalidConfigurationException : Exception
    {
        public InvalidConfigurationException() { }
        public InvalidConfigurationException(string message) : base(message) { }
        public InvalidConfigurationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
