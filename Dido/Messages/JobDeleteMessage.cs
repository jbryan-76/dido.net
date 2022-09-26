using System.IO;

namespace DidoNet
{
    internal class JobDeleteMessage : IMessage
    {
        public string JobId { get; set; } = string.Empty;

        public JobDeleteMessage() { }

        public JobDeleteMessage(
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