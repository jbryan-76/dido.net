namespace DidoNet.IO
{
    public class FileStreamProxy : Stream
    {
        public string Name { get; private set; }

        public override long Position { get; set; }

        public override long Length { get { return _length; } }

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

        internal MessageChannel Channel { get; set; }

        private long _length;

        private Action<string>? OnDispose = null;

        public override void Flush()
        {
        }

        // TODO: override ReadAsync to provide dedicated implementation?

        public override int Read(byte[] buffer, int offset, int count)
        {
            Channel.Send(new FileReadRequestMessage(Name, Position, count));
            var response = Channel.ReceiveMessage<FileReadResponseMessage>();
            response.ThrowOnError();
            Buffer.BlockCopy(response.Bytes, 0, buffer, offset, response.Bytes.Length);
            return response.Bytes.Length;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var newPosition = Position;
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
            if (newPosition < 0 || newPosition > Length)
            {
                throw new IndexOutOfRangeException($"Position {newPosition} is outside the bounds of the buffer.");
            }

            Position = newPosition;

            return Position;
        }

        public override void SetLength(long value)
        {
            Channel.Send(new FileSetLengthMessage(Name, value));
            var ack = Channel.ReceiveMessage<FileAckMessage>();
            ack.ThrowOnError();
        }

        // TODO: override WriteAsync to provide dedicated implementation?

        public override void Write(byte[] buffer, int offset, int count)
        {
            var data = new byte[count];
            Buffer.BlockCopy(buffer, offset, data, 0, count);
            Channel.Send(new FileWriteMessage(Name, Position, data));
            var ack = Channel.ReceiveMessage<FileAckMessage>();
            ack.ThrowOnError();
        }

        // TODO: explore making waiting for acknowledgement optional to increase throughput,
        // TODO: then receiving errors asynchronously and throwing during the next op
        internal FileStreamProxy(string filename, MessageChannel channel, Action<string>? onDispose = null)
        {
            Name = filename;
            Channel = channel;
            OnDispose = onDispose;
        }

        /// <summary>
        /// A finalizer is necessary when inheriting from Stream.
        /// </summary>
        ~FileStreamProxy()
        {
            Dispose(false);
        }

        /// <summary>
        /// Override of Stream.Dispose.
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            // inform the remote side the file connection is closing
            Channel.Send(new FileCloseMessage(Name));

            base.Dispose(disposing);
            if (disposing)
            {
                // dispose any managed objects
            }

            OnDispose?.Invoke(this.Name);
            OnDispose = null;

            GC.SuppressFinalize(this);
        }
    }
}
