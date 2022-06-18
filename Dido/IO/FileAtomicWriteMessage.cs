using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DidoNet.IO
{
    internal class FileAtomicWriteMessage : FileMessageBase
    {
        public byte[] Bytes { get; set; } = new byte[0];

        public bool Append { get; set; }

        public FileAtomicWriteMessage() { }

        public FileAtomicWriteMessage(string filename, byte[] bytes, bool append)
            : base(filename)
        {
            Bytes = bytes;
            Append = append;
        }

        public FileAtomicWriteMessage(string filename, IEnumerable<string> contents, bool append, Encoding? encoding = null)
            : base(filename)
        {
            encoding ??= new UTF8Encoding(false, true);
            Bytes = encoding.GetBytes(string.Join(System.Environment.NewLine, contents) + System.Environment.NewLine);
            Append = append;
        }

        public FileAtomicWriteMessage(string filename, string contents, bool append, Encoding? encoding = null)
            : base(filename)
        {
            encoding ??= new UTF8Encoding(false, true);
            Bytes = encoding.GetBytes(contents);
            Append = append;
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            Append = stream.ReadBoolean();
            int length = stream.ReadInt32BE();
            Bytes = stream.ReadBytes(length);
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteBoolean(Append);
            stream.WriteInt32BE(Bytes?.Length ?? 0);
            stream.Write(Bytes);
        }
    }
}
