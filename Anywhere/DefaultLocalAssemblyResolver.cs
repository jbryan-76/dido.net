using System.Reflection;

namespace AnywhereNET
{
    public class DefaultLocalAssemblyResolver
    {
        /// <summary>
        /// Resolve and return the provided assembly from the default application domain.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public static Task<Stream?> ResolveAssembly(string assemblyName)
        {
            // TODO: memoize this
            // TODO: recursive?
            // enumerate all the assembly files in the application directory
            var files = Directory.EnumerateFiles(AppContext.BaseDirectory, $"*.{OS.AssemblyExtension}");
            foreach (var file in files)
            {
                try
                {
                    // find and return the matching assembly
                    AssemblyName name = AssemblyName.GetAssemblyName(file);
                    if (name.FullName == assemblyName)
                    {
                        return Task.FromResult<Stream?>(File.Open(file, FileMode.Open, FileAccess.Read));
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
