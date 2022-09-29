using System.Collections.Generic;
using System.Threading.Tasks;

namespace DidoNet
{
    public interface IJob
    {
        string RunnerId { get; set; }
        string JobId { get; set; }
        string Status { get; set; }
        byte[] Data { get; set; }
    }

    public interface IJobStore
    {
        /// <summary>
        /// Create a record in the store containing the provided job values.
        /// Throw an exception only due to errors accessing the store.
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        Task CreateJob(IJob job);

        /// <summary>
        /// Update an existing record in the store with the provided job values.
        /// Return true if the record exists, else false.
        /// Throw an exception only due to errors accessing the store.
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        Task<bool> UpdateJob(IJob job);

        /// <summary>
        /// Set the status property of an existing record in the store.
        /// Return true if the record exists, else false.
        /// Throw an exception only due to errors accessing the store.
        /// </summary>
        /// <param name="jobId"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        Task<bool> SetJobStatus(string jobId, string status);

        /// <summary>
        /// Get the record from the store of the job with the given id.
        /// Return null if the record does not exist.
        /// Throw an exception only due to errors accessing the store.
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        Task<IJob?> GetJob(string jobId);

        /// <summary>
        /// Get all job records matching the given runner id.
        /// Throw an exception only due to errors accessing the store.
        /// </summary>
        /// <param name="runnerId"></param>
        /// <returns></returns>
        Task<IEnumerable<IJob>> GetAllJobs(string runnerId);

        /// <summary>
        /// Delete the record from the store of the job with the given id.
        /// Return true if the record exists, else false.
        /// Throw an exception only due to errors accessing the store.
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        Task<bool> DeleteJob(string jobId);
    }
}