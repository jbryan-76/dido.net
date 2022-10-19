using System.Diagnostics;

class Program
{
    static void PrintUse()
    {
        var appName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
        Console.WriteLine($"Transcodes a source video file to a destination video file.");
        Console.WriteLine($"Use: {appName} runner_host source_video_file destination_video_file");
        Console.WriteLine($"NOTE: A Dido.Runner must be running at the indicated host using the sample dido-localhost certificate.");
    }

    static void CheckForFfmpeg()
    {
        // check if ffmpeg is in the current directory
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

        // verify the command line
        if (args.Length < 3)
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
        Console.WriteLine($"Starting remote execution of a sample transcoding task on {conf.RunnerUri}...");
        var task = await DidoNet.Dido.RunAsync((context) => Transcoder.Transcode(context, args[1], args[2]), conf);
        var duration = await task;
        Console.WriteLine($"Transcoding duration={duration}");
    }
}
