using System;
using System.IO;

namespace DidoNet.IO
{
    internal class FileInfoMessage : FileAckMessage
    {
        public DateTime LastWriteTimeUtc { get; set; }

        public byte[] Hash { get; set; } = new byte[0];

        /// <summary>
        /// Create a new empty file info message object.
        /// Typically only used by the activator when deserializing an object.
        /// </summary>
        public FileInfoMessage() { }

        /// <summary>
        /// Create a new file info message object indicating that the given file does not exist.
        /// </summary>
        /// <param name="filename"></param>
        public FileInfoMessage(string filename) 
            : base(filename, -1, -1) { }

        public FileInfoMessage(string filename, long length,
            DateTime createUtc, DateTime accessUtc, DateTime writeUtc,
            byte[]? hash)
            : base(filename, 0, length)
        {
            LastWriteTimeUtc = writeUtc;
            Hash = hash ?? new byte[0];
        }

        /// <summary>
        /// Create a new file info message object indicating the provided error.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="exception"></param>
        public FileInfoMessage(string filename, Exception exception)
            : base(filename, exception) { }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            LastWriteTimeUtc = new DateTime(stream.ReadInt64BE(), DateTimeKind.Utc);
            Hash = stream.ReadByteArray();
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteInt64BE(LastWriteTimeUtc.Ticks);
            stream.WriteByteArray(Hash);
        }
    }
}
