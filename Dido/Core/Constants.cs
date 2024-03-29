﻿using System.Reflection;

namespace DidoNet
{
    /// <summary>
    /// Compile-time constant values.
    /// </summary>
    internal class Constants
    {
        /// <summary>
        /// The default port to use for incoming connections.
        /// </summary>
        internal static readonly int DefaultPort = 4940;

        #region Application <-> Mediator <-> Runner channels
        /// <summary>
        /// The name for the channel transporting data between an Application and a Mediator.
        /// </summary>
        internal static readonly string MediatorApp_ChannelId = "app-med";

        /// <summary>
        /// The name for the channel transporting runner data between a Runner and a Mediator.
        /// </summary>
        internal static readonly string MediatorRunner_ChannelId = "run-med";

        /// <summary>
        /// The name for the channel transporting job data between a Runner and a Mediator.
        /// </summary>
        internal static readonly string MediatorRunnerJobs_ChannelId = "run-med-job";
        #endregion

        #region Application <-> Runner channels
        /// <summary>
        /// The name for the channel transporting control messages between
        /// an Application and a Runner.
        /// </summary>
        internal static readonly string AppRunner_ControlChannelId = "control";

        /// <summary>
        /// The name for the channel transporting task requests and results between
        /// an Application and a Runner.
        /// </summary>
        internal static readonly string AppRunner_TaskChannelId = "task";

        /// <summary>
        /// The name for the channel transporting assemblies between
        /// an Application and a Runner.
        /// </summary>
        internal static readonly string AppRunner_AssemblyChannelId = "assembly";

        /// <summary>
        /// The name for the channel transporting file requests from
        /// a Runner to an Application.
        /// </summary>
        internal static readonly string AppRunner_FileChannelId = "file";
        #endregion

        /// <summary>
        /// Binding flags to filter all members of a class: instance, public, non-public, and static.
        /// </summary>
        internal static BindingFlags AllMembers =
            BindingFlags.Instance | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.Static;
    }
}