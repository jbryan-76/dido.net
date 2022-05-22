namespace DidoNet.IO
{
    internal class FileSetLengthMessage : FileMessageBase
    {
        public long Length { get; set; }

        public FileSetLengthMessage() { }

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
