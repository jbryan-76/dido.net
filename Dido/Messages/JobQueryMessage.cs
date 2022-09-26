using System.IO;

namespace DidoNet
{
    internal class JobQueryMessage : IMessage
    {
        public string JobId { get; set; } = string.Empty;

        public JobQueryMessage() { }

        public JobQueryMessage(
            string jobId)
        {
            JobId = jobId;
        }

        public void Read(Stream stream)
        {
            JobId = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(JobId);
        }
    }
}