namespace DidoNet
{
    internal class TaskCancelMessage : IMessage
    {
        public string Message { get; set; } = String.Empty;

        public TaskCancelMessage()
        //: this("The task was cancelled.")
        { }

        public TaskCancelMessage(string message)
        {
            Message = message;
        }

        public void Read(Stream stream)
        {
            ThreadHelpers.Debug($"starting to read cancel message");
            Message = stream.ReadString();
            ThreadHelpers.Debug($"read cancel message: {Message}");
        }

        public void Write(Stream stream)
        {
            stream.WriteString(Message);
        }
    }
}