using System;
using System.IO;

namespace DidoNet
{
    internal class RunnerBusyMessage : IErrorMessage
    {
        public string Message { get; set; } = string.Empty;

        public Exception Exception { get { return new RunnerBusyException(Message); } }

        public RunnerBusyMessage() { }

        public RunnerBusyMessage(string message)
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