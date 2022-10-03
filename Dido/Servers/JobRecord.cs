using System;

namespace DidoNet
{
    /// <summary>
    /// A job data record.
    /// </summary>
    internal class JobRecord : IJob
    {
        /// <summary>
        /// <inheritdoc/> 
        /// </summary>
        public string RunnerId { get; set; } = string.Empty;

        /// <summary>
        /// <inheritdoc/> 
        /// </summary>
        public string JobId { get; set; } = string.Empty;

        /// <summary>
        /// <inheritdoc/> 
        /// </summary>
        public DateTime Started { get; set; }

        /// <summary>
        /// <inheritdoc/> 
        /// </summary>
        public DateTime? Finished { get; set; } = null;

        /// <summary>
        /// <inheritdoc/> 
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// <inheritdoc/> 
        /// </summary>
        public byte[] Data { get; set; } = new byte[0];
    }
}