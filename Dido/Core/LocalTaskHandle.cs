using NLog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DidoNet
{
    public class LocalTaskHandle: ITaskHandle
    {
        public string RunnerId { get; private set; }

        public string TaskId { get; private set; }

        public Uri RunnerUri { get; private set; }

        public bool IsFinished { get { return Interlocked.Read(ref Finished) != 0; } }

        public object? Result { get; private set; } = null;

        public Exception? Error { get; private set; } = null;

        private long Finished = 0;

        /// <summary>
        /// The class logger instance.
        /// </summary>
        private readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public void WaitUntilFinished()
        {
            //FinishedEvent.WaitOne();
        }

        public void ThrowIfError()
        {
            if (Error != null)
            {
                throw Error;
            }
        }

        public T GetResult<T>()
        {
            if (Result == null)
            {
                throw new InvalidCastException($"The local task {nameof(Result)} value is null.");
            }

            // Result will always be a non-task value.
            // if the desired type is a Task, wrap the result in a properly typed
            // Task so it can be awaited.
            var intendedType = typeof(T);
            if (intendedType.IsGenericType && intendedType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // convert the result to the expected generic task type
                var resultGenericType = intendedType.GenericTypeArguments[0];
                var resultValue = Convert.ChangeType(Result, resultGenericType);
                // then construct a Task.FromResult using the expected generic type
                var method = typeof(Task).GetMethod(nameof(Task.FromResult), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                method = method!.MakeGenericMethod(resultGenericType);
                return (T)method!.Invoke(null, new[] { resultValue })!;
            }
            else
            {
                return (T)Convert.ChangeType(Result, typeof(T));
            }
        }

        public void Dispose()
        {
            //RunnerConnection?.Dispose();
            //FinishedEvent.Dispose();
        }
    }
}
