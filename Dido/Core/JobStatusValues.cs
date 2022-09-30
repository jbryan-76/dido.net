namespace DidoNet
{
    /// <summary>
    /// State values indicating the job status for storage in a backing store.
    /// </summary>
    internal class JobStatusValues
    {
        /// <summary>
        /// The job is currently executing a task.
        /// </summary>
        internal const string Running = "running";

        /// <summary>
        /// The job has completed executing a task successfully.
        /// </summary>
        internal const string Complete = "complete";

        /// <summary>
        /// The job was cancelled by the application.
        /// </summary>
        internal const string Cancelled = "cancelled";

        /// <summary>
        /// The job did not complete successfully, likely due to an exception thrown
        /// by the expression.
        /// </summary>
        internal const string Error = "error";

        /// <summary>
        /// The job timed out before the task completed.
        /// </summary>
        internal const string Timeout = "timeout";

        /// <summary>
        /// The runner executing the task was stopped or became unavailable and the
        /// job is abandoned.
        /// </summary>
        internal const string Abandoned = "abandoned";
    }
}