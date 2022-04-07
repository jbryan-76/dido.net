using System.Text;

namespace AnywhereNET
{
    internal class AssemblyResponseMessage : IMessage
    {
        public enum ContentTypes
        {
            Assembly,
            Error
        }

        public byte[] Bytes { get; private set; } = new byte[0];

        public ContentTypes ContentType { get; private set; }

        public AssemblyResponseMessage() { }

        // TODO: support "not found" errors too

        public AssemblyResponseMessage(byte[] bytes)
        {
            ContentType = ContentTypes.Assembly;
            Bytes = bytes;
        }

        public AssemblyResponseMessage(Exception ex)
        {
            ContentType = ContentTypes.Error;
            Bytes = Encoding.UTF8.GetBytes(ex.ToString());
        }

        public void Read(Stream stream)
        {
            // TODO: more robust conversion back to enum?
            ContentType = (ContentTypes)stream.ReadInt32BE();
            int length = stream.ReadInt32BE();
            Bytes = stream.ReadBytes(length);
        }

        public void Write(Stream stream)
        {
            stream.WriteInt32BE((int)ContentType);
            stream.WriteInt32BE(Bytes?.Length ?? 0);
            stream.Write(Bytes);
        }
    }
}