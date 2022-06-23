using System.Reflection;

namespace DidoNet
{
    internal class Constants
    {
        #region Application <-> Mediator <-> Runner channels
        /// <summary>
        /// The channel number for the channel transporting data between an Application and a Mediator.
        /// </summary>
        internal static readonly ushort MediatorApp_ChannelId = 10;

        /// <summary>
        /// The channel number for the channel transporting data between a Runner and a Mediator.
        /// </summary>
        internal static readonly ushort MediatorRunner_ChannelId = 11;
        #endregion

        #region Application <-> Runner channels
        /// <summary>
        /// The channel number for the channel transporting control messages between
        /// an Application and a Runner.
        /// </summary>
        internal static readonly ushort AppRunner_ControlChannelId = 20;

        /// <summary>
        /// The channel number for the channel transporting task requests and results between
        /// an Application and a Runner.
        /// </summary>
        internal static readonly ushort AppRunner_TaskChannelId = 21;

        /// <summary>
        /// The channel number for the channel transporting assemblies between
        /// an Application and a Runner.
        /// </summary>
        internal static readonly ushort AppRunner_AssemblyChannelId = 22;

        /// <summary>
        /// The channel number for the channel transporting file requests from
        /// a Runner to an Application.
        /// </summary>
        internal static readonly ushort AppRunner_FileChannelId = 23;

        /// <summary>
        /// The first channel number for channels transporting files between
        /// an Application and a Runner.
        /// </summary>
        internal static readonly ushort AppRunner_FileChannelStart = 1000;

        internal static readonly ushort AppRunner_MaxFileChannels = 1024;

        /// <summary>
        /// The first channel number for application-defined custom channels.
        /// </summary>
        internal static readonly ushort AppRunner_CustomChannelStart = 2000;

        internal static readonly ushort AppRunner_MaxCustomChannels = 1024;
        #endregion

        /// <summary>
        /// Binding flags to filter all members of a class: instance, public, non-public, and static.
        /// </summary>
        internal static BindingFlags AllMembers =
            BindingFlags.Instance | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.Static;
    }
}