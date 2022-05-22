namespace DidoNet.IO
{
    internal class FileReadResponseMessage : FileAckMessage
    {
        public byte[] Bytes { get; private set; } = new byte[0];

        public FileReadResponseMessage() { }

        public FileReadResponseMessage(string filename, long filePosition, long fileLength, byte[] bytes)
            : base(filename, filePosition, fileLength)
        {
            Bytes = bytes;
        }

        public FileReadResponseMessage(string filename, Exception exception)
            : base(filename, exception) { }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            int length = stream.ReadInt32BE();
            Bytes = stream.ReadBytes(length);
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteInt32BE(Bytes.Length);
            stream.Write(Bytes);
        }
    }
}
