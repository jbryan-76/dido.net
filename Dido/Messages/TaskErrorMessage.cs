using System;
using System.IO;

namespace DidoNet
{
    /// <summary>
    /// A message sent from a runner to an application when a task fails to complete successfully. 
    /// </summary>
    internal class TaskErrorMessage : IErrorMessage
    {
        public enum Categories
        {
            General,
            Deserialization,
            Invocation
        }

        public Categories Category { get; private set; }

        public string ExceptionType { get; private set; } = string.Empty;

        public string Message { get; private set; } = string.Empty;

        public Exception Exception
        {
            get
            {
                Exception exception;
                try
                {
                    exception = Activator.CreateInstance(Type.GetType(ExceptionType)!, Message) as Exception ?? new Exception();
                }
                catch (Exception)
                {
                    exception = new InvalidOperationException($"Could not create exception of type '{ExceptionType}' with error message {Message}.");
                }

                switch (Category)
                {
                    case TaskErrorMessage.Categories.General:
                        return new TaskGeneralException("Error while executing remote expression", exception);
                    case TaskErrorMessage.Categories.Deserialization:
                        return new TaskDeserializationException("Error while executing remote expression", exception);
                    case TaskErrorMessage.Categories.Invocation:
                        return new TaskInvocationException("Error while executing remote expression", exception);
                    default:
                        return new InvalidOperationException($"Task error category {Category} is unknown");
                }
            }
        }

        public TaskErrorMessage() { }

        public TaskErrorMessage(Categories category, Exception exception)
        {
            Category = category;
            Message = exception.ToString();
            ExceptionType = exception.GetType().FullName!;
        }

        public void Read(Stream stream)
        {
            Category = Enum.Parse<Categories>(stream.ReadString());
            ExceptionType = stream.ReadString();
            Message = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            stream.WriteString(Category.ToString());
            stream.WriteString(ExceptionType);
            stream.WriteString(Message);
        }
    }
}