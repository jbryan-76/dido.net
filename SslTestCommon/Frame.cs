namespace SslTestCommon
{
    public class Frame
    {
        public byte Type { get; set; }
        public ushort Channel { get; set; }
        public uint Length { get; set; }
        public byte[] Payload { get; set; }
    }

    public class FrameUtil
    {
        //public static async Task<Frame> ReadFrame(Stream stream)
        //{

        //}

        //public static async Task WriteFrame(Frame frame, Stream stream)
        //{

        //}
    }
}