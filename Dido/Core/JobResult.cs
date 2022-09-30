using System;

namespace DidoNet
{
    /// <summary>
    /// Encapsulates the result of a job executing an expression on a remote runner.
    /// </summary>
    /// <typeparam name="Tprop"></typeparam>
    public class JobResult<Tprop>
    {
        /// <summary>
        /// The status of the job.
        /// </summary>
        public JobStatus Status { get; set; }

        /// <summary>
        /// The result of the job. Note this value is only reliable when
        /// Status is Complete.
        /// </summary>
        public Tprop Result { get; set; }

        /// <summary>
        /// When Status is Error, contains the exception caught during execution of the expression.
        /// </summary>
        public Exception? Exception { get; set; }
    }
}
