using System;

namespace DidoNet
{
    public interface IErrorMessage : IMessage
    {
        Exception Exception { get; }
    }
}