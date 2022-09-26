using System;

namespace DidoNet
{
    public interface IErrorMessage : IMessage
    {
        string Message { get; }

        Exception Exception { get; }
    }
}