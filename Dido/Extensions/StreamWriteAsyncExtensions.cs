using System.Text;

namespace DidoNet
{
    public static class StreamWriteAsyncExtensions
    {
        /// <summary>
        /// Write the provided byte array to a stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="bytes"></param>
        public static ValueTask WriteBytesAsync(this Stream stream, byte[] bytes, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (bytes.Length > 0)
            {
                return stream.WriteAsync(bytes, cancellationToken);
            }
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Write the provided boolean value to a stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static ValueTask WriteBooleanAsync(this Stream stream, bool value, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return WriteBytesAsync(stream, bytes, cancellationToken);
        }

        /// <summary>
        /// Write the provided char value to a stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static ValueTask WriteCharAsync(this Stream stream, char value, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return WriteBytesAsync(stream, bytes, cancellationToken);
        }

        /// <summary>
        /// Write the provided short value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static ValueTask WriteInt16BEAsync(this Stream stream, short value, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return WriteBytesAsync(stream, bytes, cancellationToken);
        }

        /// <summary>
        /// Write the provided ushort value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static ValueTask WriteUInt16BEAsync(this Stream stream, ushort value, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return WriteBytesAsync(stream, bytes, cancellationToken);
        }

        /// <summary>
        /// Write the provided int value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static ValueTask WriteInt32BEAsync(this Stream stream, int value, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return WriteBytesAsync(stream, bytes, cancellationToken);
        }

        /// <summary>
        /// Write the provided uint value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static ValueTask WriteUInt32BEAsync(this Stream stream, uint value, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return WriteBytesAsync(stream, bytes, cancellationToken);
        }

        /// <summary>
        /// Write the provided long value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static ValueTask WriteInt64BEAsync(this Stream stream, long value, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return WriteBytesAsync(stream, bytes, cancellationToken);
        }

        /// <summary>
        /// Write the provided ulong value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static ValueTask WriteUInt64BEAsync(this Stream stream, ulong value, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return WriteBytesAsync(stream, bytes, cancellationToken);
        }

        /// <summary>
        /// Write the provided float value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static ValueTask WriteSingleBEAsync(this Stream stream, float value, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return WriteBytesAsync(stream, bytes, cancellationToken);
        }

        /// <summary>
        /// Write the provided double value to a stream in network-byte-order (ie Big Endian).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static ValueTask WriteDoubleBEAsync(this Stream stream, double value, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return WriteBytesAsync(stream, bytes, cancellationToken);
        }

        /// <summary>
        /// Write the provided string to a stream as a length-prefixed character array.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static async ValueTask WriteStringAsync(this Stream stream, string value, CancellationToken cancellationToken = default(CancellationToken))
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            await stream.WriteInt32BEAsync(bytes.Length, cancellationToken);
            await WriteBytesAsync(stream, bytes, cancellationToken);
        }

        /// <summary>
        /// Write the provided array to a stream using the provided writer action.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <param name="array"></param>
        /// <param name="writer"></param>
        public static async ValueTask WriteArrayAsync<T>(this Stream stream, T[] array, Func<Stream, T, CancellationToken, Task> writer, CancellationToken cancellationToken = default(CancellationToken))
        {
            await stream.WriteInt32BEAsync(array.Length, cancellationToken);
            foreach (var item in array)
            {
                await writer(stream, item, cancellationToken);
            }
        }
    }
}