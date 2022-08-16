class Program
{
    // TODO: update this to use either a mediator host or a runner host

    static void PrintUse()
    {
        var appName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
        Console.WriteLine($"Use: {appName} (-runner|-mediator) host");
        Console.WriteLine($"NOTE: A Dido.Runner or Dido.Mediator service must be running at the provided host with the sample dido-localhost certificate.");
    }

    public static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            PrintUse();
            return;
        }

        var hostType = args[0];
        var host = new UriBuilder(args[1]).Uri;

        if (hostType != "-runner" && hostType != "-mediator")
        {
            PrintUse();
            return;
        }

        var conf = new DidoNet.Configuration
        {
            ServerCertificateValidationPolicy = DidoNet.ServerCertificateValidationPolicies.Thumbprint,
            ServerCertificateThumbprint = "06c66fae6f5f6fbc0c5a882832963a7ec0351293",
            ExecutionMode = DidoNet.ExecutionModes.Remote,
            RunnerUri = hostType == "-runner" ? host : null,
            MediatorUri = hostType == "-mediator" ? host : null,
        };

        Console.WriteLine($"Starting remote execution of a sample task on {conf.RunnerUri ?? conf.MediatorUri}...");

        var result = await DidoNet.Dido.RunAsync((context) => Work.DoSomethingLongAndExpensive(64), conf);

        Console.WriteLine($"Result: duration={result.Duration} average={result.Average}");
    }
}