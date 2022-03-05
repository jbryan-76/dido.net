namespace Anywhere.Test.Common
{
    /// <summary>
    /// A common test fixture to support unit testing shared data.
    /// </summary>
    public class AnywhereTestFixture : IDisposable
    {
        public static readonly string TestLibName = "Anywhere.TestLib";
        public static readonly string AssemblyExtension = "dll";
 
        public static readonly string MemberMethodFile = "member.method";
        public static readonly string MemberResultFile = "member.result";
        public static readonly string StaticMethodFile = "static.method";
        public static readonly string StaticResultFile = "static.result";

        static readonly string SharedTestDataFolder = "Anywhere.Shared.Test.Data";
        public string SharedTestDataPath;

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