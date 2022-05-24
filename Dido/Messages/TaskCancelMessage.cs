namespace DidoNet
{
    internal class TaskCancelMessage : IMessage
    {
        public string Message { get; set; } = string.Empty;

        public TaskCancelMessage()
        //: this("The task was canceled.")
        { }

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