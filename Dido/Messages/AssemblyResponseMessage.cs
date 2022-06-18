using System.IO;

namespace DidoNet
{
    internal class AssemblyResponseMessage : IMessage
    {
        public byte[] Bytes { get; private set; } = new byte[0];

        public AssemblyResponseMessage() { }

        public AssemblyResponseMessage(byte[] bytes)
        {
            Bytes = bytes;
        }

        public void Read(Stream stream)
        {
            int length = stream.ReadInt32BE();
            Bytes = stream.ReadBytes(length);
        }

        public void Write(Stream stream)
        {
            stream.WriteInt32BE(Bytes?.Length ?? 0);
            stream.Write(Bytes);
        }
    }
}