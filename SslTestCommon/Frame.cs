using System.Text;

namespace SslTestCommon
{
    public enum FrameTypes
    {
        // heartbeat?
        // graceful close?
        // 
    }

    // TODO: for sending, create larger message constructs that are broken up into frames and multiplexed into an async producer/consumer queue for sending
    // TODO: for receiving, create buffers to aggregate frames until the larger messages are complete

    public class Frame
    {
        public byte Type { get; set; }
        public ushort Channel { get; set; }
        public int Length { get; set; }
        public byte[] Payload { get; set; }

        public override string ToString()
        {
            return $"Frame {Type} on channel {Channel}: received {Length} bytes ({Encoding.UTF8.GetString(Payload)})";
        }
    }

    //public class MessageFrame : Frame
    //{
    //    public MessageFrame(string message)
    //    {
    //        Type = (byte)FrameTypes.MessageFrame;
    //        Ch
    //    }
    //}
}