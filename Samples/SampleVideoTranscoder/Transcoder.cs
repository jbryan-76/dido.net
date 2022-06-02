using System.Diagnostics;

static class Transcoder
{
    public static async Task<double> Transcode(DidoNet.ExecutionContext context, string sourceFile, string destinationFile)
    {
        Console.WriteLine("Caching source file...");
        var cachedSrc = await context.File.CacheAsync(sourceFile, Path.GetFileName(sourceFile));
        Console.WriteLine("Caching ffmpeg...");
        var ffmpegPath = await context.File.CacheAsync(Constants.Ffmpeg, "local_ffmpeg.exe");

        var tempDestination = Path.GetTempFileName();
        if (File.Exists(tempDestination))
        {
            File.Delete(tempDestination);
        }
        tempDestination = Path.ChangeExtension(tempDestination, Path.GetExtension(sourceFile));

        Console.WriteLine("Transcoding...");
        var start = DateTime.Now;
        using (Process proc = new Process())
        {
            proc.StartInfo.FileName = ffmpegPath;
            proc.StartInfo.Arguments = $"-i {cachedSrc} {tempDestination}";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            proc.StartInfo.CreateNoWindow = true;
            proc.Start();
            string error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (!File.Exists(tempDestination))
            {
                throw new InvalidOperationException($"Unable to transcode file. Ffmpeg error: {error}");
            }
        }

        var duration = (DateTime.Now - start).TotalSeconds;

        Console.WriteLine("Storing destination file...");
        await context.File.StoreAsync(tempDestination, destinationFile);

        return duration;
    }
}