using System;
using System.IO;
using System.Text;

namespace DidoNet
{
    public static class StreamWriteExtensions
    {
        /// <summary>
        /// Write the provided byte array to a stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="bytes"></param>
        public static void WriteBytes(this Stream stream, byte[] bytes)
        {
            if (bytes.Length > 0)
            {
                stream.Write(bytes);
            }
        }

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
            WriteBytes(stream, bytes);
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
            WriteBytes(stream, bytes);
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
            WriteBytes(stream, bytes);
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
            WriteBytes(stream, bytes);
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
            WriteBytes(stream, bytes);
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
            WriteBytes(stream, bytes);
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
            WriteBytes(stream, bytes);
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
            WriteBytes(stream, bytes);
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
            WriteBytes(stream, bytes);
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
            WriteBytes(stream, bytes);
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
            WriteBytes(stream, bytes);
        }

        /// <summary>
        /// Write the provided array to a stream using the provided writer action.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <param name="array"></param>
        /// <param name="writer"></param>
        public static void WriteArray<T>(this Stream stream, T[] array, Action<Stream, T> writer)
        {
            stream.WriteInt32BE(array.Length);
            foreach (var item in array)
            {
                writer(stream, item);
            }
        }
    }
}