namespace DidoNet
{
    /// <summary>
    /// Modes indicating how and where an expression is executed.
    /// </summary>
    public enum ExecutionModes
    {
        /// <summary>
        /// The expression is executed locally in the host application's domain and environment.
        /// </summary>
        Local,

        /// <summary>
        /// The expression is executed locally in the host application's domain and environment
        /// only when running in a debugger, otherwise remotely according to the 
        /// configuration used at invokation.
        /// </summary>
        DebugLocal,

        /// <summary>
        /// The expression is executed in a remote domain and environment according to the 
        /// configuration used at invokation.
        /// </summary>
        Remote
    }
}
