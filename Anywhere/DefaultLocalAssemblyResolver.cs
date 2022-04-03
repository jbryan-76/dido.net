using System.Reflection;

namespace AnywhereNET
{
    public class DefaultLocalAssemblyResolver
    {
        private List<string> AssemblyFiles = new List<string>();

        /// <summary>
        /// Resolve and return the provided assembly from the default application domain.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public Task<Stream?> ResolveAssembly(string assemblyName)
        {
            // TODO: recursive?
            // enumerate all the assembly files in the application directory
            if (AssemblyFiles.Count == 0)
            {
                AssemblyFiles = Directory
                    .EnumerateFiles(AppContext.BaseDirectory, $"*.{OS.AssemblyExtension}")
                    .ToList();
            }

            foreach (var file in AssemblyFiles)
            {
                try
                {
                    // find and return the matching assembly
                    AssemblyName name = AssemblyName.GetAssemblyName(file);
                    if (name.FullName == assemblyName)
                    {
                        // TODO: cache this?
                        return Task.FromResult<Stream?>(File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read));
                    }
                }
                catch (Exception)
                {
                    // exception will be thrown from GetAssemblyName if the file is not a .NET assembly,
                    // in which case simply ignore
                    continue;
                }
            }

            // return null if no matching assembly could be found
            return Task.FromResult<Stream?>(null);
        }
    }
}
