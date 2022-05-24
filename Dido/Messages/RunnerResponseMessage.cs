namespace DidoNet
{
    internal class RunnerResponseMessage : IMessage
    {
        public string Endpoint { get; set; } = string.Empty;

        public RunnerResponseMessage() { }

        public RunnerResponseMessage(string endpoint)
        {
            Endpoint = endpoint;
        }

        public void Read(Stream stream)
        {
            Endpoint = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(Endpoint);
        }
    }
}