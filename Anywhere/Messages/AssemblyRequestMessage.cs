namespace DidoNet
{
    internal class AssemblyRequestMessage : IMessage
    {
        public string? AssemblyName { get; private set; }

        public AssemblyRequestMessage() { }

        public AssemblyRequestMessage(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        public void Read(Stream stream)
        {
            AssemblyName = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            if (AssemblyName != null)
            {
                stream.WriteString(AssemblyName);
            }
        }
    }
}