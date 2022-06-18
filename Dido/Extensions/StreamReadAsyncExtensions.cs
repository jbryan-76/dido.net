using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DidoNet
{
    public static class StreamReadAsyncExtensions
    {
        /// <summary>
        /// Read the given number of bytes from the stream.
        /// <para/>
        /// This method blocks until the specified number of bytes is reached, or throws IOException if 
        /// the stream is closed or the end of the stream is reached prior to reading the expected amount of data.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="length"></param>
        public static async Task<byte[]> ReadBytesAsync(this Stream stream, int length, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = new byte[length];
            var remaining = length;
            while (remaining > 0)
            {
                int read = await stream.ReadAsync(bytes, length - remaining, remaining, cancellationToken);
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
        public static async Task<bool> ReadBooleanAsync(this Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            return BitConverter.ToBoolean(await stream.ReadBytesAsync(1, cancellationToken));
        }

        /// <summary>
        /// Read a char value from a stream.
        /// </summary>
        /// <param name="stream"></param>
        public static async Task<char> ReadCharAsync(this Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = await stream.ReadBytesAsync(2, cancellationToken);
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
        public static async Task<short> ReadInt16BEAsync(this Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = await stream.ReadBytesAsync(2, cancellationToken);
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
        public static async Task<ushort> ReadUInt16BEAsync(this Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = await stream.ReadBytesAsync(2, cancellationToken);
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
        public static async Task<int> ReadInt32BEAsync(this Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = await stream.ReadBytesAsync(4, cancellationToken);
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
        public static async Task<uint> ReadUInt32BEAsync(this Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = await stream.ReadBytesAsync(4, cancellationToken);
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
        public static async Task<long> ReadInt64BEAsync(this Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = await stream.ReadBytesAsync(8, cancellationToken);
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
        public static async Task<ulong> ReadUInt64BEAsync(this Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = await stream.ReadBytesAsync(8, cancellationToken);
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
        public static async Task<float> ReadSingleBEAsync(this Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = await stream.ReadBytesAsync(4, cancellationToken);
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
        public static async Task<double> ReadDoubleBEAsync(this Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = await stream.ReadBytesAsync(8, cancellationToken);
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
        public static async Task<string> ReadStringAsync(this Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var length = await stream.ReadInt32BEAsync(cancellationToken);
            var bytes = await stream.ReadBytesAsync(length, cancellationToken);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Read an array from a stream using the provided reader function.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="reader"></param>
        public static async Task<T[]> ReadArrayAsync<T>(this Stream stream, Func<Stream, CancellationToken, Task<T>> reader, CancellationToken cancellationToken = default(CancellationToken))
        {
            int numItems = await stream.ReadInt32BEAsync(cancellationToken);
            var array = new T[numItems];
            for (int i = 0; i < numItems; ++i)
            {
                array[i] = await reader(stream, cancellationToken);
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
        /// <returns>True if the number of bytes were read successfully, else false.</returns>
        public static async Task<byte[]?> TryReadBytesAsync(this Stream stream, int length, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream.Length - stream.Position < length)
            {
                return null;
            }
            var buffer = new byte[length];
            await stream.ReadAsync(buffer, 0, length, cancellationToken);
            return buffer;
        }

        /// <summary>
        /// Try to read a byte value from a stream.
        /// <para/>Note the stream must support Position and Length properties,
        /// and data is only read if enough bytes are available.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns>True if the value was read successfully, else false.</returns>
        public static async Task<byte?> TryReadByteAsync(this Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = await stream.TryReadBytesAsync(1, cancellationToken);
            return bytes == null ? (byte?)null : bytes[0];
        }

        /// <summary>
        /// Try to read a ushort value from a stream in network-byte-order (i.e. Big Endian).
        /// <para/>Note the stream must support Position and Length properties,
        /// and data is only read if enough bytes are available.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns>True if the value was read successfully, else false.</returns>
        public static async Task<ushort?> TryReadUShortBEAsync(this Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = await stream.TryReadBytesAsync(2, cancellationToken);
            if (bytes == null)
            {
                return null;
            }
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToUInt16(bytes);
        }

        /// <summary>
        /// Try to read an int value from a stream in network-byte-order (i.e. Big Endian).
        /// <para/>Note the stream must support Position and Length properties,
        /// and data is only read if enough bytes are available.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns>True if the value was read successfully, else false.</returns>
        public static async Task<int?> TryReadIntBEAsync(this Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = await stream.TryReadBytesAsync(4, cancellationToken);
            if (bytes == null)
            {
                return null;
            }
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToInt32(bytes);
        }
    }
}