namespace DidoNet.IO
{
    internal class FileOpenMessage : FileMessageBase
    {
        public ushort ChannelNumber { get; set; }

        public FileMode Mode { get; set; }

        public FileAccess Access { get; set; }

        public FileShare Share { get; set; }

        public FileOpenMessage() { }

        public FileOpenMessage(string filename, ushort channelNumber, FileMode mode, FileAccess access, FileShare share)
            : base(filename)
        {
            ChannelNumber = channelNumber;
            Mode = mode;
            Access = access;
            Share = share;
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            ChannelNumber = stream.ReadUInt16BE();
            Mode = Enum.Parse<FileMode>(stream.ReadString());
            Access = Enum.Parse<FileAccess>(stream.ReadString());
            Share = Enum.Parse<FileShare>(stream.ReadString());
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteUInt16BE(ChannelNumber);
            stream.WriteString(Mode.ToString());
            stream.WriteString(Access.ToString());
            stream.WriteString(Share.ToString());
        }
    }
}
