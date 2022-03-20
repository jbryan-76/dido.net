using System.Text;

namespace AnywhereNET
{
    public enum FrameTypes
    {
        // heartbeat?
        // diconnect?
        // event?
        ChannelData,
        Disconnect,
        Debug
    }

    // TODO: for sending, create larger message constructs that are broken up into frames and multiplexed into an async producer/consumer queue for sending
    // TODO: for receiving, create buffers to aggregate frames until the larger messages are complete

    public class Frame
    {
        static public int MaxFrameSize = 512 * 1024;

        public byte Type { get; set; }
        public ushort Channel { get; set; }
        public int Length { get; set; }
        public byte[] Payload { get; set; }

        public FrameTypes FrameType
        {
            get
            {
                return (FrameTypes)Type;
            }
            set
            {
                Type = (byte)value;
            }
        }

        public Frame() { }

        internal Frame(Frame frame)
        {
            FrameType = frame.FrameType;
            Channel = frame.Channel;
            Length = frame.Length;
            Payload = frame.Payload;
        }

        public override string ToString()
        {
            return $"Frame '{FrameType}' on channel {Channel}: {Length} bytes ({Encoding.UTF8.GetString(Payload.ToArray())})";
        }
    }
}