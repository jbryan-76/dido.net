namespace Anywhere.Test.Common
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

        /// <summary>
        /// The extension used for .NET assemblies.
        /// </summary>
        public static readonly string AssemblyExtension = "dll";
 
        public static readonly string MemberMethodFile = "member.method";
        public static readonly string MemberResultFile = "member.result";
        public static readonly string StaticMethodFile = "static.method";
        public static readonly string StaticResultFile = "static.result";
        public static readonly string DependencyMethodFile = "dependency.method";
        public static readonly string DependencyResultFile = "dependency.result";

        public string SharedTestDataPath;

        /// <summary>
        /// The name of the folder stored in the system temp area where files can be shared
        /// between unit test projects.
        /// </summary>
        static readonly string SharedTestDataFolder = "Anywhere.Shared.Test.Data";

        /// <summary>
        /// Global test setup (only called once)
        /// </summary>
        public AnywhereTestFixture()
        {
            SharedTestDataPath = Path.Combine(Path.GetTempPath(), SharedTestDataFolder);
            Directory.CreateDirectory(SharedTestDataPath);
        }

        /// <summary>
        /// Global test teardown (only called once)
        /// </summary>
        public void Dispose()
        {
            //Directory.Delete(SharedTestDataPath, true);
        }
    }
}