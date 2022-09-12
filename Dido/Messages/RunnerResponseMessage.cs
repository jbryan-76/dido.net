using System.IO;

namespace DidoNet
{
    internal class RunnerResponseMessage : IMessage
    {
        public string Endpoint { get; set; } = string.Empty;

        public string JobId { get; set; } = string.Empty;

        public RunnerResponseMessage() { }

        public RunnerResponseMessage(string endpoint)
        {
            Endpoint = endpoint;
        }

        public void Read(Stream stream)
        {
            Endpoint = stream.ReadString();
            JobId = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(Endpoint);
            stream.WriteString(JobId);
        }
    } 
}