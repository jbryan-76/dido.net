using System;
using System.IO;

namespace DidoNet.IO
{
    internal class FileOpenMessage : FileMessageBase
    {
        public string ChannelId { get; set; }

        public FileMode Mode { get; set; }

        public FileAccess Access { get; set; }

        public FileShare Share { get; set; }

        public FileOpenMessage() { }

        public FileOpenMessage(string filename, string channelNumber, FileMode mode, FileAccess access, FileShare share)
            : base(filename)
        {
            ChannelId = channelNumber;
            Mode = mode;
            Access = access;
            Share = share;
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            ChannelId = stream.ReadString();
            Mode = Enum.Parse<FileMode>(stream.ReadString());
            Access = Enum.Parse<FileAccess>(stream.ReadString());
            Share = Enum.Parse<FileShare>(stream.ReadString());
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteString(ChannelId);
            stream.WriteString(Mode.ToString());
            stream.WriteString(Access.ToString());
            stream.WriteString(Share.ToString());
        }
    }
}
