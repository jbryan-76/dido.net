namespace DidoNet
{
    /// <summary>
    /// Tracks the state and status of a connected runner.
    /// </summary>
    internal class RunnerItem : IRunnerDetail, IRunnerStatus
    {
        /// <summary>
        /// The unique id of the runner.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The OS platform the runner is on.
        /// </summary>
        public OSPlatforms Platform { get; set; } = OSPlatforms.Unknown;

        /// <summary>
        /// The OS version for the platform the runner is on.
        /// </summary>
        public string OSVersion { get; set; } = string.Empty;

        /// <summary>
        /// The uri for applications to use to connect to the runner.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// The maximum number of tasks the runner can execute concurrently.
        /// <para/>Legal values are:
        /// <para/>Less than or equal to zero (default) = Auto (will be set to the available 
        /// number of cpu cores present on the system).
        /// <para/>Anything else indicates the maximum number of tasks.
        /// </summary>
        public int MaxTasks { get; set; } = 0;

        /// <summary>
        /// The maximum number of pending tasks the runner can accept and queue before rejecting.
        /// <para/>Legal values are:
        /// <para/>Less than zero = Unlimited (up to the number of simultaneous connections allowed by the OS).
        /// <para/>Zero (default) = Tasks cannot be queued. New tasks are accepted only if fewer than
        /// the maximum number of concurrent tasks are currently running.
        /// <para/>Anything else indicates the maximum number of tasks to queue.
        /// </summary>
        public int MaxQueue { get; set; } = 0;

        /// <summary>
        /// The optional runner label.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// The optional runner tags.
        /// </summary>
        public string[] Tags { get; set; } = new string[0];

        /// <summary>
        /// The last known state of the runner.
        /// </summary>
        public RunnerStates State { get; set; } = RunnerStates.Starting;

        /// <summary>
        /// The number of tasks the runner is currently executing.
        /// By definition this is less than or equal to MaxTasks.
        /// </summary>
        public int ActiveTasks { get; set; } = 0;

        /// <summary>
        /// The number of pending tasks the runner has in its queue.
        /// By definition this is less than or equal to MaxQueue (unless MaxQueue is less than zero).
        /// </summary>
        public int QueueLength { get; set; } = 0;

        /// <summary>
        /// The mediator-runner message channel;
        /// </summary>
        public MessageChannel? Channel { get; set; }

        /// <summary>
        /// Initialize the runner metadata from the provided message.
        /// </summary>
        /// <param name="message"></param>
        public void Init(RunnerStartMessage message, MessageChannel channel)
        {
            Channel = channel;
            Id = message.Id;
            Platform = message.Platform;
            OSVersion = message.OSVersion;
            Endpoint = message.Endpoint;
            MaxTasks = message.MaxTasks;
            MaxQueue = message.MaxQueue;
            Label = message.Label;
            Tags = message.Tags;
            State = RunnerStates.Starting;
        }

        /// <summary>
        /// Update the runner status from the provided message.
        /// </summary>
        /// <param name="message"></param>
        public void Update(RunnerStatusMessage message)
        {
            State = message.State;
            ActiveTasks = message.ActiveTasks;
            QueueLength = message.QueueLength;
        }
    }
}