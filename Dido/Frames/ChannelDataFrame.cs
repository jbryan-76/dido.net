namespace DidoNet
{
    public class ChannelDataFrame : Frame
    {
        public ChannelDataFrame(string channelId, byte[] payload)
        {
            FrameType = FrameTypes.ChannelData;
            Channel = channelId;
            Length = payload.Length;
            Payload = payload;
        }

        public ChannelDataFrame(Frame frame) : base(frame) { }
    }
}