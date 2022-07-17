using System;
using System.IO;

namespace DidoNet.IO
{
    internal class FileTransferMessageBase : FileMessageBase
    {
        public string ChannelId { get; set; }

        public long Length { get; set; }

        public DateTime LastWriteTimeUtc { get; set; }

        public byte[] Hash { get; set; } = new byte[0];

        public FileTransferMessageBase() { }

        public FileTransferMessageBase(string filename, string channelId,
            long? length = null, DateTime? modifiedUtc = null, byte[]? hash = null)
            : base(filename)
        {
            ChannelId = channelId;
            Length = length ?? -1;
            LastWriteTimeUtc = modifiedUtc ?? default(DateTime);
            Hash = hash ?? new byte[0];
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            ChannelId = stream.ReadString();
            Length = stream.ReadInt64BE();
            var ticks = stream.ReadInt64BE();
            LastWriteTimeUtc = new DateTime(ticks, DateTimeKind.Utc);
            Hash = stream.ReadByteArray();
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteString(ChannelId);
            stream.WriteInt64BE(Length);
            stream.WriteInt64BE(LastWriteTimeUtc.Ticks);
            stream.WriteByteArray(Hash);
        }
    }
}
