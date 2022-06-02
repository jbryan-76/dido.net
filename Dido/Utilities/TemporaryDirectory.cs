namespace Dido.Utilities
{
    /// <summary>
    /// Manages a unique, temporary directory as a disposable resource.
    /// </summary>
    public class TemporaryDirectory : IDisposable
    {
        /// <summary>
        /// Create a new temporary directory, optionally with a specific name.
        /// Upon disposal, the directory will be deleted, if it still exists.
        /// </summary>
        /// <param name="specificPath">The path and name of the temporary directory. If not provided, an OS-generated
        /// path will be created in the configured system temp area.</param>
        public TemporaryDirectory(string? specificPath = null)
        {
            if (string.IsNullOrWhiteSpace(specificPath))
            {
                Path = System.IO.Path.GetTempFileName();
                if (File.Exists(Path))
                {
                    File.Delete(Path);
                }
            }
            else
            {
                Path = specificPath;
            }
            Directory.CreateDirectory(Path);
        }

        /// <summary>
        /// The path of the temporary directory.
        /// </summary>
        public string Path { get; private set; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
            GC.SuppressFinalize(this);
        }
    }
}
