﻿namespace AnywhereNET
{
    internal class Constants
    {
        #region Application <-> Orchestrator <-> Runner channels
        /// <summary>
        /// A channel for transporting data between an Application and an Orchestrator.
        /// </summary>
        public static readonly ushort ApplicationChannel = 10;

        /// <summary>
        /// A channel for transporting data between a Runner and an Orchestrator.
        /// </summary>
        public static readonly ushort RunnerChannel = 11;
        #endregion

        #region Application <-> Runner channels
        /// <summary>
        /// A channel for transporting expression execution requests and results between
        /// an Application and a Runner.
        /// </summary>
        public static readonly ushort ExpressionChannel = 20;

        /// <summary>
        /// A channel for transporting assemblies between
        /// an Application and a Runner.
        /// </summary>
        public static readonly ushort AssembliesChannel = 21;

        /// <summary>
        /// A channel for transporting files between
        /// an Application and a Runner.
        /// </summary>
        public static readonly ushort FilesChannel = 22;
        #endregion
    }
}