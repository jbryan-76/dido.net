using System;

namespace DidoNet
{
    /// <summary>
    /// The contract for a data record stored in a backing store containing the 
    /// opaque state representing a single job.
    /// </summary>
    public interface IJob
    {
        /// <summary>
        /// The globally unique id of the runner where the job is executing.
        /// </summary>
        string RunnerId { get; set; }

        /// <summary>
        /// The globally unique id of the job.
        /// </summary>
        string JobId { get; set; }

        /// <summary>
        /// The UTC timestamp when the job started execution.
        /// </summary>
        DateTime Started { get; set; }

        /// <summary>
        /// The UTC timestamp when the job finished execution, or null if the job is not yet finished.
        /// </summary>
        DateTime? Finished { get; set; }

        /// <summary>
        /// An opaque string representing the internal job status.
        /// </summary>
        string Status { get; set; }

        /// <summary>
        /// An opaque blob representing the internal job state.
        /// </summary>
        byte[] Data { get; set; }
    }
}