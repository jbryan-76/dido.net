namespace DidoNet.IO
{
    internal class FileChunkMessage : FileAckMessage
    {
        public byte[] Bytes { get; private set; } = new byte[0];

        public bool EOF { get { return Position == Length; } }

        public FileChunkMessage() { }

        public FileChunkMessage(string filename)
            : base(filename, -1, -1) { }

        public FileChunkMessage(string filename, byte[] bytes, long position, long length)
            : base(filename, position, length)
        {
            Bytes = bytes;
        }

        public FileChunkMessage(string filename, Exception exception)
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
            int length = Bytes?.Length ?? 0;
            stream.WriteInt32BE(length);
            if (length > 0)
            {
                stream.Write(Bytes);
            }
        }
    }
}
