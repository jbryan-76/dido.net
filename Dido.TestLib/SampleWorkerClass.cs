using DidoNet.TestLibDependency;

namespace DidoNet.TestLib
{
    /// <summary>
    /// A test class containing sample methods for unit testing basic
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

        /// <summary>
        /// A static method that loops forever until the given cancellation token
        /// is canceled.
        /// </summary>
        /// <param name="cancel"></param>
        /// <returns></returns>
        public static bool InfiniteLoopWithCancellation(CancellationToken cancel)
        {
            while (!cancel.IsCancellationRequested)
            {
                Thread.Sleep(1);
            }
            return true;
        }
    }
}