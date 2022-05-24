using DidoNet.IO;

namespace DidoNet
{
    /// <summary>
    /// Provides configuration and utilities for remote execution of tasks.
    /// </summary>
    public class ExecutionContext
    {
        /// <summary>
        /// Indicates how the current task is being executed.
        /// </summary>
        public ExecutionModes ExecutionMode { get; internal set; }

        // TODO: add support to indicate progress

        /// <summary>
        /// Used to indicate the task should be canceled.
        /// <para/>Executing tasks should monitor the IsCancellationRequested property
        /// and abort execution if a cancellation is requested.
        /// </summary>
        public CancellationToken Cancel { get; internal set; }

        // TODO: set current try? maxtries?

        // TODO: provide an api to create custom MessageChannels so the application can optionally support interprocess communication
        //public MessageChannel MessageChannel { get; internal set; }

        public RunnerFileProxy File { get; internal set; }

        public RunnerDirectoryProxy Directory { get; internal set; }
    }
}
