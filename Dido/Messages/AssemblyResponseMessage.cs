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
            Bytes = stream.ReadByteArray();
        }

        public void Write(Stream stream)
        {
            stream.WriteByteArray(Bytes);
        }
    }
}