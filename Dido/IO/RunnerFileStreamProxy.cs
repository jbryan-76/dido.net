namespace DidoNet.IO
{
    /// <summary>
    /// Provides a virtualized file stream that marshals IO requests over a connection to 
    /// a remote application.
    /// </summary>
    public class RunnerFileStreamProxy : Stream
    {
        /// <summary>
        /// The path and filename of the file.
        /// </summary>
        public string Name { get; private set; }

        public override long Position { get; set; }

        public override long Length { get { return _length; } }

        /// <summary>
        /// Indicates the stream can read. Always true.
        /// </summary>
        public override bool CanRead => true;

        /// <summary>
        /// Indicates the stream can seek. Always true.
        /// </summary>
        public override bool CanSeek => true;

        /// <summary>
        /// Indicates the stream can write. Always true.
        /// </summary>
        public override bool CanWrite => true;

        /// <summary>
        /// The message channel used to communicate with the remote application.
        /// </summary>
        internal MessageChannel Channel { get; set; }

        private long _length;

        private Action<string>? OnDispose = null;

        public override void Flush()
        {
        }

        // TODO: override ReadAsync to provide dedicated implementation?

        public override int Read(byte[] buffer, int offset, int count)
        {
            // TODO: optimize this: break up and loop send+receive based on a max message size.
            // TODO: explore decoupling the send and receive loops so they can run concurrently. producer-consumer?
            return ReadInternal(buffer, offset, count);
        }

        private int ReadInternal(byte[] buffer, int offset, int count)
        {
            Channel.Send(new FileReadRequestMessage(Name, Position, count));
            var response = Channel.ReceiveMessage<FileReadResponseMessage>();
            response.ThrowOnError();
            Buffer.BlockCopy(response.Bytes, 0, buffer, offset, response.Bytes.Length);
            _length = response.Length;
            Position = response.Position;
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
                    newPosition = Length + offset;
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
            _length = ack.Length;
            Position = ack.Position;
        }

        // TODO: override WriteAsync to provide dedicated implementation?

        public override void Write(byte[] buffer, int offset, int count)
        {
            // TODO: optimize this: break up and loop send+receive based on a max message size.
            // TODO: explore decoupling the send and receive loops so they can run concurrently. producer-consumer?
            WriteInternal(buffer, offset, count);
        }

        private void WriteInternal(byte[] buffer, int offset, int count)
        {
            var data = new byte[count];
            Buffer.BlockCopy(buffer, offset, data, 0, count);
            Channel.Send(new FileWriteMessage(Name, Position, data));
            var ack = Channel.ReceiveMessage<FileAckMessage>();
            ack.ThrowOnError();
            _length = ack.Length;
            Position = ack.Position;
        }

        // TODO: explore making waiting for acknowledgment optional to increase throughput,
        // TODO: then receiving errors asynchronously and throwing during the next op

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="position"></param>
        /// <param name="length"></param>
        /// <param name="channel"></param>
        /// <param name="onDispose"></param>
        internal RunnerFileStreamProxy(string filename, long position, long length, MessageChannel channel, Action<string>? onDispose = null)
        {
            Position = position;
            _length = length;
            Name = filename;
            Channel = channel;
            OnDispose = onDispose;
        }

        /// <summary>
        /// A finalizer is necessary when inheriting from Stream.
        /// </summary>
        ~RunnerFileStreamProxy()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            OnDispose?.Invoke(this.Name);
            OnDispose = null;

            base.Dispose(disposing);

            if (disposing)
            {
                // dispose any managed objects
                Channel?.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
