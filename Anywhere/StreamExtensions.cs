﻿using System.Text;

namespace AnywhereNET
{
    // TODO: refactor into StreamReadExtensions and StreamWriteExtensions
    public static class StreamExtensions
    {
        /// <summary>
        /// Write the provided boolean value to a stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static void WriteBoolean(this Stream stream, bool value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            stream.Write(bytes);
        }

        /// <summary>
        /// Write the provided char value to a stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static void WriteChar(this Stream stream, char value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            stream.Write(bytes);
        }

        /// <summary>
        /// Write the provided short value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static void WriteInt16BE(this Stream stream, short value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            stream.Write(bytes);
        }

        /// <summary>
        /// Write the provided ushort value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static void WriteUInt16BE(this Stream stream, ushort value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            stream.Write(bytes);
        }

        /// <summary>
        /// Write the provided int value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static void WriteInt32BE(this Stream stream, int value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            stream.Write(bytes);
        }

        /// <summary>
        /// Write the provided uint value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static void WriteUInt32BE(this Stream stream, uint value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            stream.Write(bytes);
        }

        /// <summary>
        /// Write the provided long value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static void WriteInt64BE(this Stream stream, long value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            stream.Write(bytes);
        }

        /// <summary>
        /// Write the provided ulong value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static void WriteUInt64BE(this Stream stream, ulong value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            stream.Write(bytes);
        }

        /// <summary>
        /// Write the provided float value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static void WriteSingleBE(this Stream stream, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            stream.Write(bytes);
        }

        /// <summary>
        /// Write the provided double value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static void WriteDoubleBE(this Stream stream, double value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            stream.Write(bytes);
        }

        /// <summary>
        /// Write the provided string to a stream as a length-prefixed character array.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static void WriteString(this Stream stream, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            stream.WriteInt32BE(bytes.Length);
            stream.Write(bytes);
        }

        /// <summary>
        /// Read the given number of bytes from the stream.
        /// <para/>
        /// This method blocks until the specified number of bytes is reached, or throws IOException if 
        /// the stream is closed or the end of the stream is reached prior to reading the expected amount of data.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static byte[] ReadBytes(this Stream stream, int length)
        {
            var bytes = new byte[length];
            var remaining = length;
            //Console.WriteLine($"ReadBytes({length})");
            while (remaining > 0)
            {
                int read = stream.Read(bytes, length - remaining, remaining);
                remaining -= read;
                if (read == 0 && remaining > 0)
                {
                    throw new IOException("Unexpected end of stream reached; The stream may be closed.");
                }
            }
            //Console.WriteLine("  read bytes =" + string.Join(' ', bytes.Select(b => b.ToString())));
            return bytes;
        }

        /// <summary>
        /// Read a boolean value from a stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static bool ReadBoolean(this Stream stream)
        {
            return BitConverter.ToBoolean(stream.ReadBytes(1));
        }

        /// <summary>
        /// Read a char value from a stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static char ReadChar(this Stream stream)
        {
            return BitConverter.ToChar(stream.ReadBytes(1));
        }

        /// <summary>
        /// Read a short value from a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
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
        /// <param name="value"></param>
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
        /// <param name="value"></param>
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
        /// <param name="value"></param>
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
        /// <param name="value"></param>
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
        /// <param name="value"></param>
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
        /// <param name="value"></param>
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
        /// <param name="value"></param>
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
        /// <param name="value"></param>
        public static string ReadString(this Stream stream)
        {
            var length = stream.ReadInt32BE();
            var bytes = stream.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
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