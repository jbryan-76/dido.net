namespace DidoNet
{
    public class ChannelDataFrame : Frame
    {
        public ChannelDataFrame(ushort channelNumber, byte[] payload)
        {
            FrameType = FrameTypes.ChannelData;
            Channel = channelNumber;
            Length = payload.Length;
            Payload = payload;
        }

        public ChannelDataFrame(Frame frame) : base(frame) { }
    }
}