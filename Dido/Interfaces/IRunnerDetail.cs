namespace DidoNet
{
    internal interface IRunnerDetail
    {
        OSPlatforms Platform { get; set; }

        string OSVersion { get; set; }

        string Endpoint { get; set; }

        int MaxTasks { get; set; }

        int MaxQueue { get; set; }

        string Label { get; set; }

        string[] Tags { get; set; }
    }
}