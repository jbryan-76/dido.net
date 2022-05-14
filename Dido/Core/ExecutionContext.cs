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
        /// Used to indicate the task should be cancelled.
        /// <para/>Executing tasks should monitor the IsCancellationRequested property
        /// and abort execution if a cancellation is requested.
        /// </summary>
        public CancellationToken Cancel { get; internal set; }

        // TODO: set current try? maxtries?

        // TODO: provide a generic MessageChannel that connects to something on the application side
        public MessageChannel MessageChannel { get; internal set; }

        // TODO: add methods to access files, explicitly load assemblies, etc?

        public DidoNet.IO.ProxyFile File;

        public DidoNet.IO.ProxyDirectory Directory;

        // TODO: replace this with the IO layer
        //internal Channel FilesChannel { get; set; }
    }
}
