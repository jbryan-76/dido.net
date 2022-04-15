namespace AnywhereNET
{
    // TODO: TaskCancelMessage
    // TODO: TaskTimeoutMessage
    // TODO: RunnerBusyMessage

    internal class TaskErrorMessage : IMessage
    {
        public enum ErrorTypes
        {
            General,
            Deserialization,
            Invokation
        }

        public ErrorTypes ErrorType { get; private set; }

        public string Error { get; private set; } = "";

        public TaskErrorMessage() { }

        public TaskErrorMessage(string error, ErrorTypes errorType)
        {
            ErrorType = errorType;
            Error = error;
        }

        public TaskErrorMessage(Exception ex, ErrorTypes errorType)
            : this(ex.ToString(), errorType) { }

        public void Read(Stream stream)
        {
            ErrorType = Enum.Parse<ErrorTypes>(stream.ReadString());
            Error = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(ErrorType.ToString());
            stream.WriteString(Error);
        }
    }
}