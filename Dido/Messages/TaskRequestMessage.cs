using System;
using System.IO;
using System.Threading;

namespace DidoNet
{
    internal class TaskRequestMessage : IMessage
    {
        public byte[] Bytes { get; private set; } = new byte[0];

        public string ApplicationId { get; set; } = string.Empty;

        public AssemblyCachingPolicies AssemblyCaching { get; set; }

        public string CachedAssemblyEncryptionKey { get; set; } = string.Empty;

        public int TimeoutInMs { get; private set; } = -1;

        public TaskRequestMessage() { }

        public TaskRequestMessage(
            byte[] bytes,
            string applicationId,
            AssemblyCachingPolicies assemblyCaching,
            string assemblyEncryptionKey,
            int timeoutInMs = Timeout.Infinite)
        {
            Bytes = bytes;
            AssemblyCaching = assemblyCaching;
            CachedAssemblyEncryptionKey = assemblyEncryptionKey;
            ApplicationId = applicationId;
            TimeoutInMs = timeoutInMs;
        }

        public void Read(Stream stream)
        {
            ApplicationId = stream.ReadString();
            AssemblyCaching = Enum.Parse<AssemblyCachingPolicies>(stream.ReadString());
            CachedAssemblyEncryptionKey = stream.ReadString();
            TimeoutInMs = stream.ReadInt32BE();
            Bytes = stream.ReadByteArray();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(ApplicationId);
            stream.WriteString(AssemblyCaching.ToString());
            stream.WriteString(CachedAssemblyEncryptionKey);
            stream.WriteInt32BE(TimeoutInMs);
            stream.WriteByteArray(Bytes);
        }
    }
}