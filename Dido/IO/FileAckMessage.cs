namespace DidoNet.IO
{
    internal class FileAckMessage : FileMessageBase
    {
        public string ExceptionType { get; set; } = "";

        public string ExceptionMessage { get; set; } = "";

        public bool IsOk { get { return string.IsNullOrEmpty(ExceptionType); } }

        public Exception? Exception
        {
            get
            {
                return IsOk ? null : Activator.CreateInstance(Type.GetType(ExceptionType), ExceptionMessage) as Exception;
            }
        }

        public FileAckMessage(string filename, Exception? exception = null)
            : base(filename)
        {
            if (exception != null)
            {
                ExceptionType = exception.GetType().FullName!;
                ExceptionMessage = exception.ToString();
            }
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            ExceptionType = stream.ReadString();
            ExceptionMessage = stream.ReadString();
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteString(ExceptionType);
            stream.WriteString(ExceptionMessage);
        }

        public void ThrowOnError()
        {
            if (!IsOk)
            {
                throw Exception!;
            }
        }
    }
}
