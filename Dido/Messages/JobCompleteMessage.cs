using System.IO;

namespace DidoNet
{
    internal class JobCompleteMessage : IMessage
    {
        public string RunnerId { get; set; } = string.Empty;

        public string JobId { get; set; } = string.Empty;

        //public string ResultMessageType { get; set; } = string.Empty;

        public byte[] ResultMessageBytes { get; set; } = new byte[0];

        public IMessage? ResultMessage
        {
            get
            {
                if (_message == null && ResultMessageBytes.Length > 0)
                {
                    //var type = Type.GetType(ResultMessageType);
                    //if (type == null)
                    //{
                    //    throw new InvalidOperationException($"Unknown message type '{type}'");
                    //}
                    //var message = Activator.CreateInstance(type) as IMessage;
                    //if (message == null)
                    //{
                    //    throw new InvalidOperationException($"Cannot create instance of message type '{ResultMessageType}'");
                    //}
                    using (var stream = new MemoryStream(ResultMessageBytes))
                    {
                        _message = stream.ReadMessage();
                        //message.Read(stream);
                    }
                    //_message = message;
                }
                return _message;
            }
            set
            {
                _message = value;
                //if (_message != null)
                //{
                //var type = value.GetType();
                //ResultMessageType = type.AssemblyQualifiedName!;
                if (_message != null)
                {
                    using (var stream = new MemoryStream())
                    {
                        stream.WriteMessage(_message);
                        //value.Write(stream);
                        ResultMessageBytes = stream.ToArray();
                    }
                }
                else
                {
                    ResultMessageBytes = new byte[0];
                }
                //}
            }
        }

        public JobCompleteMessage() { }

        public JobCompleteMessage(
            string runnerId,
            string jobId,
            IMessage result)
        {
            RunnerId = runnerId;
            JobId = jobId;
            ResultMessage = result;
        }

        public void Read(Stream stream)
        {
            RunnerId = stream.ReadString();
            JobId = stream.ReadString();
            //ResultMessageType = stream.ReadString();
            ResultMessageBytes = stream.ReadByteArray();
            _message = null;
        }

        public void Write(Stream stream)
        {
            stream.WriteString(RunnerId);
            stream.WriteString(JobId);
            //stream.WriteString(ResultMessageType);
            stream.WriteByteArray(ResultMessageBytes);
        }

        private IMessage? _message;
    }
}