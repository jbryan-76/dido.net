using System;

namespace DidoNet
{
    /// <summary>
    /// A job data record.
    /// </summary>
    internal class JobRecord : IJob
    {
        /// <inheritdoc/> 
        public string RunnerId { get; set; } = string.Empty;

        /// <inheritdoc/> 
        public string JobId { get; set; } = string.Empty;

        /// <inheritdoc/> 
        public DateTime Started { get; set; }

        /// <inheritdoc/> 
        public DateTime? Finished { get; set; } = null;

        /// <inheritdoc/> 
        public string Status { get; set; } = string.Empty;

        /// <inheritdoc/> 
        public byte[] Data { get; set; } = new byte[0];
    }
}