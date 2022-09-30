namespace DidoNet
{
    /// <summary>
    /// Indicates the status of an job.
    /// </summary>
    public enum JobStatus
    {
        /// <summary>
        /// The job is currently executing a task.
        /// </summary>
        Running,

        /// <summary>
        /// The job has completed executing a task successfully and the expression result is available.
        /// </summary>
        Complete,

        /// <summary>
        /// The job was cancelled by the application.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The job did not complete successfully or the task threw an exception.
        /// </summary>
        Error,

        /// <summary>
        /// The job timed out before the task completed.
        /// </summary>
        Timeout,

        /// <summary>
        /// The runner executing the task was stopped or became unavailable and the
        /// job is abandoned.
        /// </summary>
        Abandoned
    }
}
