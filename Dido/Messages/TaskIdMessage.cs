using System.IO;

namespace DidoNet
{
    /// <summary>
    /// A message sent from a runner to an application with the specific ids of a task the runner is executing.
    /// </summary>
    internal class TaskIdMessage : IMessage
    {
        public string RunnerId { get; set; } = string.Empty;

        public string TaskId { get; set; } = string.Empty;

        public TaskIdMessage() { }

        public TaskIdMessage(
            string runnerId,
            string taskId)
        {
            RunnerId = runnerId;
            TaskId = taskId;
        }

        public void Read(Stream stream)
        {
            RunnerId = stream.ReadString();
            TaskId = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(RunnerId);
            stream.WriteString(TaskId);
        }
    }
}