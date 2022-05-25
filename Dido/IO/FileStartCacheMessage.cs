namespace DidoNet.IO
{
    internal class FileStartCacheMessage : FileMessageBase
    {
        public ushort ChannelNumber { get; set; }

        public long Length { get; set; }

        public DateTime LastWriteTimeUtc { get; set; }

        public byte[] Hash { get; set; } = new byte[0];

        public FileStartCacheMessage() { }

        public FileStartCacheMessage(string filename, ushort channelNumber,
            long? length = null, DateTime? modifiedUtc = null, byte[]? hash = null)
            : base(filename)
        {
            ChannelNumber = channelNumber;
            Length = length ?? -1;
            LastWriteTimeUtc = modifiedUtc ?? default(DateTime);
            Hash = hash ?? new byte[0];
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            ChannelNumber = stream.ReadUInt16BE();
            Length = stream.ReadInt64BE();
            var ticks = stream.ReadInt64BE();
            LastWriteTimeUtc = new DateTime(ticks, DateTimeKind.Utc);
            int length = stream.ReadInt32BE();
            Hash = stream.ReadBytes(length);
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteUInt16BE(ChannelNumber);
            stream.WriteInt64BE(Length);
            stream.WriteInt64BE(LastWriteTimeUtc.Ticks);
            stream.WriteInt32BE(Hash.Length);
            if (Hash.Length > 0)
            {
                stream.Write(Hash);
            }
        }
    }
}
