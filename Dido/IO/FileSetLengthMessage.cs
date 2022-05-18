namespace DidoNet.IO
{
    //internal class FileSeekMessage : FileMessageBase
    //{
    //    public long Offset { get; set; }

    //    public SeekOrigin Origin { get; set; }

    //    public FileSeekMessage(string filename, long offset, SeekOrigin origin)
    //        : base(filename)
    //    {
    //        Offset = offset;
    //        Origin = origin;
    //    }

    //    public override void Read(Stream stream)
    //    {
    //        base.Read(stream);
    //        Offset = stream.ReadInt64BE();
    //        Origin = Enum.Parse<SeekOrigin>(stream.ReadString());
    //    }

    //    public override void Write(Stream stream)
    //    {
    //        base.Write(stream);
    //        stream.WriteInt64BE(Offset);
    //        stream.WriteString(Origin.ToString());
    //    }
    //}

    internal class FileSetLengthMessage : FileMessageBase
    {
        public long Length { get; set; }

        public FileSetLengthMessage(string filename, long length)
            : base(filename)
        {
            Length = length;
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            Length = stream.ReadInt64BE();
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteInt64BE(Length);
        }
    }
}
