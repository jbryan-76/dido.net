namespace AnywhereNET
{
    public class RunnerConfiguration
    {
        /// <summary>
        /// The optional label for the runner.
        /// </summary>
        public string Label { get; set; } = "";

        /// <summary>
        /// The set of optional tags for the runner.
        /// </summary>
        public string[] Tags { get; set; } = new string[0];

        /// <summary>
        /// The maximum number of processing slots to allow for work request processing.
        /// This should roughly correlate to the number of CPU cores available.
        /// <para/>Allowed values are:
        /// <para/>Less than or equal to zero (default) = Auto (will be set to the smaller of 1 and the available 
        /// number of cpu cores present on the system).
        /// <para/>Anything else indicates the maximum number of slots.
        /// </summary>
        public int MaxSlots { get; set; } = 0;

        /// <summary>
        /// The maximum number of pending work requests to accept before rejecting.
        /// <para/>Allowed values are:
        /// <para/>Less than zero = Unlimited (up to the number of simultaneous connections allowed by the OS).
        /// <para/>Zero (default) = Work cannot be queued: each request is fully processed on a slot 
        /// before accepting another.
        /// <para/>Anything else indicates the maximum number of work requests.
        /// </summary>
        public int MaxQueue { get; set; } = 0;

        /// <summary>
        /// The uri for the orchestrator service used to monitor and manage runners.
        /// If null, the runner will operate in an independent/isolated mode.
        /// </summary>
        public Uri? OrchestratorUri { get; set; } = null;
    }
}