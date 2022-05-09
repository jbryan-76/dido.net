﻿using System.IO;

class Program
{
    static void PrintUse()
    {
        var appName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
        Console.WriteLine($"Use: {appName} runner_host");
    }

    public static async Task Main(string[] args)
    {
        if (args.Length < 1)
        {
            PrintUse();
            return;
        }

        var conf = new DidoNet.Configuration
        {
            ServerValidationPolicy = DidoNet.ServerCertificateValidationPolicies.Thumbprint,
            ServerCertificateThumbprint = "06c66fae6f5f6fbc0c5a882832963a7ec0351293",
            ExecutionMode = DidoNet.ExecutionModes.Remote,
            RunnerUri = new UriBuilder(args[0]).Uri
        };
        Console.WriteLine($"Starting remote execution of a sample task on {conf.RunnerUri}...");
        var result = await DidoNet.Dido.RunAsync((context) => Work.DoSomethingLongAndExpensive(64), conf);

        Console.WriteLine($"Result: duration={result.Duration} average={result.Average}");
    }
}