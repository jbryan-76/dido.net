namespace DidoNet
{
    internal class AssemblyErrorMessage : IMessage
    {
        public string Error { get; private set; } = String.Empty;

        public AssemblyErrorMessage() { }

        public AssemblyErrorMessage(string error)
        {
            Error = error;
        }

        public AssemblyErrorMessage(Exception ex)
            : this(ex.ToString()) { }

        public void Read(Stream stream)
        {
            Error = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(Error);
        }
    }
}