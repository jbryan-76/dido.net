using System;

namespace DidoNet.IO
{
    internal class FileStartCacheMessage : FileTransferMessageBase
    {
        public FileStartCacheMessage() { }

        public FileStartCacheMessage(string filename, ushort channelNumber,
            long? length = null, DateTime? modifiedUtc = null, byte[]? hash = null)
            : base(filename, channelNumber, length, modifiedUtc, hash) { }
    }
}
