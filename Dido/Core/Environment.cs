using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace DidoNet
{
    /// <summary>
    /// Contains the ambient runtime state for a task executing in a runner service.
    /// </summary>
    public class Environment : IDisposable
    {
        // TODO: replace RemoteAssemblyResolver with an interface: Resolve() and Close()

        // TODO: should this yield a stream or byte[]?
        /// <summary>
        /// Signature for a method that resolves a provided assembly by name,
        /// returning a stream containing the assembly byte-code.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public delegate Task<Stream?> RemoteAssemblyResolver(Environment env, string assemblyName);

        /// <summary>
        /// A delegate method for resolving remote runtime assemblies used by the host application.
        /// </summary>
        public RemoteAssemblyResolver? ResolveRemoteAssemblyAsync { get; set; }

        /// <summary>
        /// A runtime AssemblyLoadContext which serves as an isolated container
        /// for all assemblies needed to execute an expression.
        /// </summary>
        public AssemblyLoadContext AssemblyContext { get; private set; }

        ///// <summary>
        ///// A bi-directional communications channel to the application.
        ///// </summary>
        //public Stream? ApplicationChannel { get; set; }

        /// <summary>
        /// The context available to the executing expression to access configuration and utilities.
        /// </summary>
        public ExecutionContext? ExecutionContext { get; set; }

        /// <summary>
        /// The cache of loaded assemblies available to the runner environment, which may contain a mix 
        /// of assemblies loaded in the Default AssemblyLoadContext, as well as those in the environment's
        /// specific AssemblyLoadContext.
        /// </summary>
        private ConcurrentDictionary<string, Assembly> LoadedAssemblies { get; set; } = new ConcurrentDictionary<string, Assembly>();

        /// <summary>
        /// The local file-system path used to cache application assemblies used by the runner.
        /// </summary>
        public string? AssemblyCachePath { get; set; }

        /// <summary>
        /// Initializes a new instance of the Environment class.
        /// </summary>
        public Environment()
        {
            AssemblyContext = new AssemblyLoadContext(Guid.NewGuid().ToString(), true);
            AssemblyContext.Resolving += ResolveMissingAssembly;
        }

        public void Dispose()
        {
            AssemblyContext.Unload();
        }

        /// <summary>
        /// Indicates whether the given assembly is already loaded into an AssemblyLoadContext.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        internal bool IsAssemblyLoaded(string assemblyName)
        {
            return LoadedAssemblies.ContainsKey(assemblyName);
        }

        /// <summary>
        /// Attempts to get the given assembly which is already loaded into an AssemblyLoadContext.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="asm"></param>
        /// <returns><see langword="true"/> if the assembly was found, else <see langword="false"/>.</returns>
        internal bool TryGetLoadedAssembly(string assemblyName, out Assembly? asm)
        {
            return LoadedAssemblies.TryGetValue(assemblyName, out asm) && asm != null;
        }

        /// <summary>
        /// Heuristically resolves and returns the given assembly as follows:
        /// <para/>1) Checks all loaded assemblies (in both the Default and runner Environment AssemblyLoadContexts).
        /// <para/>2) Checks any configured AssemblyCachePath.
        /// <para/>3) Fetches with ResolveRemoteAssemblyAsync.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        internal Assembly? ResolveAssembly(string assemblyName)
        {
            // check if the assembly is already loaded into the default context
            // (this will be common for standard .NET assemblies, e.g. System)
            var asm = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(asm => asm.FullName == assemblyName);
            if (asm != null)
            {
                LoadedAssemblies.TryAdd(assemblyName, asm);
                return asm;
            }

            // next try the in-memory cache of loaded assemblies
            if (LoadedAssemblies.TryGetValue(assemblyName, out asm) && asm != null)
            {
                return null;
            }

            var asmName = new AssemblyName(assemblyName);
            var asmCachedFilename = $"{asmName.Name}.{asmName.Version}.{OSConfiguration.AssemblyExtension}";
            var asmCachedPath = !string.IsNullOrEmpty(AssemblyCachePath)
                ? Path.Combine(AssemblyCachePath, asmCachedFilename)
                : null;

            // next try the disk cache
            if (File.Exists(asmCachedPath))
            {
                // TODO: stronger confirmation the assembly matches?
                var info = FileVersionInfo.GetVersionInfo(asmCachedPath);
                if (info.FileVersion == asmName.Version?.ToString())
                {
                    using (var fs = File.Open(asmCachedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        asm = AssemblyContext.LoadFromStream(fs);
                        LoadedAssemblies.TryAdd(assemblyName, asm);
                        return asm;
                    }
                }
            }

            // finally try the remote resolver
            if (ResolveRemoteAssemblyAsync == null)
            {
                throw new InvalidOperationException($"'{nameof(Environment.ResolveRemoteAssemblyAsync)}' is not defined on the current Environment parameter.");
            }
            var stream = ResolveRemoteAssemblyAsync(this, assemblyName).Result;
            if (stream == null)
            {
                return null;
            }

            // cache the assembly to disk
            if (!string.IsNullOrEmpty(asmCachedPath))
            {
                // after copying the assembly stream to disk, dispose it and 
                // use the file stream instead (since the assembly stream may
                // not be seekable)
                var asmCachedFile = File.Create(asmCachedPath);
                stream.CopyTo(asmCachedFile);
                stream.Dispose();
                asmCachedFile.Seek(0, SeekOrigin.Begin);
                stream = asmCachedFile;
            }

            // load the assembly
            asm = AssemblyContext.LoadFromStream(stream);
            LoadedAssemblies.TryAdd(assemblyName, asm);
            stream.Dispose();

            return asm;
        }

        private Assembly? ResolveMissingAssembly(AssemblyLoadContext context, AssemblyName asmName)
        {
            return ResolveAssembly(asmName.FullName);
        }
    }
}