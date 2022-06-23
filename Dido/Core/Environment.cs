using Dido.Utilities;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        /// <param name="env"></param>
        /// <param name="assemblyName"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public delegate Task<Stream?> RemoteAssemblyResolver(Environment env, string assemblyName, out string? error);

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

        private HashSet<string> RequestedSystemAssemblies { get; set; } = new HashSet<string>();

        /// <summary>
        /// The local file-system path used to cache application assemblies used by the runner.
        /// </summary>
        public string? AssemblyCachePath { get; set; }

        /// <summary>
        /// The optional encryption key to use when caching assemblies used by the runner.
        /// </summary>
        public string? CachedAssemblyEncryptionKey { get; set; }

        private static ILogger Logger = LogManager.GetCurrentClassLogger();

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
        /// <para/>1) Checks the in-memory cache of previously loaded assemblies.
        /// <para/>2) Checks all loaded assemblies (in both the Default and runner Environment AssemblyLoadContexts).
        /// <para/>3) Checks any configured AssemblyCachePath.
        /// <para/>4) Fetches with ResolveRemoteAssemblyAsync.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        internal Assembly? ResolveAssembly(string assemblyName)
        {
            Logger.Trace($"ResolveAssembly: finding {assemblyName}:");

            var asmName = new AssemblyName(assemblyName);
            Assembly? asm;

            // first try the in-memory cache of loaded assemblies
            Logger.Trace($"ResolveAssembly: Checking memory cache...");
            if (LoadedAssemblies.TryGetValue(assemblyName, out asm) && asm != null)
            {
                Logger.Trace($"ResolveAssembly:  FOUND! {asm}");
                return asm;
            }

            // special handling for CoreLib which can't be "overloaded" and which should be backwards compatible
            // with any older application code
            if (asmName.Name == "System.Private.CoreLib")
            //if (asmName.Name!.StartsWith("System.Private"))
            {
                //if (!RequestedSystemAssemblies.Contains(assemblyName))
                //{
                //    // if this is the first time the assembly was requested, 
                //    // allow it to go through normal 
                //}
                Logger.Trace($"finding system library: {asmName}");
                asm = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(asm => asm.GetName().Name == asmName.Name);
                if (asm != null)
                {
                    Logger.Trace($"ResolveAssembly:  FOUND! {asm}");
                    LoadedAssemblies.TryAdd(assemblyName, asm);
                    return asm;
                }
            }

            // next check if the assembly is already loaded into the default context
            // (this will be common for standard .NET assemblies, e.g. System)
            Logger.Trace($"ResolveAssembly: Checking default context...");
            asm = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(asm => asm.FullName == assemblyName);
            if (asm != null)
            {
                Logger.Trace($"ResolveAssembly:  FOUND! {asm}");
                LoadedAssemblies.TryAdd(assemblyName, asm);
                return asm;
            }

            var asmCachedFilename = $"{asmName.Name}.{asmName.Version}.{OSConfiguration.AssemblyExtension}";
            var asmCachedPath = !string.IsNullOrEmpty(AssemblyCachePath)
                ? Path.Combine(AssemblyCachePath, asmCachedFilename)
                : null;

            // next try the disk cache
            Logger.Trace($"ResolveAssembly: Checking disk cache...");
            if (File.Exists(asmCachedPath))
            {
                // TODO: should a stronger confirmation be used that the assembly matches other than
                // TODO: the explicitly generated cache filename? if the file is encrypted it will 
                // TODO: first need to be decrypted to even introspect it, which adds overhead
                //var info = FileVersionInfo.GetVersionInfo(asmCachedPath);
                //if (info.FileVersion == asmName.Version?.ToString())
                try
                {
                    using (var fs = File.Open(asmCachedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        // decrypt the file if necessary
                        if (!string.IsNullOrEmpty(CachedAssemblyEncryptionKey))
                        {
                            using (var membuf = new MemoryStream())
                            {
                                AES.Decrypt(fs, membuf, CachedAssemblyEncryptionKey);
                                membuf.Seek(0, SeekOrigin.Begin);
                                asm = AssemblyContext.LoadFromStream(membuf);
                            }
                        }
                        else
                        {
                            asm = AssemblyContext.LoadFromStream(fs);
                        }
                        LoadedAssemblies.TryAdd(assemblyName, asm);
                        Logger.Trace($"ResolveAssembly:  FOUND! {asm}");
                        return asm;
                    }
                }
                catch (Exception)
                {
                    // if the cached assembly could not be loaded, remove it and fall through to use
                    // the remote resolver below
                    File.Delete(asmCachedPath);
                    LoadedAssemblies.Remove(assemblyName, out _);
                }
            }

            // finally try the remote resolver
            if (ResolveRemoteAssemblyAsync == null)
            {
                throw new InvalidOperationException($"'{nameof(Environment.ResolveRemoteAssemblyAsync)}' is not defined on the current Environment parameter.");
            }
            Logger.Trace($"ResolveAssembly: Checking remote...");
            var stream = ResolveRemoteAssemblyAsync(this, assemblyName, out string? error).Result;
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException($"Fatal error while resolving remote assembly: {error}.");
            }
            if (stream == null)
            {
                return null;
            }

            // if the underlying stream is not seekable, copy it to a memory stream
            // so it can be read more than once below
            if (!stream.CanSeek)
            {
                var tmp = new MemoryStream();
                stream.CopyTo(tmp);
                stream.Dispose();
                tmp.Seek(0, SeekOrigin.Begin);
                stream = tmp;
            }

            // cache the assembly to disk if necessary
            if (!string.IsNullOrEmpty(asmCachedPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(asmCachedPath)!);
                using (var asmCachedFile = File.Create(asmCachedPath))
                {
                    // encrypt the file if necessary
                    if (!string.IsNullOrEmpty(CachedAssemblyEncryptionKey))
                    {
                        AES.Encrypt(stream, asmCachedFile, CachedAssemblyEncryptionKey);
                    }
                    else
                    {
                        stream.CopyTo(asmCachedFile);
                    }
                }
                stream.Seek(0, SeekOrigin.Begin);
            }

            // load the assembly
            Logger.Trace($"ResolveAssembly:  got stream {stream.Length} bytes. loading...");
            try
            {
                asm = AssemblyContext.LoadFromStream(stream);
                Logger.Trace($"ResolveAssembly:  FOUND! {asm}");
                LoadedAssemblies.TryAdd(assemblyName, asm);
            }
            catch (Exception ex)
            {
                Logger.Trace($"ResolveAssembly:  could not load assembly: possible runtime conflict");

                //// next handle system libraries that could conflict with already loaded libraries
                ////if (asmName.Name == "System.Private.CoreLib")
                //if (asmName.Name!.StartsWith("System.Private"))
                //{
                //    if (!RequestedSystemAssemblies.Contains(assemblyName))
                //    {
                //        // if this is the first time the assembly was requested, 
                //        // allow it to go through normal 
                //    }
                //    Logger.Trace($"finding system library: {asmName}");
                //    //asm = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(asm => asm.GetName().Name == asmName.Name);
                //    //if (asm != null)
                //    //{
                //    //    Logger.Trace($"ResolveAssembly:  FOUND! {asm}");
                //    //    LoadedAssemblies.TryAdd(assemblyName, asm);
                //    //    return asm;
                //    //}
                //}

                Logger.Error(ex);
                return null;
            }
            finally
            {
                stream.Dispose();
            }

            return asm;
        }

        private Assembly? ResolveMissingAssembly(AssemblyLoadContext context, AssemblyName asmName)
        {
            return ResolveAssembly(asmName.FullName);
        }
    }
}