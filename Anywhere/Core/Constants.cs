using System.Reflection;

namespace AnywhereNET
{
    internal class Constants
    {
        #region Application <-> Orchestrator <-> Runner channels
        /// <summary>
        /// A channel for transporting data between an Application and an Orchestrator.
        /// </summary>
        internal static readonly ushort ApplicationChannelNumber = 10;

        /// <summary>
        /// A channel for transporting data between a Runner and an Orchestrator.
        /// </summary>
        internal static readonly ushort RunnerChannelNumber = 11;
        #endregion

        #region Application <-> Runner channels
        /// <summary>
        /// A channel for transporting task requests and results between
        /// an Application and a Runner.
        /// </summary>
        internal static readonly ushort TaskChannelNumber = 20;

        /// <summary>
        /// A channel for transporting assemblies between
        /// an Application and a Runner.
        /// </summary>
        internal static readonly ushort AssemblyChannelNumber = 21;

        /// <summary>
        /// A channel for transporting files between
        /// an Application and a Runner.
        /// </summary>
        internal static readonly ushort FileChannelNumber = 22;
        #endregion

        /// <summary>
        /// Binding flags to filter all members of a class: instance, public, non-public, and static.
        /// </summary>
        internal static BindingFlags AllMembers =
            BindingFlags.Instance | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.Static;
    }
}