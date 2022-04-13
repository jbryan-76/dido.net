namespace AnywhereNET
{
    public class HeartbeatFrame : Frame
    {
        public int PeriodInMs
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

        public HeartbeatFrame(int periodInMs)
        {
            var bytes = BitConverter.GetBytes(periodInMs);
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