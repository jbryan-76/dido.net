using System.Runtime.Loader;

namespace DidoNet.Test.Common
{
    /// <summary>
    /// A common test fixture to support unit testing data shared between test projects.
    /// <para/>
    /// Note the entire premise of the Dido framework relies on communication between
    /// disjoint domains, which complicates running all unit tests within a single test project.
    /// This class facilitates data sharing between test projects.
    /// </summary>
    public class TestFixture : IDisposable
    {
        /// <summary>
        /// The project name for the library containing sample assembly code used in 
        /// unit tests which must be resolved at runtime to support remote assembly code execution.
        /// </summary>
        public static readonly string TestLibName = "Dido.TestLib";

        public static readonly string MemberMethodFile = "member.method";
        public static readonly string MemberResultFile = "member.result";
        public static readonly string StaticMethodFile = "static.method";
        public static readonly string StaticResultFile = "static.result";
        public static readonly string DependencyMethodFile = "dependency.method";
        public static readonly string DependencyResultFile = "dependency.result";

        public string SharedTestDataPath;

        public Configuration Configuration;

        public Environment Environment;

        internal DebugRemoteAssemblyResolver AssemblyResolver { get; private set; }

        /// <summary>
        /// The folder containing the assemblies of the sibling TestLib project.
        /// </summary>
        readonly string TestLibAssembliesFolder = FindTestLibAssembliesFolder();

        /// <summary>
        /// The name of the folder stored in the system temp area where files can be shared
        /// between unit test projects.
        /// </summary>
        static readonly string SharedTestDataFolder = "Dido.Shared.Test.Data";

        /// <summary>
        /// Global test setup (only called once)
        /// </summary>
        public TestFixture()
        {
            // create a directory to hold test data that should be shared between
            // the "Dido.Test" and "Dido.Test.Servers" projects
            SharedTestDataPath = Path.Combine(Path.GetTempPath(), SharedTestDataFolder);
            Directory.CreateDirectory(SharedTestDataPath);

            AssemblyResolver = new DebugRemoteAssemblyResolver(TestLibAssembliesFolder);

            // set up a common environment instance
            Environment = new Environment
            {
                ResolveRemoteAssemblyAsync = AssemblyResolver.ResolveAssembly,
                ExecutionContext = new ExecutionContext
                {
                    ExecutionMode = ExecutionModes.Local,
                },
            };

            // set up a common configuration instance
            Configuration = new Configuration
            {
                MaxTries = 1,
                ExecutionMode = ExecutionModes.Local,
                ResolveLocalAssemblyAsync = (assemblyName) => AssemblyResolver.ResolveAssembly(Environment, assemblyName, out _)
            };
        }

        /// <summary>
        /// Global test teardown (only called once)
        /// </summary>
        public void Dispose()
        {
            //Directory.Delete(SharedTestDataPath, true);
            Environment.Dispose();
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
                testLibFolder = Directory.EnumerateDirectories(path!).FirstOrDefault(d => d.EndsWith(TestFixture.TestLibName));

                // if so, search it for the assemblies
                if (testLibFolder != null)
                {
                    var file = Directory.GetFiles(testLibFolder, $"{TestFixture.TestLibName}.{DidoNet.OSConfiguration.AssemblyExtension}", SearchOption.AllDirectories)
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
                throw new InvalidOperationException($"Could not find assemblies for project '{TestFixture.TestLibName}'. Be sure that project is built and available.");
            }

            return testLibFolder;
        }
    }
}