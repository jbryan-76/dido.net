namespace SslTestCommon
{
    public static class FrameFactory
    {
        public static Frame Decode(Frame frame)
        {
            switch (frame.FrameType)
            {
                case FrameTypes.Debug: return new DebugFrame(frame);
                case FrameTypes.Disconnect: return new DisconnectFrame(frame);
                default:
                    throw new InvalidOperationException($"Unknown frame type: {frame.Type}");
            }
        }
    }
}