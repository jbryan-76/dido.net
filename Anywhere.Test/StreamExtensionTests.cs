using System.IO;
using System.Linq;
using Xunit;

namespace AnywhereNET.Test
{
    public class StreamExtensionTests
    {
        [Fact]
        public void Boolean()
        {
            using (var stream = new MemoryStream())
            {
                var tx = true;
                stream.WriteBoolean(tx);
                stream.Position = 0;
                var rx = stream.ReadBoolean();
                Assert.Equal(tx, rx);
            }
        }

        [Fact]
        public void Char()
        {
            using (var stream = new MemoryStream())
            {
                var tx = 'A';
                stream.WriteChar(tx);
                stream.Position = 0;
                var rx = stream.ReadChar();
                Assert.Equal(tx, rx);
            }
        }

        [Fact]
        public void Short()
        {
            using (var stream = new MemoryStream())
            {
                var tx = (short)-123;
                stream.WriteInt16BE(tx);
                stream.Position = 0;
                var rx = stream.ReadInt16BE();
                Assert.Equal(tx, rx);
            }
        }

        [Fact]
        public void UShort()
        {
            using (var stream = new MemoryStream())
            {
                var tx = (ushort)123;
                stream.WriteUInt16BE(tx);
                stream.Position = 0;
                var rx = stream.ReadUInt16BE();
                Assert.Equal(tx, rx);
            }
        }

        [Fact]
        public void Int()
        {
            using (var stream = new MemoryStream())
            {
                var tx = (int)-123;
                stream.WriteInt32BE(tx);
                stream.Position = 0;
                var rx = stream.ReadInt32BE();
                Assert.Equal(tx, rx);
            }
        }

        [Fact]
        public void UInt()
        {
            using (var stream = new MemoryStream())
            {
                var tx = (uint)123;
                stream.WriteUInt32BE(tx);
                stream.Position = 0;
                var rx = stream.ReadUInt32BE();
                Assert.Equal(tx, rx);
            }
        }

        [Fact]
        public void Long()
        {
            using (var stream = new MemoryStream())
            {
                var tx = (long)-123;
                stream.WriteInt64BE(tx);
                stream.Position = 0;
                var rx = stream.ReadInt64BE();
                Assert.Equal(tx, rx);
            }
        }

        [Fact]
        public void ULong()
        {
            using (var stream = new MemoryStream())
            {
                var tx = (ulong)123;
                stream.WriteUInt64BE(tx);
                stream.Position = 0;
                var rx = stream.ReadUInt64BE();
                Assert.Equal(tx, rx);
            }
        }

        [Fact]
        public void Single()
        {
            using (var stream = new MemoryStream())
            {
                var tx = (float)3.1415926;
                stream.WriteSingleBE(tx);
                stream.Position = 0;
                var rx = stream.ReadSingleBE();
                Assert.Equal(tx, rx);
            }
        }

        [Fact]
        public void Double()
        {
            using (var stream = new MemoryStream())
            {
                var tx = (double)3.1415926;
                stream.WriteDoubleBE(tx);
                stream.Position = 0;
                var rx = stream.ReadDoubleBE();
                Assert.Equal(tx, rx);
            }
        }

        [Fact]
        public void String()
        {
            using (var stream = new MemoryStream())
            {
                var tx = "hello world";
                stream.WriteString(tx);
                stream.Position = 0;
                var rx = stream.ReadString();
                Assert.Equal(tx, rx);
            }
        }

        [Fact]
        public void StringArray()
        {
            using (var stream = new MemoryStream())
            {
                var tx = new string[] { "one", "two", "three" };
                stream.WriteArray(tx, (s, item) => s.WriteString(item));

                stream.Position = 0;

                var rx = stream.ReadArray((s) => s.ReadString());

                Assert.True(Enumerable.SequenceEqual(tx, rx));
            }
        }

        [Fact]
        public void IntArray()
        {
            using (var stream = new MemoryStream())
            {
                var tx = new int[] { 1, 2, 3 };
                stream.WriteArray(tx, (s, item) => s.WriteInt32BE(item));

                stream.Position = 0;

                var rx = stream.ReadArray((s) => s.ReadInt32BE());

                Assert.True(Enumerable.SequenceEqual(tx, rx));
            }
        }
    }
}