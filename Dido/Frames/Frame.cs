using System;
using System.Linq;

namespace DidoNet
{
    public enum FrameTypes
    {
        ChannelData,
        Disconnect,
        Debug,
        Heartbeat
    }

    public class Frame
    {
        static public int MaxFrameSize = 512 * 1024;

        public byte Type { get; set; }

        public string Channel { get; set; } = string.Empty;

        public int Length { get; set; }

        public byte[] Payload { get; set; } = new byte[0];

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
            return $"Frame '{FrameType}' on channel {Channel}: {Length} bytes";
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }
            if (Object.ReferenceEquals(this, obj))
            {
                return true;
            }
            if (this.GetType() != obj.GetType())
            {
                return false;
            }
            Frame f = (Frame)obj;
            return FrameType == f.FrameType
                && Channel == f.Channel
                && Length == f.Length
                && Enumerable.SequenceEqual(Payload, f.Payload);
        }

        public override int GetHashCode()
        {
            // good enough considering frames won't usually be added to a HashSet nor as a key to a Dictionary.
            return HashCode.Combine(Type, Channel, Length);
        }
    }
}