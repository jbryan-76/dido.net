using System.Runtime.InteropServices;

namespace DidoNet
{
    /// <summary>
    /// The set of known .NET platforms.
    /// </summary>
    public enum OSPlatforms
    {
        Unknown,
        FreeBSD,
        Linux,
        OSX,
        Windows
    }

    internal class RunnerStartMessage : IMessage, IRunnerDetail
    {
        public OSPlatforms Platform { get; set; } = OSPlatforms.Unknown;

        public string OSVersion { get; set; } = string.Empty;

        public string Endpoint { get; set; } = string.Empty;

        public int MaxTasks { get; set; } = 0;

        public int MaxQueue { get; set; } = 0;

        public string Label { get; set; } = string.Empty;

        public string[] Tags { get; set; } = new string[0];

        public RunnerStartMessage() { }

        public RunnerStartMessage(string endpoint, int maxTasks, int maxQueue, string label, string[] tags)
        {
            Endpoint = endpoint;
            MaxTasks = maxTasks;
            MaxQueue = maxQueue;
            Label = label;
            Tags = tags;

            Platform = RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD) ? OSPlatforms.FreeBSD
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OSPlatforms.Linux
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OSPlatforms.OSX
                : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatforms.Windows
                : OSPlatforms.Unknown;

            OSVersion = RuntimeInformation.OSDescription;
        }

        public void Read(Stream stream)
        {
            Platform = Enum.Parse<OSPlatforms>(stream.ReadString());
            OSVersion = stream.ReadString();
            Endpoint = stream.ReadString();
            MaxTasks = stream.ReadInt32BE();
            MaxQueue = stream.ReadInt32BE();
            Label = stream.ReadString();
            Tags = stream.ReadArray((stream) => stream.ReadString());
        }

        public void Write(Stream stream)
        {
            stream.WriteString(Platform.ToString());
            stream.WriteString(OSVersion);
            stream.WriteString(Endpoint);
            stream.WriteInt32BE(MaxTasks);
            stream.WriteInt32BE(MaxQueue);
            stream.WriteString(Label);
            stream.WriteArray(Tags, (stream, tag) => stream.WriteString(tag));
        }
    }
}