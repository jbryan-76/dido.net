namespace DidoNet
{
    /// <summary>
    /// Specifies the types of data frames communicated over a Channel.
    /// </summary>
    public enum FrameTypes
    {
        ChannelData,
        Disconnect,
        Debug,
        Heartbeat
    }
}