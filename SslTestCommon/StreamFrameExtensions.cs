namespace SslTestCommon
{
    public static class StreamFrameExtensions
    {
        public static async Task<Frame> ReadFrame(this Stream stream)
        {
            // TODO: any value in converting to async reading?
            // use ReadBytes instead of ReadByte so the stream throws if no data is available instead of returning -1
            var type = stream.ReadBytes(1)[0];
            var channel = stream.ReadUShortBE();
            var length = stream.ReadIntBE();
            var payload = stream.ReadBytes(length);
            return new Frame
            {
                Type = type,
                Channel = channel,
                Length = length,
                Payload = payload
            };
        }

        public static async Task WriteFrame(this Stream stream, Frame frame)
        {
            // TODO: any value in converting to async writing?
            stream.WriteByte(frame.Type);
            stream.WriteUInt16BE(frame.Channel);
            stream.WriteInt32BE(frame.Length);
            stream.Write(frame.Payload);
            stream.Flush();
        }
    }
}