using System.Reflection;

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

        /// <summary>
        /// Global test setup (only called once)
        /// </summary>
        public AnywhereTestFixture()
        {
            SharedTestDataPath = Path.Combine(Path.GetTempPath(), SharedTestDataFolder);
            Directory.CreateDirectory(SharedTestDataPath);

            Anywhere = new Anywhere
            {
                ExecutionMode = ExecutionModes.Local,
                ResolveLocalAssembly = UnitTestLocalAssemblyResolver
            };

            //Anywhere.ResolveAssembly = UnitTestAssemblyResolver;

            Environment = new Environment
            {
                 //ApplicationChannel
                 ResolveRemoteAssembly = UnitTestRemoteAssemblyResolver
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

        /// <summary>
        /// Resolve and return the provided assembly.
        /// This implementation is specifically to support unit test projects.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        Task<Stream?> UnitTestLocalAssemblyResolver(string assemblyName)
        {
            // TODO: memoize this
            var files = Directory.EnumerateFiles(TestLibAssembliesFolder, $"*.{AnywhereNET.OS.AssemblyExtension}");
            foreach (var file in files)
            {
                try
                {
                    AssemblyName name = AssemblyName.GetAssemblyName(file);
                    if (name.FullName == assemblyName)
                    {
                        return Task.FromResult<Stream?>(File.Open(file, FileMode.Open, FileAccess.Read));
                    }
                }
                catch (Exception)
                {
                    // exception will be thrown if the file is not a .NET assembly, in which case simply ignore
                    continue;
                }
            }

            // return null if no matching assembly could be found
            return Task.FromResult<Stream?>(null);
        }

        /// <summary>
        /// Resolve and return the provided assembly.
        /// This implementation is specifically to support unit test projects.
        /// </summary>
        /// <param name="env"></param>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        Task<Stream?> UnitTestRemoteAssemblyResolver(Environment env, string assemblyName)
        {
            return UnitTestLocalAssemblyResolver(assemblyName);
        }

    }
}