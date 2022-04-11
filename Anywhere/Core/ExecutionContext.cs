namespace AnywhereNET
{
    /// <summary>
    /// Provides configuration and utilities for remote execution of expressions.
    /// </summary>
    public class ExecutionContext
    {
        /// <summary>
        /// Indicates how the current expression is being executed.
        /// </summary>
        public ExecutionModes ExecutionMode { get; internal set; }

        // TODO: add methods to access files, etc

        // TODO: add support to indicate progress

        // TODO: add support for caller to cancel

        internal Channel FilesChannel { get; set; }

        internal ExecutionContext()
        {

        }
    }
}
