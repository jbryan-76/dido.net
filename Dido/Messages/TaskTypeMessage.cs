using System;
using System.IO;

namespace DidoNet
{
    internal class TaskTypeMessage : IMessage
    {
        /// <summary>
        /// The types of remote tasks that can be executed by a runner.
        /// </summary>
        public enum TaskTypes
        {
            /// <summary>
            /// The application exists and remains connected throughout the life cycle
            /// execution of the task.
            /// </summary>
            Tethered,

            /// <summary>
            /// TODO: The application exists and is connected for task startup, but otherwise
            /// *may* disconnect and reconnect during the task life cycle.
            /// </summary>
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