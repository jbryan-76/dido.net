﻿namespace DidoNet.IO
{
    internal class FileMessageBase : IMessage
    {
        public string Filename { get; set; }

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