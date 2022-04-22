namespace DidoNet
{
    internal interface IRunnerStatus
    {
        RunnerStates State { get; set; }

        int ActiveTasks { get; set; }

        int QueueLength { get; set; }
    }
}