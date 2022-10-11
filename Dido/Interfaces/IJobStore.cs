using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DidoNet
{
    /// <summary>
    /// The contract for a data store to persist job records.
    /// </summary>
    public interface IJobStore
    {
        /// <summary>
        /// Create a record in the store to persist the provided job state.
        /// Throws an exception only due to errors accessing the store.
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        Task CreateJob(IJob job);

        /// <summary>
        /// Update an existing record in the store with the provided job state.
        /// Return <c>true</c> if the record exists, else <c>false</c>.
        /// Throws an exception only due to errors accessing the store.
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        Task<bool> UpdateJob(IJob job);

        /// <summary>
        /// Get the record from the store of the job with the given id.
        /// Return null if the record does not exist.
        /// Throws an exception only due to errors accessing the store.
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        Task<IJob?> GetJob(string jobId);

        /// <summary>
        /// Get all job records matching the given runner id.
        /// Throws an exception only due to errors accessing the store.
        /// </summary>
        /// <param name="runnerId"></param>
        /// <returns></returns>
        Task<IEnumerable<IJob>> GetAllJobs(string runnerId);

        /// <summary>
        /// Delete the record from the store for the job with the given id.
        /// Return <c>true</c> if the record exists, else <c>false</c>.
        /// Throws an exception only due to errors accessing the store.
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        Task<bool> DeleteJob(string jobId);

        /// <summary>
        /// Delete all records from the store where the Finished timestamp of the job
        /// is older than the provided age.
        /// </summary>
        /// <returns></returns>
        Task DeleteExpiredJobs(TimeSpan age);
    }
}