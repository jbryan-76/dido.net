namespace AnywhereNET
{
    internal class ThreadHelpers
    {
        /// <summary>
        /// Yields the remaining cpu time slice of the current thread and 
        /// forces a context switch, optionally sleeping up to the provided
        /// number of milliseconds. This prevents 100% cpu utilization
        /// in a wait-loop and avoids subtle thread starvation and race
        /// condition edge cases.
        /// </summary>
        /// <param name="milliseconds"></param>
        public static void Yield(int milliseconds = 1)
        {
            // never use Sleep(0), always use at least Sleep(1)
            // http://joeduffyblog.com/2006/08/22/priorityinduced-starvation-why-sleep1-is-better-than-sleep0-and-the-windows-balance-set-manager/
            Thread.Sleep(Math.Max(milliseconds, 1));
        }
    }
}