﻿namespace AnywhereNET
{
    internal class TaskRequestMessage : IMessage
    {
        public byte[] Bytes { get; private set; } = new byte[0];

        public TaskRequestMessage() { }

        public TaskRequestMessage(byte[] bytes)
        {
            Bytes = bytes;
        }

        public void Read(Stream stream)
        {
            int length = stream.ReadInt32BE();
            Bytes = stream.ReadBytes(length);
        }

        public void Write(Stream stream)
        {
            stream.WriteInt32BE(Bytes.Length);
            stream.Write(Bytes);
        }
    }
}