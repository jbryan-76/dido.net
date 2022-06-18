using System.IO;

namespace DidoNet
{
    internal class TaskTimeoutMessage : IMessage
    {
        public string Message { get; set; } = string.Empty;

        public TaskTimeoutMessage()
        //: this("The task did not complete in the allotted time.")
        { }

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