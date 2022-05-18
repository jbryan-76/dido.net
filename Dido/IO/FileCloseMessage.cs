namespace DidoNet.IO
{
    internal class FileCloseMessage : FileMessageBase
    {
        public FileCloseMessage(string filename)
            : base(filename) { }
    }
}
