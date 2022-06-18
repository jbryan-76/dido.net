using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace DidoNet
{
    /// <summary>
    /// A default implementation to resolve assemblies using the application's 
    /// local base directory.
    /// </summary>
    public class DefaultLocalAssemblyResolver
    {
        /// <summary>
        /// Resolve and return the provided assembly from the default application domain.
        /// In practice this resolves the assembly from the application's base directory.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public Task<Stream?> ResolveAssembly(string assemblyName)
        {
            // check if the assembly is already loaded into the default context
            // (this will be common for standard .NET assemblies, e.g. System)
            var asm = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(asm => asm.FullName == assemblyName);
            if (asm != null && !string.IsNullOrEmpty(asm.Location))
            {
                Console.WriteLine($"Found {asm.Location}");
                return Task.FromResult<Stream?>(File.Open(asm.Location, FileMode.Open, FileAccess.Read, FileShare.Read));
            }

            // next check if the assembly is in the application base directory
            // TODO: recursive?
            var files = Directory
                .EnumerateFiles(AppContext.BaseDirectory, $"*.{OSConfiguration.AssemblyExtension}")
                .ToList();
            foreach (var file in files.ToArray())
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
