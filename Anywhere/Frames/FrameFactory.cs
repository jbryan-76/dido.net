namespace AnywhereNET
{
    public static class FrameFactory
    {
        /// <summary>
        /// Convert the given abstract frame into a concrete typed frame.
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static Frame Decode(Frame frame)
        {
            switch (frame.FrameType)
            {
                case FrameTypes.Debug: return new DebugFrame(frame);
                case FrameTypes.Disconnect: return new DisconnectFrame(frame);
                case FrameTypes.ChannelData: return new ChannelDataFrame(frame);
                case FrameTypes.Heartbeat: return new HeartbeatFrame(frame);
                default:
                    throw new InvalidOperationException($"Unknown frame type: {frame.Type}");
            }
        }
    }
}