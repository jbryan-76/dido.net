using System;
using System.IO;

namespace DidoNet
{
    /// <summary>
    /// A message sent from a runner to an application when a task is canceled.
    /// </summary>
    internal class TaskCancelMessage : IErrorMessage
    {
        public string Message { get; set; } = string.Empty;

        public Exception Exception { get { return new OperationCanceledException(Message); } }

        public TaskCancelMessage() { }

        public TaskCancelMessage(string message)
        {
            Message = message;
        }

        public void Read(Stream stream)
        {
            Message = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(Message);
        }
    }
}