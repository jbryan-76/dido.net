namespace AnywhereNET
{
    public static class StreamFrameExtensions
    {
        public static async Task<Frame> ReadFrameAsync(this Stream stream)
        {
            // TODO: use async reading?
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

        public static Frame ReadFrame(this Stream stream)
        {
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

        public static async Task WriteFrameAsync(this Stream stream, Frame frame)
        {
            // TODO: use async writing?
            stream.WriteByte(frame.Type);
            stream.WriteUInt16BE(frame.Channel);
            stream.WriteInt32BE(frame.Length);
            if (frame.Length > 0)
            {
                //stream.Write(frame.Payload.Array, frame.Payload.Offset, frame.Payload.Count);
                stream.Write(frame.Payload, 0, frame.Payload.Length);
                //Console.WriteLine("wrote bytes=" + String.Join(' ', frame.Payload.ToArray().Select(b => b.ToString())));
            }
            stream.Flush();
        }

        public static void WriteFrame(this Stream stream, Frame frame)
        {
            stream.WriteByte(frame.Type);
            stream.WriteUInt16BE(frame.Channel);
            stream.WriteInt32BE(frame.Length);
            if (frame.Length > 0)
            {
                //stream.Write(frame.Payload.Array, frame.Payload.Offset, frame.Payload.Count);
                stream.Write(frame.Payload, 0, frame.Payload.Length);
                //Console.WriteLine("wrote bytes=" + String.Join(' ', frame.Payload.ToArray().Select(b => b.ToString())));
            }
            stream.Flush();
        }
    }
}