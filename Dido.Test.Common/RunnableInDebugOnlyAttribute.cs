using System.Diagnostics;
using Xunit;

namespace DidoNet.Test.Common
{
    /// <summary>
    /// Only runs a test in DEBUG.
    /// https://stackoverflow.com/questions/37334245/can-you-mark-xunit-tests-as-explicit
    /// </summary>
    internal class RunnableInDebugOnlyAttribute : FactAttribute
    {
        public RunnableInDebugOnlyAttribute()
        {
            if (!Debugger.IsAttached)
            {
                Skip = "Only runs in interactive/debug mode.";
            }
        }
    }
}