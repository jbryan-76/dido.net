﻿namespace AnywhereNET
{
    /// <summary>
    /// A variable-length, thread-safe FIFO buffer that acts like a byte queue with Stream semantics:
    /// Data writes are always appended to the end of the buffer, but reads are random-access
    /// using the Stream Position/Seek API. Once data has been consumed, either automatically after
    /// read or explicitly using Truncate(), the buffer size must be reduced to avoid unbounded growth.
    /// </summary>
    internal class QueueBufferStream : Stream
    {
        /// <summary>
        /// Indicates the position where the next Read() will start.
        /// Writes are always appended to the end of the stream.
        /// <para/>Note this value can decrease after a Truncate().
        /// </summary>
        public override long Position
        {
            get { return CurrentReadPosition; }
            set { Seek(value, SeekOrigin.Begin); }
        }

        /// <summary>
        /// The length of the stream in bytes.
        /// </summary>
        public override long Length { get { return TotalLength; } }

        /// <summary>
        /// Indicates the stream can read. Always returns true.
        /// </summary>
        public override bool CanRead => true;

        /// <summary>
        /// Indicates the stream can seek. Always returns true.
        /// </summary>
        public override bool CanSeek => true;

        /// <summary>
        /// Indicates the stream can write. Always returns true.
        /// </summary>
        public override bool CanWrite => true;

        /// <summary>
        /// Indicates whether Read() will automatically truncate (ie discard)
        /// the internal buffer as needed after data is successfully read.
        /// <para/>Default value is 'true'.
        /// </summary>
        public bool AutoTruncate { get; set; } = true;

        /// <summary>
        /// Indicates whether there is any data to read.
        /// </summary>
        public bool IsEmpty { get { return Length == 0; } }

        /// <summary>
        /// The list of byte array segments that make up the buffer.
        /// </summary>
        private List<byte[]> Segments = new List<byte[]>();

        /// <summary>
        /// The index of the byte array segment where the current Position is.
        /// </summary>
        private int CurrentSegmentIndex = 0;

        /// <summary>
        /// The byte offset in the current byte array segment where the current Position is.
        /// </summary>
        private int CurrentSegmentOffset = 0;

        /// <summary>
        /// The internal position where the next Read() will start.
        /// </summary>
        private long CurrentReadPosition = 0;

        /// <summary>
        /// The total length in bytes of all byte array segments.
        /// </summary>
        private long TotalLength = 0;

        /// <summary>
        /// Creates a new QueueBufferStream instance.
        /// </summary>
        /// <param name="autoTruncate">The initial value of AutoConsume.</param>
        public QueueBufferStream(bool autoTruncate = true)
        {
            AutoTruncate = autoTruncate;
        }

        /// <summary>
        /// Clears the buffer contents.
        /// </summary>
        public void Clear()
        {
            lock (Segments)
            {
                Segments.Clear();
                CurrentReadPosition = 0;
                CurrentSegmentOffset = 0;
                CurrentSegmentIndex = 0;
                TotalLength = 0;
            }
        }

        /// <summary>
        /// Delete all segments before the current position.
        /// <para/>Note the Position will likely decrease after this method completes.
        /// </summary>
        public void Truncate()
        {
            lock (Segments)
            {
                TruncateImplementation();
            }
        }

        /// <summary>
        /// Delete all segments before the provided position.
        /// <para/>Note the Position will likely decrease after this method completes.
        /// </summary>
        public void Truncate(long position)
        {
            Position = position;
            Truncate();
        }

        /// <summary>
        /// This method does nothing.
        /// </summary>
        public override void Flush() { }

        /// <summary>
        /// Reads from and advances the current Position.
        /// Returns the number of bytes read.
        /// <para/>Note: if there is not enough data in the buffer to complete the read,
        /// this method returns 0 and does not change the Position.
        /// <para/>Note: if AutoConsume is true, the internal buffer is released as needed after
        /// data is read, otherwise Truncate() must be called periodically when appropriate
        /// to prevent the buffer from growing without bound.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns>The number of bytes read</returns>
        /// <exception cref="EndOfStreamException"></exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (Segments)
            {
                // if there is not enough data to read, return 0
                if (Length - CurrentReadPosition < count || Segments.Count == 0)
                {
                    return 0;
                }

                // iteratively copy from the current position
                int read = 0;
                var remaining = count;
                while (remaining > 0)
                {
                    // the current segment
                    var segment = Segments[CurrentSegmentIndex];
                    // how many bytes are left to read in the current segment?
                    int remainingInSegment = segment.Length - CurrentSegmentOffset;
                    // how many bytes can be copied in this loop iteration?
                    int size = Math.Min(remaining, remainingInSegment);
                    // copy the bytes from the segment to the buffer
                    Buffer.BlockCopy(segment, CurrentSegmentOffset, buffer, offset, size);
                    // update counters
                    offset += size;
                    read += size;
                    remaining -= size;
                    remainingInSegment -= size;
                    CurrentSegmentOffset += size;
                    CurrentReadPosition += size;
                    // if the entire segment has been read, advance to the next one
                    if (remainingInSegment == 0)
                    {
                        if (remaining > 0 && CurrentSegmentIndex + 1 >= Segments.Count)
                        {
                            // this should be impossible, but keep it to be robust
                            throw new EndOfStreamException();
                        }

                        // update to the next segment and discard read data, if necessary
                        CurrentSegmentIndex++;
                        CurrentSegmentOffset = 0;
                        if (AutoTruncate)
                        {
                            TruncateImplementation();
                        }
                    }
                }
                return read;
            }
        }

        /// <summary>
        /// Set the current Position.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            lock (Segments)
            {
                var newPosition = CurrentReadPosition;
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        newPosition = offset;
                        break;
                    case SeekOrigin.End:
                        newPosition = Length - offset;
                        break;
                    case SeekOrigin.Current:
                        newPosition += offset;
                        break;
                }
                if (newPosition == CurrentReadPosition)
                {
                    return CurrentReadPosition;
                }
                else if (newPosition < 0 || newPosition > Length)
                {
                    throw new IndexOutOfRangeException($"Position {Position} is outside the bounds of the buffer.");
                }

                // update the tracking fields
                CurrentReadPosition = newPosition;

                // determine the new segment offset and index
                long bytesFromFront = 0;
                CurrentSegmentOffset = 0;
                for (CurrentSegmentIndex = 0; CurrentSegmentIndex < Segments.Count; CurrentSegmentIndex++)
                {
                    var segment = Segments[CurrentSegmentIndex];
                    if (CurrentReadPosition < bytesFromFront + segment.Length)
                    {
                        CurrentSegmentOffset = (int)(CurrentReadPosition - bytesFromFront);
                        break;
                    }
                    bytesFromFront += segment.Length;
                }
                return CurrentReadPosition;
            }
        }

        /// <summary>
        /// Not implemented. Throws NotImplementedException.
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="NotImplementedException"></exception>
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Appends the specified data to the end of the stream.
        /// This method DOES NOT change the Position.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (Segments)
            {
                // copy the indicated byterange and append to the segments list
                var bytes = new byte[count];
                Array.Copy(buffer, offset, bytes, 0, count);
                Segments.Add(bytes);
                TotalLength += count;
            }
        }

        /// <summary>
        /// A finalizer is necessary when inheriting from Stream.
        /// </summary>
        ~QueueBufferStream()
        {
            Dispose(false);
        }

        /// <summary>
        /// Override of Stream.Dispose.
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                // dispose any managed objects
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Implements the truncation. 
        /// This is intended to only be used within a critical section.
        /// </summary>
        private void TruncateImplementation()
        {
            for (int i = 0; i < CurrentSegmentIndex; i++)
            {
                var segment = Segments[0];
                CurrentReadPosition -= segment.Length;
                TotalLength -= segment.Length;
                Segments.RemoveAt(0);
            }
            CurrentSegmentIndex = 0;
        }
    }
}