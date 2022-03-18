using System.Collections.Concurrent;

namespace AnywhereNET
{
    public class Channel : Stream
    {
        public delegate void DataAvailableHandler(Channel channel);

        public event DataAvailableHandler? OnDataAvailable;

        public ushort ChannelNumber { get; private set; }

        public Connection Connection { get; private set; }

        private ConcurrentQueue<ArraySegment<byte>> DataQueue = new ConcurrentQueue<ArraySegment<byte>>();
        private int CurrentSegmentOffset = 0;

        internal Channel(Connection connection, ushort channelNumber)
        {
            Connection = connection;
            ChannelNumber = channelNumber;
        }

        public override bool CanRead => true;

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = 0;
            int remaining = count;
            while (remaining > 0)
            {
                // peek at the next available segment
                if (DataQueue.TryPeek(out ArraySegment<byte> segment))
                {
                    // how many bytes remain to be read in the current segment?
                    int remainingInSegment = segment.Count - CurrentSegmentOffset;
                    // how many bytes can be copied in this loop iteration?
                    int size = Math.Min(remaining, remainingInSegment);
                    // copy the bytes from the segment to the buffer
                    Buffer.BlockCopy(segment.Array!, segment.Offset + CurrentSegmentOffset, buffer, offset, size);
                    // update counters
                    offset += size;
                    read += size;
                    remaining -= size;
                    remainingInSegment -= size;
                    CurrentSegmentOffset += size;
                    // if the entire segment has been read, remove it
                    if (remainingInSegment == 0)
                    {
                        DataQueue.TryDequeue(out segment);
                        CurrentSegmentOffset = 0;
                    }
                }
                else
                {
                    // no more data is available
                    break;
                }
            }
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            int remaining = count;
            while (remaining > 0)
            {
                int size = Math.Min(remaining, Frame.MaxFrameSize);
                Connection.EnqueueFrame(new Frame
                {
                    FrameType = FrameTypes.ChannelData,
                    Channel = ChannelNumber,
                    Length = size,
                    Payload = new ArraySegment<byte>(buffer, offset, size)
                });
                remaining -= size;
                offset += size;
            }
        }

        internal void Receive(ArraySegment<byte> data)
        {
            // when the connection receives data for this channel, enqueue it to be later read by a caller
            DataQueue.Enqueue(data);
            // notify all event handlers new data is available
            OnDataAvailable?.Invoke(this);
        }
    }

}