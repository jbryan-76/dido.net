using System;
using System.Linq;

namespace DidoNet
{
    public class HeartbeatFrame : Frame
    {
        public int TimeoutInSeconds
        {
            get
            {
                var bytes = Payload.ToArray();
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }
                return BitConverter.ToInt32(bytes);
            }
        }

        public HeartbeatFrame(int timeoutInSeconds)
        {
            var bytes = BitConverter.GetBytes(timeoutInSeconds);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            FrameType = FrameTypes.Heartbeat;
            Length = bytes.Length;
            Payload = bytes;
        }

        public HeartbeatFrame(Frame frame) : base(frame) { }
    }
}