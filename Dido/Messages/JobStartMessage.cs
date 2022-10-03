using System;
using System.IO;

namespace DidoNet
{
    internal class JobStartMessage : IMessage
    {
        public string RunnerId { get; set; } = string.Empty;

        public string JobId { get; set; } = string.Empty;

        public DateTime Started
        {
            get { return DateTime.FromBinary(_startTime); }
            set { _startTime = value.ToBinary(); }
        }

        public JobStartMessage() { }

        public JobStartMessage(string runnerId, string jobId, DateTime started)
        {
            RunnerId = runnerId;
            JobId = jobId;
            Started = started;
        }

        public void Read(Stream stream)
        {
            RunnerId = stream.ReadString();
            JobId = stream.ReadString();
            _startTime = stream.ReadInt64BE();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(RunnerId);
            stream.WriteString(JobId);
            stream.WriteInt64BE(_startTime);
        }

        private long _startTime { get; set; }
    }
}