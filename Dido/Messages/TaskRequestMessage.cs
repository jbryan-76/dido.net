using System.IO;
using System.Threading;

namespace DidoNet
{
    internal class TaskRequestMessage : IMessage
    {
        public byte[] Bytes { get; private set; } = new byte[0];

        public string ApplicationId { get; set; }

        public int TimeoutInMs { get; private set; } = -1;

        public TaskRequestMessage() { }

        public TaskRequestMessage(byte[] bytes, string applicationId, int timeoutInMs = Timeout.Infinite)
        {
            Bytes = bytes;
            ApplicationId = applicationId;
            TimeoutInMs = timeoutInMs;
        }

        public void Read(Stream stream)
        {
            ApplicationId = stream.ReadString();
            TimeoutInMs = stream.ReadInt32BE();
            int length = stream.ReadInt32BE();
            Bytes = stream.ReadBytes(length);
        }

        public void Write(Stream stream)
        {
            stream.WriteString(ApplicationId);
            stream.WriteInt32BE(TimeoutInMs);
            stream.WriteInt32BE(Bytes.Length);
            stream.Write(Bytes);
        }
    }
}