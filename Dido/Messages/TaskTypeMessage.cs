using System;
using System.IO;

namespace DidoNet
{
    internal class TaskTypeMessage : IMessage
    {
        public enum TaskTypes
        {
            Tethered,
            Untethered
        }

        public TaskTypes TaskType { get; set; } = TaskTypes.Tethered;

        public string TaskId { get; set; } = string.Empty;

        public TaskTypeMessage() { }

        public TaskTypeMessage(TaskTypes type, string? id = null)
        {
            TaskType = type;
            TaskId = id ?? "";
        }

        public void Read(Stream stream)
        {
            TaskType = Enum.Parse<TaskTypes>(stream.ReadString());
            TaskId = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(TaskType.ToString());
            stream.WriteString(TaskId);
        }
    }
}