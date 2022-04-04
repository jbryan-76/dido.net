using System.Reflection;
using System.Runtime.Loader;

namespace AnywhereNET.Test.Common
{
    /// <summary>
    /// A common test fixture to support unit testing data shared between test projects.
    /// <para/>
    /// Note the entire premise of the Anywhere framework relies on communication between
    /// disjoint domains, which complicates running all unit tests within a single test project.
    /// This class facilitates data sharing between test projects.
    /// </summary>
    public class AnywhereTestFixture : IDisposable
    {
        /// <summary>
        /// The project name for the library containing sample assembly code used in 
        /// unit tests which must be resolved at runtime to support remote assembly code execution.
        /// </summary>
        public static readonly string TestLibName = "Anywhere.TestLib";

        public static readonly string MemberMethodFile = "member.method";
        public static readonly string MemberResultFile = "member.result";
        public static readonly string StaticMethodFile = "static.method";
        public static readonly string StaticResultFile = "static.result";
        public static readonly string DependencyMethodFile = "dependency.method";
        public static readonly string DependencyResultFile = "dependency.result";

        public string SharedTestDataPath;

        /// <summary>
        /// The folder containing the assemblies of the sibling TestLib project.
        /// </summary>
        readonly string TestLibAssembliesFolder = FindTestLibAssembliesFolder();

        /// <summary>
        /// The name of the folder stored in the system temp area where files can be shared
        /// between unit test projects.
        /// </summary>
        static readonly string SharedTestDataFolder = "Anywhere.Shared.Test.Data";

        public Anywhere Anywhere;

        public Environment Environment;

        private Dictionary<string, byte[]> AssemblyCache = new Dictionary<string, byte[]>();

        private List<string> AssemblyFiles = new List<string>();

        private DebugRemoteAssemblyResolver AssemblyResolver;

        /// <summary>
        /// Global test setup (only called once)
        /// </summary>
        public AnywhereTestFixture()
        {
            // create a directory to hold test data that should be shared between
            // the "Anywhere.Test" and "Anywhere.TestEnv" projects
            SharedTestDataPath = Path.Combine(Path.GetTempPath(), SharedTestDataFolder);
            Directory.CreateDirectory(SharedTestDataPath);

            AssemblyResolver = new DebugRemoteAssemblyResolver(TestLibAssembliesFolder);

            //Anywhere.ResolveAssembly = UnitTestAssemblyResolver;

            // set up a singleton environment instance
            Environment = new Environment
            {
                //ApplicationChannel
                //ResolveRemoteAssemblyAsync = UnitTestRemoteAssemblyResolver,
                ResolveRemoteAssemblyAsync = AssemblyResolver.ResolveAssembly,// new DebugRemoteAssemblyResolver(TestLibAssembliesFolder).ResolveAssembly,
                Context = new ExecutionContext
                {
                    ExecutionMode = ExecutionModes.Local,
                },
                AssemblyContext = new AssemblyLoadContext("test", true)
            };

            // set up a singleton anywhere instance
            Anywhere = new Anywhere
            {
                ExecutionMode = ExecutionModes.Local,
                ResolveLocalAssemblyAsync = (assemblyName) => AssemblyResolver.ResolveAssembly(Environment, assemblyName) //UnitTestLocalAssemblyResolver
            };
        }

        /// <summary>
        /// Global test teardown (only called once)
        /// </summary>
        public void Dispose()
        {
            //Directory.Delete(SharedTestDataPath, true);
        }

        /// <summary>
        /// Find the folder containing the assemblies of the sibling TestLib project,
        /// which are needed by AssemblyResolver to dynamically resolve assemblies at runtime.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        internal static string FindTestLibAssembliesFolder()
        {
            // get current executing path and its root
            var path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var root = Path.GetPathRoot(path);

            // assuming the testlib is a sibling, find the path to its assemblies
            string? testLibFolder = null;
            while (!string.IsNullOrEmpty(path) && path != root && string.IsNullOrEmpty(testLibFolder))
            {
                path = Directory.GetParent(path!)!.FullName;

                // see if the testlib folder is a child of the path
                testLibFolder = Directory.EnumerateDirectories(path!).FirstOrDefault(d => d.EndsWith(AnywhereTestFixture.TestLibName));

                // if so, search it for the assemblies
                if (testLibFolder != null)
                {
                    var file = Directory.GetFiles(testLibFolder, $"{AnywhereTestFixture.TestLibName}.{AnywhereNET.OS.AssemblyExtension}", SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (file != null)
                    {
                        // found it
                        testLibFolder = Directory.GetParent(file)?.FullName;
                    }
                }
            }

            if (testLibFolder == null)
            {
                throw new InvalidOperationException($"Could not find assemblies for project '{AnywhereTestFixture.TestLibName}'. Be sure that project is built and available.");
            }

            return testLibFolder;
        }

        ///// <summary>
        ///// Resolve and return the provided assembly.
        ///// This implementation is specifically to support unit test projects.
        ///// </summary>
        ///// <param name="assemblyName"></param>
        ///// <returns></returns>
        //Task<Stream?> UnitTestLocalAssemblyResolver(string assemblyName)
        //{
        //    lock (Anywhere)
        //    {
        //        // see if the assembly is already in the cache
        //        if (AssemblyCache.ContainsKey(assemblyName))
        //        {
        //            return Task.FromResult<Stream?>(new MemoryStream(AssemblyCache[assemblyName]));
        //        }

        //        // get the set of available assembly files in the TestLib project
        //        if (AssemblyFiles.Count == 0)
        //        {
        //            AssemblyFiles = Directory
        //                .EnumerateFiles(TestLibAssembliesFolder, $"*.{OS.AssemblyExtension}")
        //                .ToList();
        //        }

        //        // try to find the requested assembly by name
        //        foreach (var file in AssemblyFiles)
        //        {
        //            try
        //            {
        //                AssemblyName name = AssemblyName.GetAssemblyName(file);
        //                if (name.FullName == assemblyName)
        //                {
        //                    using (var fileStream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        //                    {
        //                        // cache it
        //                        var memStream = new MemoryStream();
        //                        fileStream.CopyTo(memStream);
        //                        AssemblyCache.Add(assemblyName, memStream.ToArray());

        //                        // then return it
        //                        memStream.Position = 0;
        //                        return Task.FromResult<Stream?>(memStream);
        //                    }
        //                }
        //            }
        //            catch (Exception)
        //            {
        //                // exception will be thrown if the file is not a .NET assembly, in which case simply ignore
        //                continue;
        //            }
        //        }

        //        // as a backstop, see if the desired assembly is actually already loaded
        //        var asm = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.FullName == assemblyName);
        //        if (asm != null && !string.IsNullOrEmpty(asm.Location))
        //        {
        //            using (var fileStream = File.Open(asm.Location, FileMode.Open, FileAccess.Read, FileShare.Read))
        //            {
        //                // cache it
        //                var memStream = new MemoryStream();
        //                fileStream.CopyTo(memStream);
        //                AssemblyCache.Add(assemblyName, memStream.ToArray());

        //                // then return it
        //                memStream.Position = 0;
        //                return Task.FromResult<Stream?>(memStream);
        //            }
        //        }

        //        // return null if no matching assembly could be found
        //        return Task.FromResult<Stream?>(null);
        //    }
        //}

        ///// <summary>
        ///// Resolve and return the provided assembly.
        ///// This implementation is specifically to support unit test projects.
        ///// </summary>
        ///// <param name="env"></param>
        ///// <param name="assemblyName"></param>
        ///// <returns></returns>
        //Task<Stream?> UnitTestRemoteAssemblyResolver(Environment env, string assemblyName)
        //{
        //    // TODO: use a custom stream to simulate the message passing to negotiate file loading?
        //    return UnitTestLocalAssemblyResolver(assemblyName);
        //}

    }
}