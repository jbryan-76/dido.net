using System;
using System.IO;
using System.Text;

namespace DidoNet
{
    public static class StreamReadExtensions
    {
        /// <summary>
        /// Read the given number of bytes from the stream.
        /// <para/>
        /// This method blocks until the specified number of bytes is reached, or throws IOException if 
        /// the stream is closed or the end of the stream is reached prior to reading the expected amount of data.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="length"></param>
        public static byte[] ReadBytes(this Stream stream, int length)
        {
            var bytes = new byte[length];
            var remaining = length;
            while (remaining > 0)
            {
                int read = stream.Read(bytes, length - remaining, remaining);
                remaining -= read;
                if (read == 0 && remaining > 0)
                {
                    throw new IOException("Unexpected end of stream reached; The stream may be closed.");
                }
            }
            return bytes;
        }

        /// <summary>
        /// Read a boolean value from a stream.
        /// </summary>
        /// <param name="stream"></param>
        public static bool ReadBoolean(this Stream stream)
        {
            return BitConverter.ToBoolean(stream.ReadBytes(1));
        }

        /// <summary>
        /// Read a char value from a stream.
        /// </summary>
        /// <param name="stream"></param>
        public static char ReadChar(this Stream stream)
        {
            var bytes = stream.ReadBytes(2);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToChar(bytes);
        }

        /// <summary>
        /// Read a short value from a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        public static short ReadInt16BE(this Stream stream)
        {
            var bytes = stream.ReadBytes(2);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToInt16(bytes);
        }

        /// <summary>
        /// Read a ushort value from a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        public static ushort ReadUInt16BE(this Stream stream)
        {
            var bytes = stream.ReadBytes(2);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToUInt16(bytes);
        }

        /// <summary>
        /// Read an int value from a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        public static int ReadInt32BE(this Stream stream)
        {
            var bytes = stream.ReadBytes(4);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToInt32(bytes);
        }

        /// <summary>
        /// Read a uint value from a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        public static uint ReadUInt32BE(this Stream stream)
        {
            var bytes = stream.ReadBytes(4);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToUInt32(bytes);
        }

        /// <summary>
        /// Read a long value from a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        public static long ReadInt64BE(this Stream stream)
        {
            var bytes = stream.ReadBytes(8);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToInt64(bytes);
        }

        /// <summary>
        /// Read a ulong value from a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        public static ulong ReadUInt64BE(this Stream stream)
        {
            var bytes = stream.ReadBytes(8);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToUInt64(bytes);
        }

        /// <summary>
        /// Read a float value from a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        public static float ReadSingleBE(this Stream stream)
        {
            var bytes = stream.ReadBytes(4);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToSingle(bytes);
        }

        /// <summary>
        /// Read a double value from a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        public static double ReadDoubleBE(this Stream stream)
        {
            var bytes = stream.ReadBytes(8);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToDouble(bytes);
        }

        /// <summary>
        /// Read a string from a stream as a length-prefixed character array.
        /// </summary>
        /// <param name="stream"></param>
        public static string ReadString(this Stream stream)
        {
            var length = stream.ReadInt32BE();
            var bytes = stream.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Read an array from a stream using the provided reader function.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="reader"></param>
        public static T[] ReadArray<T>(this Stream stream, Func<Stream, T> reader)
        {
            int numItems = stream.ReadInt32BE();
            var array = new T[numItems];
            for (int i = 0; i < numItems; ++i)
            {
                array[i] = reader(stream);
            }
            return array;
        }

        /// <summary>
        /// Try to read the given number of bytes from the stream.
        /// <para/>Note the stream must support Position and Length properties,
        /// and data is only read if enough bytes are available.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="length"></param>
        /// <param name="buffer"></param>
        /// <returns>True if the number of bytes were read successfully, else false.</returns>
        public static bool TryReadBytes(this Stream stream, int length, out byte[]? buffer)
        {
            if (stream.Length - stream.Position < length)
            {
                buffer = null;
                return false;
            }
            buffer = new byte[length];
            stream.Read(buffer, 0, length);
            return true;
        }

        /// <summary>
        /// Try to read a byte value from a stream.
        /// <para/>Note the stream must support Position and Length properties,
        /// and data is only read if enough bytes are available.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        /// <returns>True if the value was read successfully, else false.</returns>
        public static bool TryReadByte(this Stream stream, out byte value)
        {
            value = 0;
            if (!stream.TryReadBytes(1, out var bytes))
            {
                return false;
            }
            value = bytes![0];
            return true;
        }

        /// <summary>
        /// Try to read a ushort value from a stream in network-byte-order (ie Big Endian).
        /// <para/>Note the stream must support Position and Length properties,
        /// and data is only read if enough bytes are available.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        /// <returns>True if the value was read successfully, else false.</returns>
        public static bool TryReadUShortBE(this Stream stream, out ushort value)
        {
            value = 0;
            if (!stream.TryReadBytes(2, out var bytes))
            {
                return false;
            }
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes!);
            }
            value = BitConverter.ToUInt16(bytes);
            return true;
        }

        /// <summary>
        /// Try to read an int value from a stream in network-byte-order (ie Big Endian).
        /// <para/>Note the stream must support Position and Length properties,
        /// and data is only read if enough bytes are available.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        /// <returns>True if the value was read successfully, else false.</returns>
        public static bool TryReadIntBE(this Stream stream, out int value)
        {
            value = 0;
            if (!stream.TryReadBytes(4, out var bytes))
            {
                return false;
            }
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes!);
            }
            value = BitConverter.ToInt32(bytes);
            return true;
        }
    }
}