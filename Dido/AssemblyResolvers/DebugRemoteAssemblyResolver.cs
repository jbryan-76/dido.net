using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace DidoNet
{
    internal class DebugRemoteAssemblyResolver
    {
        private object LockObject = new object();

        private Dictionary<string, byte[]> AssemblyCache = new Dictionary<string, byte[]>();

        private List<string> AssemblyFiles = new List<string>();

        public string AssemblySearchPath { get; private set; }

        /// <summary>
        /// Create a new assembly resolver suitable for emulating finding assemblies
        /// requested by a remote environment, as needed for debugging or unit testing.
        /// </summary>
        /// <param name="assemblySearchPath">The folder containing all assemblies that can be resolved.</param>
        public DebugRemoteAssemblyResolver(string assemblySearchPath)
        {
            AssemblySearchPath = assemblySearchPath;
        }

        public Task<Stream?> ResolveAssembly(Environment env, string assemblyName, out string? error)
        {
            error = null;
            lock (LockObject)
            {
                // see if the assembly is already in the cache
                if (AssemblyCache.ContainsKey(assemblyName))
                {
                    return Task.FromResult<Stream?>(new MemoryStream(AssemblyCache[assemblyName]));
                }

                // enumerate all the assembly files in the configured directory
                if (AssemblyFiles.Count == 0)
                {
                    AssemblyFiles = Directory
                        .EnumerateFiles(AssemblySearchPath, $"*.{OSConfiguration.AssemblyExtension}")
                        //.EnumerateFiles(AppContext.BaseDirectory, $"*.{OS.AssemblyExtension}")
                        .ToList();
                }

                // try to find the requested assembly by name
                foreach (var file in AssemblyFiles)
                {
                    try
                    {
                        AssemblyName name = AssemblyName.GetAssemblyName(file);
                        if (name.FullName == assemblyName)
                        {
                            using (var fileStream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                // cache it
                                var memStream = new MemoryStream();
                                fileStream.CopyTo(memStream);
                                AssemblyCache.Add(assemblyName, memStream.ToArray());

                                // then return it
                                memStream.Position = 0;
                                return Task.FromResult<Stream?>(memStream);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // exception will be thrown if the file is not a .NET assembly, in which case simply ignore
                        continue;
                    }
                }

                // as a backstop, see if the desired assembly is actually already loaded
                var asm = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.FullName == assemblyName);
                if (asm != null && !string.IsNullOrEmpty(asm.Location))
                {
                    using (var fileStream = File.Open(asm.Location, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        // cache it
                        var memStream = new MemoryStream();
                        fileStream.CopyTo(memStream);
                        AssemblyCache.Add(assemblyName, memStream.ToArray());

                        // then return it
                        memStream.Position = 0;
                        return Task.FromResult<Stream?>(memStream);
                    }
                }

                // return null if no matching assembly could be found
                return Task.FromResult<Stream?>(null);
            }
        }
    }
}