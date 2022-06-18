using System.IO;

namespace DidoNet.IO
{
    internal class FileMessageBase : IMessage
    {
        public string Filename { get; set; } = string.Empty;

        public FileMessageBase() { }

        public FileMessageBase(string filename)
        {
            Filename = filename;
        }

        public virtual void Read(Stream stream)
        {
            Filename = stream.ReadString();
        }

        public virtual void Write(Stream stream)
        {
            stream.WriteString(Filename);
        }
    }
}
