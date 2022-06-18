using System;

namespace DidoNet.IO
{
    internal class FileStartStoreMessage : FileTransferMessageBase
    {
        public FileStartStoreMessage() { }

        public FileStartStoreMessage(string filename, ushort channelNumber,
            long? length = null, DateTime? modifiedUtc = null, byte[]? hash = null)
            : base(filename, channelNumber, length, modifiedUtc, hash) { }
    }
}
