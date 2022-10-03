using System;
using System.IO;

namespace DidoNet
{
    internal class JobCompleteMessage : IMessage
    {
        public string RunnerId { get; set; } = string.Empty;

        public string JobId { get; set; } = string.Empty;

        public DateTime Started
        {
            get { return DateTime.FromBinary(_startTime); }
            set { _startTime = value.ToBinary(); }
        }

        public DateTime Finished
        {
            get { return DateTime.FromBinary(_finishTime); }
            set { _finishTime = value.ToBinary(); }
        }

        public byte[] ResultMessageBytes { get; set; } = new byte[0];

        public IMessage? ResultMessage
        {
            get
            {
                if (_message == null && ResultMessageBytes.Length > 0)
                {
                    using (var stream = new MemoryStream(ResultMessageBytes))
                    {
                        _message = stream.ReadMessage();
                    }
                }
                return _message;
            }
            set
            {
                _message = value;
                if (_message != null)
                {
                    using (var stream = new MemoryStream())
                    {
                        stream.WriteMessage(_message);
                        ResultMessageBytes = stream.ToArray();
                    }
                }
                else
                {
                    ResultMessageBytes = new byte[0];
                }
            }
        }

        public JobCompleteMessage() { }

        public JobCompleteMessage(
            string runnerId,
            string jobId,
            IMessage result,
            DateTime started,
            DateTime finished)
        {
            RunnerId = runnerId;
            JobId = jobId;
            ResultMessage = result;
            Started = started;
            Finished = finished;
        }

        public void Read(Stream stream)
        {
            RunnerId = stream.ReadString();
            JobId = stream.ReadString();
            ResultMessageBytes = stream.ReadByteArray();
            _startTime = stream.ReadInt64BE();
            _finishTime = stream.ReadInt64BE();
            _message = null;
        }

        public void Write(Stream stream)
        {
            stream.WriteString(RunnerId);
            stream.WriteString(JobId);
            stream.WriteByteArray(ResultMessageBytes);
            stream.WriteInt64BE(_startTime);
            stream.WriteInt64BE(_finishTime);
        }

        private IMessage? _message;
        private long _startTime { get; set; }
        private long _finishTime { get; set; }
    }
}