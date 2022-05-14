namespace DidoNet.IO
{
    internal class FileMessageBase : IMessage
    {
        public string Filename { get; set; }

        public FileMessageBase(string filename)
        {
            Filename = filename;
        }

        public virtual void Read(Stream stream)
        {
            Filename = stream.ReadString();
        }

        public virtual void Write(Stream stream)
        {
            stream.WriteString(Filename);
        }
    }

    //internal class FileCreateMessage : FileMessageBase
    //{
    //}

    internal class FileOpenMessage : FileMessageBase
    {
        public FileMode Mode { get; set; }

        public FileAccess Access { get; set; }

        public FileShare Share { get; set; }

        public FileOpenMessage(string filename, FileMode mode, FileAccess access, FileShare share)
            : base(filename)
        {
            Mode = mode;
            Access = access;
            Share = share;
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            Mode = Enum.Parse<FileMode>(stream.ReadString());
            Access = Enum.Parse<FileAccess>(stream.ReadString());
            Share = Enum.Parse<FileShare>(stream.ReadString());
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteString(Mode.ToString());
            stream.WriteString(Access.ToString());
            stream.WriteString(Share.ToString());
        }
    }

    //internal class FileSeekMessage : FileMessageBase
    //{
    //    public long Offset { get; set; }

    //    public SeekOrigin Origin { get; set; }

    //    public FileSeekMessage(string filename, long offset, SeekOrigin origin)
    //        : base(filename)
    //    {
    //        Offset = offset;
    //        Origin = origin;
    //    }

    //    public override void Read(Stream stream)
    //    {
    //        base.Read(stream);
    //        Offset = stream.ReadInt64BE();
    //        Origin = Enum.Parse<SeekOrigin>(stream.ReadString());
    //    }

    //    public override void Write(Stream stream)
    //    {
    //        base.Write(stream);
    //        stream.WriteInt64BE(Offset);
    //        stream.WriteString(Origin.ToString());
    //    }
    //}

    internal class FileSetLengthMessage : FileMessageBase
    {
        public long Length { get; set; }

        public FileSetLengthMessage(string filename, long length)
            : base(filename)
        {
            Length = length;
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            Length = stream.ReadInt64BE();
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteInt64BE(Length);
        }
    }

    internal class FileReadRequestMessage : FileMessageBase
    {
        public long Position { get; set; }

        public int Count { get; set; }

        public FileReadRequestMessage(string filename, long position, int count)
            : base(filename)
        {
            Position = position;
            Count = count;
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            Position = stream.ReadInt64BE();
            Count = stream.ReadInt32BE();
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteInt64BE(Position);
            stream.WriteInt32BE(Count);
        }
    }

    internal class FileReadResponseMessage : FileAckMessage
    {
        public byte[] Bytes { get; private set; } = new byte[0];

        public FileReadResponseMessage(string filename, byte[] bytes)
            : base(filename, null)
        {
            Bytes = bytes;
        }

        public FileReadResponseMessage(string filename, Exception exception)
            : base(filename, exception) { }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            int length = stream.ReadInt32BE();
            Bytes = stream.ReadBytes(length);
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteInt32BE(Bytes?.Length ?? 0);
            stream.Write(Bytes);
        }
    }

    internal class FileWriteMessage : FileMessageBase
    {
        public long Position { get; set; }

        public byte[] Bytes { get; private set; } = new byte[0];

        public FileWriteMessage(string filename, long position, byte[] bytes)
            : base(filename)
        {
            Position = position;
            Bytes = bytes;
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            Position = stream.ReadInt64BE();
            int length = stream.ReadInt32BE();
            Bytes = stream.ReadBytes(length);
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteInt64BE(Position);
            stream.WriteInt32BE(Bytes?.Length ?? 0);
            stream.Write(Bytes);
        }
    }

    internal class FileCloseMessage : FileMessageBase
    {
        public FileCloseMessage(string filename)
            : base(filename) { }
    }

    internal class FileAckMessage : FileMessageBase
    {
        public string ExceptionType { get; set; } = "";

        public string ExceptionMessage { get; set; } = "";

        public bool IsOk { get { return string.IsNullOrEmpty(ExceptionType); } }

        public Exception? Exception
        {
            get
            {
                return IsOk ? null : Activator.CreateInstance(Type.GetType(ExceptionType), ExceptionMessage) as Exception;
            }
        }

        public FileAckMessage(string filename, Exception? exception)
            : base(filename)
        {
            if (exception != null)
            {
                ExceptionType = exception.GetType().FullName!;
                ExceptionMessage = exception.ToString();
            }
        }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            ExceptionType = stream.ReadString();
            ExceptionMessage = stream.ReadString();
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteString(ExceptionType);
            stream.WriteString(ExceptionMessage);
        }

        public void ThrowOnError()
        {
            if (!IsOk)
            {
                throw Exception!;
            }
        }
    }

    public class ProxyFileStream : Stream
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

        private long _length;

        private MessageChannel Channel;

        // TODO: explore making waiting for acknowledgement optional to increase throughput,
        // TODO: then receiving errors asynchronously and throwing during the next op
        internal ProxyFileStream(string filename, MessageChannel channel)
        {
            Name = filename;
            Channel = channel;
        }

        public override void Flush()
        {
        }

        // TODO: override ReadAsync to provided dedicated implementation?

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

        // TODO: override WriteAsync to provided dedicated implementation?

        public override void Write(byte[] buffer, int offset, int count)
        {
            var data = new byte[count];
            Buffer.BlockCopy(buffer, offset, data, 0, count);
            Channel.Send(new FileWriteMessage(Name, Position, data));
            var ack = Channel.ReceiveMessage<FileAckMessage>();
            ack.ThrowOnError();
        }

        /// <summary>
        /// A finalizer is necessary when inheriting from Stream.
        /// </summary>
        ~ProxyFileStream()
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
    }

    public class ProxyFile
    {
        //System.IO.File;
        //System.IO.FileStream;
        // append; copy; create; exists; delete; get info; move; open; read; write;

        // TODO: relative files only? only files that resolve as descendents from the root app folder?

        /// <summary>
        /// The channel to use for exchanging messages.
        /// </summary>
        internal MessageChannel? Channel { get; set; }

        /// <summary>
        /// Create a new instance to proxy file IO over a communications channel.
        /// If a channel is not provided, IO requests pass-through to the local filesystem.
        /// </summary>
        /// <param name="channel"></param>
        internal ProxyFile(MessageChannel? channel)
        {
            Channel = channel;
        }

        public Stream Open(string path, FileMode mode)
        {
            if (Channel != null)
            {
                Channel.Send(new FileOpenMessage(path, mode, FileAccess.ReadWrite, FileShare.None));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
                return new ProxyFileStream(path, Channel);
            }
            else
            {
                return File.Open(path, mode);
            }
        }

        public Stream Open(string path, FileMode mode, FileAccess access)
        {
            if (Channel != null)
            {
                Channel.Send(new FileOpenMessage(path, mode, access, FileShare.None));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
                return new ProxyFileStream(path, Channel);
            }
            else
            {
                return File.Open(path, mode, access);
            }
        }

        public Stream Open(string path, FileMode mode, FileAccess access, FileShare share)
        {
            if (Channel != null)
            {
                Channel.Send(new FileOpenMessage(path, mode, access, share));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
                return new ProxyFileStream(path, Channel);
            }
            else
            {
                return File.Open(path, mode, access, share);
            }
        }

        //public static FileStream Open(string path, FileStreamOptions options)
        //{
        //    throw new NotImplementedException();
        //}
    }

    public class ProxyDirectory
    {
        // System.IO.Directory;
        // create; delete; enumerate; exists; get info; enumerate files; 
        internal MessageChannel? Channel { get; set; }

        internal ProxyDirectory(MessageChannel? channel)
        {
            Channel = channel;
        }

    }
}
