namespace Anywhere.Test.Common
{
    public class AnywhereTestFixture : IDisposable
    {
        public static readonly string TestLibName = "Anywhere.TestLib";
        public static readonly string AssemblyExtension = "dll";
 
        public static readonly string MemberMethodFile = "member.method";
        public static readonly string StaticMethodFile = "static.method";

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