namespace AnywhereNET
{
    public class RunnerConfiguration
    {
        // TODO: configure to indicate OS environment
        // TODO: configure to indicate support for queued vs concurrent vs single requests

        /// <summary>
        /// The uri for the orchestrator service used to monitor and manage runners.
        /// </summary>
        public Uri? OrchestratorUri { get; set; } = null;
    }
}