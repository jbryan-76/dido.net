namespace DidoNet.IO
{
    internal class FileCloseMessage : FileMessageBase
    {
        public FileCloseMessage() { }

        public FileCloseMessage(string filename)
            : base(filename) { }
    }
}
