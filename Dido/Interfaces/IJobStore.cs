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
        Task CreateJob(IJob job);
        Task UpdateJob(IJob job);
        Task SetJobStatus(string jobId, string status);
        Task<IJob?> GetJob(string jobId);
        Task<IEnumerable<IJob>> GetAllJobs(string runnerId);
        Task DeleteJob(string jobId);
        //Task DeleteAllJobs(string runnerId);
    }
}