using System;

namespace DidoNet.IO
{
    internal class FileStartStoreMessage : FileTransferMessageBase
    {
        public FileStartStoreMessage() { }

        public FileStartStoreMessage(string filename, string channelId,
            long? length = null, DateTime? modifiedUtc = null, byte[]? hash = null)
            : base(filename, channelId, length, modifiedUtc, hash) { }
    }
}
