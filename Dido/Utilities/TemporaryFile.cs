namespace Dido.Utilities
{
    /// <summary>
    /// Manages a unique, temporary filename and associated file as a disposable resource.
    /// </summary>
    public class TemporaryFile : IDisposable
    {
        /// <summary>
        /// Create a new temporary file, optionally with a specific filename and extension.
        /// Upon disposal, the file will be deleted, if it still exists.
        /// </summary>
        /// <param name="specificPath">The path and filename of the temporary file. If not provided, an OS-generated
        /// path will be created in the configured system temp area.</param>
        /// <param name="specificExtension">The extension for the filename. If not provided, no specific extension is 
        /// assigned.</param>
        public TemporaryFile(string? specificPath = null, string? specificExtension = null)
        {
            Filename = string.IsNullOrWhiteSpace(specificPath)
                ? Path.GetTempFileName()
                : specificPath;

            // delete any existing file so the caller can create/open as needed
            if (File.Exists(Filename))
            {
                File.Delete(Filename);
            }

            if (!string.IsNullOrEmpty(specificExtension) &&
                Path.GetExtension(Filename).TrimStart('.').ToLower() != specificExtension.TrimStart('.').ToLower())
            {
                Filename = Filename.TrimEnd('.') + "." + specificExtension;
            }
        }

        /// <summary>
        /// The name of the temporary file.
        /// </summary>
        public string Filename { get; private set; }

        public void Dispose()
        {
            if (File.Exists(Filename))
            {
                File.Delete(Filename);
            }
            GC.SuppressFinalize(this);
        }
    }
}
