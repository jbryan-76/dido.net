using DidoNet.IO;
using System;

namespace DidoNet
{
    /// <summary>
    /// Encapsulates the internal state for a job executing an expression on a remote runner.
    /// </summary>
    public class JobHandle : IDisposable
    {
        /// <summary>
        /// The globally unique job id.
        /// </summary>
        public string JobId { get; set; }

        // TODO: add an event delegate invoked when the job completes using a completion source?
        // TODO: SetJobHandler(id,handler) => invoke handler when job is done (either polling background thread or use MQ)

        internal ClientConnectionSettings ConnectionSettings { get; set; }

        internal Connection RunnerConnection { get; set; }
        
        internal MessageChannel AssembliesChannel { get; set; }
        
        internal ApplicationIOProxy IOProxy { get; set; }

        public void Dispose()
        {
            RunnerConnection.Dispose();
        }
    }
}
