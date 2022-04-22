namespace DidoNet
{
    internal class RunnerRequestMessage : IMessage
    {
        public int Priority { get; set; } = 0;

        public OSPlatforms Platform { get; set; } = OSPlatforms.Unknown;

        public string Label { get; set; } = "";

        public string[] Tags { get; set; } = new string[0];

        public RunnerRequestMessage() { }

        public RunnerRequestMessage(string assemblyName)
        {
        }

        public void Read(Stream stream)
        {
            Label = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(Label);
        }
    }
}