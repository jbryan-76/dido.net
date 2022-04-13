using System.Runtime.InteropServices;

namespace AnywhereNET
{
    internal class RunnerStatusMessage : IMessage
    {
        public enum OSPlatforms
        {
            Unknown,
            FreeBSD,
            Linux,
            OSX,
            Windows
        }

        public enum Statuses
        {
            Starting,
            Stopping,
            BeginJob,
            UpdateJob,
            FinishJob
        }

        public Statuses Status { get; set; }

        public OSPlatforms Platform { get; set; } = OSPlatforms.Unknown;

        public string OSVersion { get; set; } = "";

        public int AvailableSlots { get; set; } = 0;

        public int QueueLength { get; set; } = 0;

        public string Label { get; set; } = "";

        public string[] Tags { get; set; } = new string[0];

        //public int AverageTaskDurationInSeconds { get; set; }

        public RunnerStatusMessage() { }

        public RunnerStatusMessage(Statuses status, int availableSlots,
            int queueLength, string label, string[] tags)
        {
            Status = status;
            AvailableSlots = availableSlots;
            QueueLength = queueLength;
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
            Status = Enum.Parse<Statuses>(stream.ReadString());
            Platform = Enum.Parse<OSPlatforms>(stream.ReadString());
            OSVersion = stream.ReadString();
            AvailableSlots = stream.ReadInt32BE();
            QueueLength = stream.ReadInt32BE();
            Label = stream.ReadString();
            Tags = stream.ReadArray((stream) => stream.ReadString());
        }

        public void Write(Stream stream)
        {
            stream.WriteString(Status.ToString());
            stream.WriteString(Platform.ToString());
            stream.WriteString(OSVersion);
            stream.WriteInt32BE(AvailableSlots);
            stream.WriteInt32BE(QueueLength);
            stream.WriteString(Label);
            stream.WriteArray(Tags, (stream, tag) => stream.WriteString(tag));
        }
    }
}