using System;
using System.IO;
using System.Linq;

namespace DidoNet
{
    /// <summary>
    /// A message to request an available runner from a mediator to run a task.
    /// </summary>
    internal class RunnerRequestMessage : IMessage
    {
        // TODO: incorporate a "priority" for the task or runner?
        //public int Priority { get; set; } = 0;

        // TODO: for "tetherless" tasks, incorporate a runner id in the request, that always succeeds if the runner exists

        /// <summary>
        /// Indicates a set of runner OS platforms the task should run on.
        /// If provided, the task can only run on a runner where the set intersection
        /// with the runner's platform is non-empty.
        /// </summary>
        public OSPlatforms[] Platforms { get; set; } = new OSPlatforms[0];

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

        public RunnerRequestMessage(OSPlatforms[] platforms, string label, string[] tags)
        {
            Label = label;
            Platforms = platforms.ToArray();
            Tags = tags.ToArray();
        }

        public void Read(Stream stream)
        {
            Label = stream.ReadString();
            Platforms = stream.ReadArray((s) => Enum.Parse<OSPlatforms>(s.ReadString()));
            Tags = stream.ReadArray((s) => s.ReadString());
        }

        public void Write(Stream stream)
        {
            stream.WriteString(Label);
            stream.WriteArray(Platforms, (s, item) => s.WriteString(item.ToString()));
            stream.WriteArray(Tags, (s, item) => s.WriteString(item));
        }
    }
}