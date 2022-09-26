using System.IO;

namespace DidoNet
{
    //internal class JobStatusMessage : IMesssage
    //{
    //    public string JobId { get; set; } = string.Empty;

    //    public string JobStatus { get; set; } = string.Empty;



    //    public JobStatusMessage() { }

    //    public JobStatusMessage(
    //        string jobId)
    //    {
    //        JobId = jobId;
    //    }

    //    public void Read(Stream stream)
    //    {
    //        JobId = stream.ReadString();
    //    }

    //    public void Write(Stream stream)
    //    {
    //        stream.WriteString(JobId);
    //    }
    //}

    internal class JobStartMessage : IMessage
    {
        public string RunnerId { get; set; } = string.Empty;

        public string JobId { get; set; } = string.Empty;

        public JobStartMessage() { }

        public JobStartMessage(
            string runnerId,
            string jobId)
        {
            RunnerId = runnerId;
            JobId = jobId;
        }

        public void Read(Stream stream)
        {
            RunnerId = stream.ReadString();
            JobId = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(RunnerId);
            stream.WriteString(JobId);
        }
    }
}