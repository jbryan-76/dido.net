using System.Diagnostics;

/// <summary>
/// A simple class to demonstrate the Dido Proxy IO API.
/// </summary>
static class Transcoder
{
    /// <summary>
    /// Use the Proxy IO API exposed by the ExecutionContext to retrieve a source video file and an ffmpeg
    /// executable from the application, transcode the video, and store the result back with the application. 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sourceFile"></param>
    /// <param name="destinationFile"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static async Task<double> Transcode(DidoNet.ExecutionContext context, string sourceFile, string destinationFile)
    {
        // cache the source file from the application to the local file-system
        Console.WriteLine("Caching source file...");
        var cachedSrc = await context.File.CacheAsync(sourceFile, Path.GetFileName(sourceFile));

        // cache the ffmpeg executable from the application to the local file-system
        Console.WriteLine("Caching ffmpeg...");
        var ffmpegPath = await context.File.CacheAsync(Constants.Ffmpeg, "local_ffmpeg.exe");

        // create a temporary file for the transcoded result
        var tempDestination = Path.GetTempFileName();
        if (File.Exists(tempDestination))
        {
            File.Delete(tempDestination);
        }
        tempDestination = Path.ChangeExtension(tempDestination, Path.GetExtension(sourceFile));

        // transcode...
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

        // store the result back to the destination on the application file-system
        Console.WriteLine("Storing destination file...");
        await context.File.StoreAsync(tempDestination, destinationFile);

        return duration;
    }
}