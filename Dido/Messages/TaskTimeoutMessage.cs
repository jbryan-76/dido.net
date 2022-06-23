using System;
using System.IO;

namespace DidoNet
{
    internal class TaskTimeoutMessage : IErrorMessage
    {
        public string Message { get; set; } = string.Empty;

        public Exception Exception { get { return new TimeoutException(Message); } }

        public TaskTimeoutMessage() { }

        public TaskTimeoutMessage(string message)
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