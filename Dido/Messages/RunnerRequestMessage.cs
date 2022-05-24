namespace DidoNet
{
    /// <summary>
    /// A message to request an available runner from a mediator to run a task.
    /// </summary>
    internal class RunnerRequestMessage : IMessage
    {
        // TODO: incorporate a "priority" for the task or runner?
        //public int Priority { get; set; } = 0;

        /// <summary>
        /// Indicates the runner OS platform the task should run on.
        /// If not OSPlatforms.Unknown, the task can only be run on a runner
        /// with a matching OS platform.
        /// </summary>
        public OSPlatforms Platform { get; set; } = OSPlatforms.Unknown;

        /// <summary>
        /// Indicates the specific label of the runner the task should run on.
        /// If provided, the task can only run on a runner with a matching label.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Indicates a set of tags to filter runners the task should run on.
        /// If provided, the task can only run on a runner where the set intersection
        /// with the runner's tags is non-empty.
        /// <para/>This is useful for specifying CPU, GPU, RAM, or other hardware or 
        /// environment-specific capabilities.
        /// </summary>
        public string[] Tags { get; set; } = new string[0];

        public RunnerRequestMessage() { }

        public RunnerRequestMessage(OSPlatforms platform, string label, string[] tags)
        {
            Platform = platform;
            Label = label;
            Tags = tags.ToArray();
        }

        public void Read(Stream stream)
        {
            Platform = Enum.Parse<OSPlatforms>(stream.ReadString());
            Label = stream.ReadString();
            Tags = stream.ReadArray((s) => s.ReadString());
        }

        public void Write(Stream stream)
        {
            stream.WriteString(Platform.ToString());
            stream.WriteString(Label);
            stream.WriteArray(Tags, (s, item) => s.WriteString(item));
        }
    }
}