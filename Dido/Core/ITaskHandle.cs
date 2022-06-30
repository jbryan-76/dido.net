using System;

namespace DidoNet
{
    public interface ITaskHandle : IDisposable
    {
        string RunnerId { get; }

        string TaskId { get; }

        Uri RunnerUri { get; }

        bool IsFinished { get; }

        object? Result { get; } 

        Exception? Error { get; }

        void WaitUntilFinished();

        void ThrowIfError();

        T GetResult<T>();
    }
}
