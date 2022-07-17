using System;

namespace DidoNet.IO
{
    internal class FileStartCacheMessage : FileTransferMessageBase
    {
        public FileStartCacheMessage() { }

        public FileStartCacheMessage(string filename, string channelId,
            long? length = null, DateTime? modifiedUtc = null, byte[]? hash = null)
            : base(filename, channelId, length, modifiedUtc, hash) { }
    }
}
