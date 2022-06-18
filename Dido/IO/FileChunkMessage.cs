using System;
using System.IO;

namespace DidoNet.IO
{
    internal class FileChunkMessage : FileAckMessage
    {
        public byte[] Bytes { get; private set; } = new byte[0];

        /// <summary>
        /// Indicates this chunk represents the last chunk for the file
        /// (i.e. the position equals the length).
        /// </summary>
        public bool EOF { get { return Position == Length; } }

        public FileChunkMessage() { }
        
        /// <summary>
        /// Create a new file chunk message object of a degenerate chunk
        /// indicating the existing destination file is the same as the source,
        /// and no further chunks will be sent.
        /// </summary>
        /// <param name="filename"></param>
        public FileChunkMessage(string filename)
            : base(filename, -1, -1) { }

        public FileChunkMessage(string filename, byte[] bytes, long position, long length)
            : base(filename, position, length)
        {
            Bytes = bytes;
        }

        public FileChunkMessage(string filename, Exception exception)
            : base(filename, exception) { }

        public override void Read(Stream stream)
        {
            base.Read(stream);
            Bytes = stream.ReadByteArray();
        }

        public override void Write(Stream stream)
        {
            base.Write(stream);
            stream.WriteByteArray(Bytes);
        }
    }
}
