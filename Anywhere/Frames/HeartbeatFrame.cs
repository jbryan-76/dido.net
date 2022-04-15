namespace AnywhereNET
{
    public class HeartbeatFrame : Frame
    {
        public int PeriodInSeconds
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

        public HeartbeatFrame(int periodInSeconds)
        {
            var bytes = BitConverter.GetBytes(periodInSeconds);
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