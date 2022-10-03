// TODO: move this to Dido.MediatorFileJobStore

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using static System.Collections.Specialized.BitVector32;

//namespace DidoNet
//{
//    /// <summary>
//    /// A simple file-based memory backing store for job records using LiteDB.
//    /// </summary>
//    public class FileJobStore : IJobStore
//    {
//        /// <summary>
//        /// The on-disk file storing the data.
//        /// </summary>
//        //string DbFile;
//        private string DbFile { get; set; }

//        public FileJobStore(string filename)
//        {
//            DbFile = filename;
//        }

//        /// <summary>
//        /// <inheritdoc/> 
//        /// </summary>
//        public Task CreateJob(IJob job)
//        {
//            using (var db = new LiteDatabase(DbFile))
//            {
//                var jobs = db.GetCollection<JobRecord>();
//                jobs.Insert(new JobRecord
//                {
//                    JobId = job.JobId,
//                    RunnerId = job.RunnerId,
//                    Status = job.Status,
//                    Data = job.Data.ToArray()
//                });
//            }

//            //lock (Jobs)
//            //{
//            //    if (Jobs.ContainsKey(job.JobId))
//            //    {
//            //        throw new Exception($"Duplicate key: A job with id '{job.JobId}' already exists.");
//            //    }
//            //    Jobs.Add(job.JobId, new JobRecord
//            //    {
//            //        JobId = job.JobId,
//            //        RunnerId = job.RunnerId,
//            //        Status = job.Status,
//            //        Data = job.Data.ToArray()
//            //    });
//            //}
//            return Task.CompletedTask;
//        }

//        /// <summary>
//        /// <inheritdoc/> 
//        /// </summary>
//        public Task<bool> UpdateJob(IJob job)
//        {
//            lock (Jobs)
//            {
//                if (!Jobs.ContainsKey(job.JobId))
//                {
//                    return Task.FromResult(false);
//                }
//                Jobs[job.JobId] = new JobRecord
//                {
//                    JobId = job.JobId,
//                    RunnerId = job.RunnerId,
//                    Status = job.Status,
//                    Data = job.Data.ToArray()
//                };
//            }
//            return Task.FromResult(true);
//        }

//        /// <summary>
//        /// <inheritdoc/> 
//        /// </summary>
//        public Task<bool> SetJobStatus(string jobId, string status)
//        {
//            lock (Jobs)
//            {
//                if (!Jobs.ContainsKey(jobId))
//                {
//                    return Task.FromResult(false);
//                }
//                Jobs[jobId].Status = status;
//            }
//            return Task.FromResult(true);
//        }

//        /// <summary>
//        /// <inheritdoc/> 
//        /// </summary>
//        public Task<IJob?> GetJob(string jobId)
//        {
//            lock (Jobs)
//            {
//                Jobs.TryGetValue(jobId, out var job);
//                return Task.FromResult((IJob?)job);
//            }
//        }

//        /// <summary>
//        /// <inheritdoc/> 
//        /// </summary>
//        public Task<IEnumerable<IJob>> GetAllJobs(string runnerId)
//        {
//            lock (Jobs)
//            {
//                return Task.FromResult(Jobs.Values.Where(j => j.RunnerId == runnerId).Select(j => (IJob)j));
//            }
//        }

//        /// <summary>
//        /// <inheritdoc/> 
//        /// </summary>
//        public Task<bool> DeleteJob(string jobId)
//        {
//            lock (Jobs)
//            {
//                return Task.FromResult(Jobs.Remove(jobId));
//            }
//        }

//        private void InitializeDb()
//        {
//            BsonMapper.Global.EmptyStringToNull = false;
//            using (var db = new LiteDatabase(DbFile))
//            {
//                var jobs = db.GetCollection<JobRecord>();
//                jobs.EnsureIndex(x => x.JobId);
//            }
//        }
//    }
//}