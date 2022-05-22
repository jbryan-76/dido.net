using System.Text;

namespace DidoNet.IO
{
    internal class FileWriteMessage : FileMessageBase
    {
        public long Position { get; set; }

        public byte[] Bytes { get; private set; } = new byte[0];

        public FileWriteMessage() { }

        public FileWriteMessage(string filename, long position, byte[] bytes)
            : base(filename)
        {
            Position = position;
            Bytes = bytes;
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            Position = stream.ReadInt64BE();
            int length = stream.ReadInt32BE();
            Bytes = stream.ReadBytes(length);
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteInt64BE(Position);
            stream.WriteInt32BE(Bytes?.Length ?? 0);
            stream.Write(Bytes);
        }
    }
}
