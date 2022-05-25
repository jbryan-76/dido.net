namespace DidoNet.IO
{
    internal class FileInfoMessage : FileAckMessage
    {
        public DateTime CreationTimeUtc { get; set; }
        
        public DateTime LastAccessTimeUtc { get; set; }
        
        public DateTime LastWriteTimeUtc { get; set; }

        public byte[] Hash { get; set; } = new byte[0];

        public FileInfoMessage() { }

        public FileInfoMessage(string filename, long length,
            DateTime createUtc, DateTime accessUtc, DateTime writeUtc,
            byte[]? hash)
            : base(filename, 0, length)
        {
            CreationTimeUtc = createUtc;
            LastAccessTimeUtc = accessUtc;
            LastWriteTimeUtc = writeUtc;
            Hash = hash ?? new byte[0];
        }

        public FileInfoMessage(string filename, Exception exception)
            : base(filename, exception) { }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            CreationTimeUtc = new DateTime(stream.ReadInt64BE(), DateTimeKind.Utc);
            LastAccessTimeUtc = new DateTime(stream.ReadInt64BE(), DateTimeKind.Utc);
            LastWriteTimeUtc = new DateTime(stream.ReadInt64BE(), DateTimeKind.Utc);
            int length = stream.ReadInt32BE();
            Hash = stream.ReadBytes(length);
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteInt64BE(CreationTimeUtc.Ticks);
            stream.WriteInt64BE(LastAccessTimeUtc.Ticks);
            stream.WriteInt64BE(LastWriteTimeUtc.Ticks);
            stream.WriteInt32BE(Hash.Length);
            if (Hash.Length > 0)
            {
                stream.Write(Hash);
            }
        }
    }
}
