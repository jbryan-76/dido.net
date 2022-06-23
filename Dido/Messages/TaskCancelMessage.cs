using System;
using System.IO;

namespace DidoNet
{
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