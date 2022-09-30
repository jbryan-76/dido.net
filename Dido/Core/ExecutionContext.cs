using DidoNet.IO;
using System.Threading;

namespace DidoNet
{
    /// <summary>
    /// Provides configuration and utilities for remote execution of tasks.
    /// </summary>
    public class ExecutionContext
    {
        /// <summary>
        /// Indicates how the current expression is being executed.
        /// </summary>
        public ExecutionModes ExecutionMode { get; internal set; }

        // TODO: add support to indicate progress

        /// <summary>
        /// Used to indicate the current expression should be canceled.
        /// <para/>Executing expressions should monitor the IsCancellationRequested property
        /// and abort execution if a cancellation is requested.
        /// </summary>
        public CancellationToken Cancel { get; internal set; }

        // TODO: current try? max tries?

        /// <summary>
        /// A networked proxy for System.IO.File, allowing the current expression to
        /// access the file system of the application.
        /// </summary>
        public RunnerFileProxy File { get; internal set; }

        /// <summary>
        /// A networked proxy for System.IO.Directory, allowing the current expression to
        /// access the file system of the application.
        /// </summary>
        public RunnerDirectoryProxy Directory { get; internal set; }


        // TODO: provide an api to create custom MessageChannels so the application can optionally support interprocess communication
        //public MessageChannel GetChannel()
        //{

        //}

        /// <summary>
        /// The connection from the runner executing a task to the application.
        /// </summary>
        internal Connection Connection { get; set; }
    }
}
