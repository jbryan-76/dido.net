using AnywhereNET.TestLibDependency;

namespace AnywhereNET.TestLib
{
    /// <summary>
    /// A test class containing sample methods for unit testing basic Anywhere
    /// functionality for transporting assemblies and invoking methods.
    /// </summary>
    public class SampleWorkerClass
    {
        /// <summary>
        /// A member method that simply returns its argument.
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        public int SimpleMemberMethod(int arg)
        {
            return arg;
        }

        /// <summary>
        /// A static method that simply returns its argument.
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        public static int SimpleStaticMethod(int arg)
        {
            return arg;
        }

        /// <summary>
        /// A member method that utilizes a dependent assembly and simply returns
        /// a string representation of its argument.
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        public string MemberMethodWithDependency(SampleDependencyClass arg)
        {
            return arg.GenerateCombinedString();
        }
    }
}