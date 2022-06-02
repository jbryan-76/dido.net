using System.Diagnostics;

class Program
{
    static void PrintUse()
    {
        var appName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
        Console.WriteLine($"Use: {appName} runner_host source_file destination_file");
    }

    static void CheckForFfmpeg()
    {
        // check if it's in the current directory
        if (File.Exists(Constants.Ffmpeg))
        {
            return;
        }

        Console.WriteLine($"Error: can not find ffmpeg program. Download a static build of ffmpeg.exe from https://www.ffmpeg.org/ and place in this application's directory.");

        Environment.Exit(0);
    }

    public static async Task Main(string[] args)
    {
        CheckForFfmpeg();

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

        Console.WriteLine($"Starting remote execution of a sample task on {conf.RunnerUri}...");

        var task = await DidoNet.Dido.RunAsync((context) => Transcoder.Transcode(context, args[1], args[2]), conf);
        var duration = await task;

        Console.WriteLine($"Transcoding duration={duration}");
    }
}
