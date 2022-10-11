using DidoNet.IO;
using System;

namespace DidoNet
{
    /// <summary>
    /// Encapsulates the internal state for a task executing as a job on a remote runner.
    /// </summary>
    public class JobHandle : IDisposable
    {
        /// <summary>
        /// The globally unique job id.
        /// </summary>
        public string JobId { get; set; }

        /// <summary>
        /// The connection settings used to connect the application to the runner.
        /// </summary>
        internal ClientConnectionSettings ConnectionSettings { get; set; }

        /// <summary>
        /// The connection from the application to the runner.
        /// </summary>
        internal Connection RunnerConnection { get; set; }
        
        /// <summary>
        /// The assemblies communications channel from the application to the runner.
        /// </summary>
        internal MessageChannel AssembliesChannel { get; set; }
        
        /// <summary>
        /// The application IO proxy to handle IO requests from a remotely executing task.
        /// </summary>
        internal ApplicationIOProxy IOProxy { get; set; }

        /// <inheritdoc/> 
        public void Dispose()
        {
            RunnerConnection.Dispose();
        }
    }
}
