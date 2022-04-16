using System.Runtime.InteropServices;

namespace AnywhereNET
{
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

        public int ActiveTasks { get; set; } = 0;

        public int QueueLength { get; set; } = 0;

        //public int AverageTaskDurationInSeconds { get; set; } // TODO?

        public RunnerStatusMessage() { }

        public RunnerStatusMessage(States status, int activeTasks, int queueLength)
        {
            State = status;
            ActiveTasks = activeTasks;
            QueueLength = queueLength;
        }

        public void Read(Stream stream)
        {
            State = Enum.Parse<States>(stream.ReadString());
            ActiveTasks = stream.ReadInt32BE();
            QueueLength = stream.ReadInt32BE();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(State.ToString());
            stream.WriteInt32BE(ActiveTasks);
            stream.WriteInt32BE(QueueLength);
        }
    }
}