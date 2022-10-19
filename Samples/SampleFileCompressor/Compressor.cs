using ICSharpCode.SharpZipLib.Zip.Compression;

/// <summary>
/// A simple class to demonstrate the Dido Proxy IO API and use of 3rd party dependent assemblies.
/// </summary>
static class Compressor
{
    public static readonly string Extension = ".compressed";

    /// <summary>
    /// Use the Proxy IO API exposed by the ExecutionContext to retrieve a source file from the application,
    /// compress it (using the Deflate algorithm), and store it back with the application. 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sourceFile"></param>
    /// <param name="destinationFile"></param>
    /// <returns></returns>
    public static async Task<double> Deflate(DidoNet.ExecutionContext context, string sourceFile, string destinationFile)
    {
        // cache the source file from the application to the local file-system
        Console.WriteLine("Caching source file...");
        var cachedSrc = await context.File.CacheAsync(sourceFile, Path.GetFileName(sourceFile));

        // get a temporary filename 
        var tempDestination = Path.GetTempFileName();
        if (File.Exists(tempDestination))
        {
            File.Delete(tempDestination);
        }

        // force the destination to the proper extension
        if (string.Compare(Path.GetExtension(destinationFile), Extension, true) != 0)
        {
            destinationFile = Path.ChangeExtension(destinationFile, Extension);
        }

        // compress the source file to the temp file
        var start = DateTime.Now;
        using (var src = File.Open(cachedSrc, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var dst = File.Open(tempDestination, FileMode.Create, FileAccess.Write))
        {
            Console.WriteLine("Compressing...");
            DeflateTo(src, dst);
        }

        var duration = (DateTime.Now - start).TotalSeconds;

        // store the temp file back to the destination on the application file-system
        Console.WriteLine("Storing destination file...");
        await context.File.StoreAsync(tempDestination, destinationFile);

        // cleanup
        File.Delete(tempDestination);

        return duration;
    }

    public static async Task<double> Inflate(DidoNet.ExecutionContext context, string sourceFile, string destinationFile)
    {
        // cache the source file from the application to the local file-system
        Console.WriteLine("Caching source file...");
        var cachedSrc = await context.File.CacheAsync(sourceFile, Path.GetFileName(sourceFile));

        // get a temporary filename 
        var tempDestination = Path.GetTempFileName();
        if (File.Exists(tempDestination))
        {
            File.Delete(tempDestination);
        }

        // decompress the source file to the temp file
        Console.WriteLine("Decompressing...");
        var start = DateTime.Now;
        using (var src = File.Open(cachedSrc, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var dst = File.Open(tempDestination, FileMode.Create, FileAccess.Write))
        {
            InflateTo(src, dst);
        }

        var duration = (DateTime.Now - start).TotalSeconds;

        // store the temp file back to the destination on the application file-system
        Console.WriteLine("Storing destination file...");
        await context.File.StoreAsync(tempDestination, destinationFile);

        // cleanup
        File.Delete(tempDestination);

        return duration;
    }

    static int DeflateTo(this Stream input, Stream output, int level = 6, int bufferSize = 1024 * 1024)
    {
        byte[] inBuffer = new byte[bufferSize];
        byte[] outBuffer = new byte[bufferSize];
        int length = 0;

        var deflater = new Deflater(level);
        while (!deflater.IsFinished)
        {
            if (deflater.IsNeedingInput)
            {
                int bytesRead = input.Read(inBuffer, 0, inBuffer.Length);
                if (bytesRead == 0)
                {
                    deflater.Finish();
                }
                else
                {
                    deflater.SetInput(inBuffer, 0, bytesRead);
                }
            }
            int size = deflater.Deflate(outBuffer);
            length += size;
            if (size > 0)
            {
                output.Write(outBuffer, 0, size);
                output.Flush();
            }
        }
        return length;
    }

    static int InflateTo(this Stream input, Stream output, int bufferSize = 1024 * 1024)
    {
        byte[] inBuffer = new byte[bufferSize];
        byte[] outBuffer = new byte[bufferSize];
        int length = 0;

        var inflater = new Inflater();
        while (!inflater.IsFinished)
        {
            if (inflater.IsNeedingInput)
            {
                int bytesRead = input.Read(inBuffer, 0, inBuffer.Length);
                if (bytesRead > 0)
                {
                    inflater.SetInput(inBuffer, 0, bytesRead);
                }
            }
            int size = inflater.Inflate(outBuffer);
            length += size;
            if (size > 0)
            {
                output.Write(outBuffer, 0, size);
                output.Flush();
            }
        }
        return length;
    }
}
