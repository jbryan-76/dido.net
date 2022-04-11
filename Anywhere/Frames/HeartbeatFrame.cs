namespace AnywhereNET
{
    public class HeartbeatFrame : Frame
    {
        public HeartbeatFrame()
        {
            FrameType = FrameTypes.Heartbeat;
            Length = 0;
            Payload = new byte[0];
        }

        public HeartbeatFrame(Frame frame) : base(frame) { }
    }
}