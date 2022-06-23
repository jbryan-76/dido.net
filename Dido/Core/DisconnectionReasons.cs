namespace DidoNet
{
    /// <summary>
    /// Possible values indicating why a connection closed.
    /// </summary>
    public enum DisconnectionReasons
    {
        /// <summary>
        /// Default value indicating an unknown or non-specific reason.
        /// For a closed connection this is not a legal value.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Indicates the connection was nominally terminated explicitly from the local side by calling Disconnect().
        /// </summary>
        LocalDisconnect,

        /// <summary>
        /// Indicates the connection was nominally terminated explicitly from the remote side.
        /// </summary>
        RemoteDisconnect,

        /// <summary>
        /// Indicates the connection was terminated locally due to a logical code execution exception.
        /// </summary>
        Error,

        /// <summary>
        /// Indicates the connection closed unexpectedly, usually due to a problem with the underlying network socket,
        /// for example a network error, the remote machine or application crashing, etc.
        /// </summary>
        Dropped,

        /// <summary>
        /// Indicates the connection was terminated locally because the remote endpoint did not send a heartbeat
        /// message within the expected time-frame.
        /// </summary>
        Unresponsive
    }
}
