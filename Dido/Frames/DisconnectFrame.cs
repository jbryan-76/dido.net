namespace DidoNet
{
    public class DisconnectFrame : Frame
    {
        public DisconnectFrame()
        {
            FrameType = FrameTypes.Disconnect;
            Length = 0;
            Payload = new byte[0];
        }

        public DisconnectFrame(Frame frame) : base(frame) { }
    }
}