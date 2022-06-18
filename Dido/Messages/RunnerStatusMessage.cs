
using System;
using System.IO;

namespace DidoNet
{
    /// <summary>
    /// The set of available runner states.
    /// </summary>
    public enum RunnerStates
    {
        Starting,
        Ready,
        Paused,
        Stopping
    }

    internal class RunnerStatusMessage : IMessage, IRunnerStatus
    {
        public RunnerStates State { get; set; } = RunnerStates.Starting;

        public int ActiveTasks { get; set; } = 0;

        public int QueueLength { get; set; } = 0;

        //public int AverageTaskDurationInSeconds { get; set; } // TODO?

        public RunnerStatusMessage() { }

        public RunnerStatusMessage(RunnerStates status, int activeTasks, int queueLength)
        {
            State = status;
            ActiveTasks = activeTasks;
            QueueLength = queueLength;
        }

        public void Read(Stream stream)
        {
            State = Enum.Parse<RunnerStates>(stream.ReadString());
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