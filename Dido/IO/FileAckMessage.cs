namespace DidoNet.IO
{
    internal class FileAckMessage : FileMessageBase
    {
        public long Position { get; set; }

        public long Length { get; set; }

        public string ExceptionType { get; set; } = string.Empty;

        public string ExceptionMessage { get; set; } = string.Empty;

        public bool IsOk { get { return string.IsNullOrEmpty(ExceptionType); } }

        public Exception? Exception
        {
            get
            {
                return IsOk ? null : Activator.CreateInstance(Type.GetType(ExceptionType)!, ExceptionMessage) as Exception;
            }
        }

        public FileAckMessage() { }

        public FileAckMessage(string filename, long position, long length)
            : base(filename)
        {
            Position = position;
            Length = length;
        }

        public FileAckMessage(string filename, Exception exception)
            : base(filename)
        {
            Position = -1;
            Length = -1;
            ExceptionType = exception.GetType().FullName!;
            ExceptionMessage = exception.ToString();
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            Position = stream.ReadInt64BE();
            Length = stream.ReadInt64BE();
            ExceptionType = stream.ReadString();
            ExceptionMessage = stream.ReadString();
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteInt64BE(Position);
            stream.WriteInt64BE(Length);
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
