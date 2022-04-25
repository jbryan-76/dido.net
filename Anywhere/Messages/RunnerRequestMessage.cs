namespace DidoNet
{
    internal class RunnerRequestMessage : IMessage
    {
        public int Priority { get; set; } = 0;

        public OSPlatforms Platform { get; set; } = OSPlatforms.Unknown;

        public string Label { get; set; } = "";

        public string[] Tags { get; set; } = new string[0];

        public RunnerRequestMessage() { }

        public RunnerRequestMessage(int priority, OSPlatforms platform, string label, string[] tags)
        {
            Priority = priority;
            Platform = platform;
            Label = label;
            Tags = tags.ToArray();
        }

        public void Read(Stream stream)
        {
            Priority = stream.ReadInt32BE();
            Platform = Enum.Parse<OSPlatforms>(stream.ReadString());
            Label = stream.ReadString();
            Tags = stream.ReadArray((s) => s.ReadString());
        }

        public void Write(Stream stream)
        {
            stream.WriteInt32BE(Priority);
            stream.WriteString(Platform.ToString());
            stream.WriteString(Label);
            stream.WriteArray(Tags, (s, item) => s.WriteString(item));
        }
    }
}