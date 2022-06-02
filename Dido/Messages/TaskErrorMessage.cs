namespace DidoNet
{
    internal class TaskErrorMessage : IMessage
    {
        public enum Categories
        {
            General,
            Deserialization,
            Invocation
        }

        public Categories Category { get; private set; }

        public string ExceptionType { get; private set; } = string.Empty;

        public string ExceptionMessage { get; private set; } = string.Empty;

        public Exception? Exception
        {
            get
            {
                return string.IsNullOrEmpty(ExceptionType) 
                    ? null 
                    : Activator.CreateInstance(Type.GetType(ExceptionType)!, ExceptionMessage) as Exception;
            }
        }

        public TaskErrorMessage() { }

        public TaskErrorMessage(Categories category, Exception exception)
        {
            Category = category;
            ExceptionMessage = exception.ToString();
            ExceptionType = exception.GetType().FullName!;
        }

        public void Read(Stream stream)
        {
            Category = Enum.Parse<Categories>(stream.ReadString());
            ExceptionType = stream.ReadString();
            ExceptionMessage = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(Category.ToString());
            stream.WriteString(ExceptionType);
            stream.WriteString(ExceptionMessage);
        }
    }
}