using System.Runtime.InteropServices;

namespace AnywhereNET
{
    internal class RunnerBusyMessage : IMessage
    {
        public void Read(Stream stream)
        {
        }

        public void Write(Stream stream)
        {
        }
    }

    internal class RunnerErrorMessage : IMessage
    {
        public void Read(Stream stream)
        {
        }

        public void Write(Stream stream)
        {
        }
    }

    internal class RunnerStatusMessage : IMessage
    {
        /// <summary>
        /// The set of available runner states.
        /// </summary>
        public enum States
        {
            Starting,
            Ready,
            Paused,
            Stopping
        }

        public States State { get; set; }

        public int RunningTasks { get; set; } = 0;

        public int QueueLength { get; set; } = 0;

        //public int AverageTaskDurationInSeconds { get; set; } // TODO?

        public RunnerStatusMessage() { }

        public RunnerStatusMessage(States status, int availableSlots, int queueLength)
        {
            State = status;
            RunningTasks = availableSlots;
            QueueLength = queueLength;
        }

        public void Read(Stream stream)
        {
            State = Enum.Parse<States>(stream.ReadString());
            RunningTasks = stream.ReadInt32BE();
            QueueLength = stream.ReadInt32BE();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(State.ToString());
            stream.WriteInt32BE(RunningTasks);
            stream.WriteInt32BE(QueueLength);
        }
    }
}