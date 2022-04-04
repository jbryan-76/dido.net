namespace AnywhereNET
{
    /// <summary>
    /// Modes indicating how and where an expression is executed.
    /// </summary>
    public enum ExecutionModes
    {
        /// <summary>
        /// The expression is executed locally, in the host application's domain and environment.
        /// </summary>
        Local,

        /// <summary>
        /// The expression is executed in a remote domain and environment.
        /// </summary>
        Remote
    }
}
