using System.Diagnostics;

class Program
{
    static void PrintUse()
    {
        var appName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
        Console.WriteLine($"Compresses or decompresses a source file to a destination file.");
        Console.WriteLine($"Use: {appName} runner_host source_file destination_file");
        Console.WriteLine($"NOTE: A Dido.Runner must be running at the indicated host using the sample dido-localhost certificate.");
    }

    public static async Task Main(string[] args)
    {
        if (args.Length < 3)
        {
            PrintUse();
            return;
        }

        var conf = new DidoNet.Configuration
        {
            ServerCertificateValidationPolicy = DidoNet.ServerCertificateValidationPolicies.Thumbprint,
            ServerCertificateThumbprint = "06c66fae6f5f6fbc0c5a882832963a7ec0351293",
            ExecutionMode = DidoNet.ExecutionModes.Remote,
            RunnerUri = new UriBuilder(args[0]).Uri
        };

        Console.WriteLine($"Starting remote execution of a sample compression task on {conf.RunnerUri}...");

        Task<double> task;
        if (string.Compare(Path.GetExtension(args[1]), Compressor.Extension, true) == 0)
        {
            task = await DidoNet.Dido.RunAsync((context) => Compressor.Inflate(context, args[1], args[2]), conf);
        }
        else
        {
            task = await DidoNet.Dido.RunAsync((context) => Compressor.Deflate(context, args[1], args[2]), conf);
        }
        var duration = await task;

        Console.WriteLine($"Compressing duration={duration}");
    }
}
