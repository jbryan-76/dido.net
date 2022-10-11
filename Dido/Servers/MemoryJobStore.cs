using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DidoNet
{
    /// <summary>
    /// An in-memory backing store for job records.
    /// </summary>
    internal class MemoryJobStore : IJobStore
    {
        /// <inheritdoc/> 
        public Task CreateJob(IJob job)
        {
            lock (Jobs)
            {
                if (Jobs.ContainsKey(job.JobId))
                {
                    throw new Exception($"Duplicate key: A job with id '{job.JobId}' already exists.");
                }
                Jobs.Add(job.JobId, new JobRecord
                {
                    JobId = job.JobId,
                    RunnerId = job.RunnerId,
                    Started = job.Started,
                    Finished = job.Finished,
                    Status = job.Status,
                    Data = job.Data.ToArray()
                });
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<bool> UpdateJob(IJob job)
        {
            lock (Jobs)
            {
                if (!Jobs.ContainsKey(job.JobId))
                {
                    return Task.FromResult(false);
                }
                Jobs[job.JobId] = new JobRecord
                {
                    JobId = job.JobId,
                    RunnerId = job.RunnerId,
                    Started = job.Started,
                    Finished = job.Finished,
                    Status = job.Status,
                    Data = job.Data.ToArray()
                };
            }
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public Task<IJob?> GetJob(string jobId)
        {
            lock (Jobs)
            {
                Jobs.TryGetValue(jobId, out var job);
                return Task.FromResult((IJob?)job);
            }
        }

        /// <inheritdoc/>
        public Task<IEnumerable<IJob>> GetAllJobs(string runnerId)
        {
            lock (Jobs)
            {
                return Task.FromResult(Jobs.Values.Where(j => j.RunnerId == runnerId).Select(j => (IJob)j));
            }
        }

        /// <inheritdoc/>
        public Task<bool> DeleteJob(string jobId)
        {
            lock (Jobs)
            {
                return Task.FromResult(Jobs.Remove(jobId));
            }
        }

        /// <inheritdoc/>
        public Task DeleteExpiredJobs(TimeSpan age)
        {
            lock (Jobs)
            {
                var now = DateTime.UtcNow;
                var toDelete = Jobs.Values
                    .Where(j => j.Finished != null && now - j.Finished > age)
                    .ToList();
                foreach (var job in toDelete)
                {
                    Jobs.Remove(job.JobId);
                }
                return Task.CompletedTask;
            }
        }

        private Dictionary<string, JobRecord> Jobs = new Dictionary<string, JobRecord>();
    }
}