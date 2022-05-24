using System.IO;

namespace DidoNet.Test
{
    internal class TestMessage : IMessage
    {
        public int MyIntValue { get; set; }

        public string MyStringValue { get; set; } = string.Empty;

        public void Read(Stream stream)
        {
            MyIntValue = stream.ReadInt32BE();
            MyStringValue = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            stream.WriteInt32BE(MyIntValue);
            stream.WriteString(MyStringValue);
        }
    }
}