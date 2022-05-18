namespace DidoNet.IO
{
    internal class FileReadRequestMessage : FileMessageBase
    {
        public long Position { get; set; }

        public int Count { get; set; }

        public FileReadRequestMessage(string filename, long position, int count)
            : base(filename)
        {
            Position = position;
            Count = count;
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            Position = stream.ReadInt64BE();
            Count = stream.ReadInt32BE();
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteInt64BE(Position);
            stream.WriteInt32BE(Count);
        }
    }
}
