using System.IO;

namespace DidoNet
{
    internal class JobNotFoundMessage : IMessage
    {
        public string JobId { get; set; } = string.Empty;

        public JobNotFoundMessage() { }

        public JobNotFoundMessage(
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