namespace DidoNet
{
    internal class TaskRequestMessage : IMessage
    {
        public byte[] Bytes { get; private set; } = new byte[0];

        public int TimeoutInMs { get; private set; } = -1;

        public TaskRequestMessage() { }

        public TaskRequestMessage(byte[] bytes, int timeoutInMs = Timeout.Infinite)
        {
            Bytes = bytes;
            TimeoutInMs = timeoutInMs;
        }

        public void Read(Stream stream)
        {
            TimeoutInMs = stream.ReadInt32BE();
            int length = stream.ReadInt32BE();
            Bytes = stream.ReadBytes(length);
        }

        public void Write(Stream stream)
        {
            stream.WriteInt32BE(TimeoutInMs);
            stream.WriteInt32BE(Bytes.Length);
            stream.Write(Bytes);
        }
    }
}