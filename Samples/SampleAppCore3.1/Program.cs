using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static void PrintUse()
    {
        var appName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
        Console.WriteLine($"Use: {appName} runner_host");
        Console.WriteLine($"NOTE: A Dido.Runner must be running at the indicated host using the sample dido-localhost certificate.");
    }

    public static async Task Main(string[] args)
    {
        // verify the command line
        if (args.Length < 1)
        {
            PrintUse();
            return;
        }

        // create the dido configuration, which explicitly uses the sample self-signed certificate included in the repository
        // and connects to an explicit runner
        var conf = new DidoNet.Configuration
        {
            ServerCertificateValidationPolicy = DidoNet.ServerCertificateValidationPolicies.Thumbprint,
            ServerCertificateThumbprint = "06c66fae6f5f6fbc0c5a882832963a7ec0351293",
            ExecutionMode = DidoNet.ExecutionModes.Remote,
            RunnerUri = new UriBuilder(args[0]).Uri
        };

        // do the work
        Console.WriteLine($"Starting remote execution of a sample task on {conf.RunnerUri}...");
        var result = await DidoNet.Dido.RunAsync((context) => Work.DoSomethingLongAndExpensive(64), conf);
        Console.WriteLine($"Result: duration={result.Duration} average={result.Average} xml={result.Xml}");
    }
}