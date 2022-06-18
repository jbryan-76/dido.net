using System.IO;

namespace DidoNet
{
    internal class RunnerNotAvailableMessage : IMessage
    {
        public string Message { get; set; } = string.Empty;

        public RunnerNotAvailableMessage()
        //: this("The runner is busy: all task slots are full and the task queue is either full or disabled.")
        { }

        public RunnerNotAvailableMessage(string message)
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