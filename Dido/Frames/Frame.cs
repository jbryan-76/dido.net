﻿namespace DidoNet
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
        
        public ushort Channel { get; set; }
        
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
    }
}