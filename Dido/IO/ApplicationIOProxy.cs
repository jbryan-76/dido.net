﻿using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace DidoNet.IO
{
    /// <summary>
    /// Processes file and directory IO requests from a remotely executing expression, which is using 
    /// a RunnerFileProxy or RunnerDirectoryProxy instance in an ExecutionContext.
    /// </summary>
    internal class ApplicationIOProxy : MessageChannel
    {
        /// <summary>
        /// The readonly set of currently open files managed by this instance, keyed to the file path.
        /// </summary>
        public ReadOnlyDictionary<string, ApplicationFileStreamProxy> Files
        {
            get { return new ReadOnlyDictionary<string, ApplicationFileStreamProxy>(files); }
        }

        /// <summary>
        /// The set of currently open files managed by this instance, keyed to the file path.
        /// </summary>
        private ConcurrentDictionary<string, ApplicationFileStreamProxy> files = new ConcurrentDictionary<string, ApplicationFileStreamProxy>();

        /// <summary>
        /// Create a new io proxy to process file and directory IO requests.
        /// </summary>
        /// <param name="connection"></param>
        public ApplicationIOProxy(Connection connection)
            : base(connection, Constants.AppRunner_FileChannelId)
        {
            OnMessageReceived = FileMessageHandler;

            // TODO: capture the current working directory, and hold constant as a relative base path throughout object lifetime?
        }

        /// <summary>
        /// Processes received messages to perform file and directory IO operations for the remotely
        /// executing expression.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="channel"></param>
        /// <exception cref="InvalidOperationException"></exception>
        void FileMessageHandler(IMessage message, MessageChannel channel)
        {
            switch (message)
            {
                case FileOpenMessage open:
                    try
                    {
                        var openFile = new ApplicationFileStreamProxy(open, Channel.Connection);
                        files.TryAdd(open.Filename, openFile);
                        channel.Send(new FileAckMessage(open.Filename, openFile.Stream.Position, openFile.Stream.Length));
                    }
                    catch (Exception ex)
                    {
                        channel.Send(new FileAckMessage(open.Filename, ex));
                    }

                    break;

                case FileCloseMessage close:
                    if (files.TryGetValue(close.Filename, out var closeFile))
                    {
                        // dispose the file before removing it
                        closeFile.Dispose();
                        files.TryRemove(close.Filename, out _);
                    }
                    break;

                case FileAtomicWriteMessage write:
                    try
                    {
                        using (var file = File.Open(write.Filename, write.Append ? FileMode.Append : FileMode.Create))
                        {
                            file.Write(write.Bytes);
                            channel.Send(new FileAckMessage(write.Filename, file.Position, file.Length));
                        }
                    }
                    catch (Exception ex)
                    {
                        channel.Send(new FileAckMessage(write.Filename, ex));
                    }
                    break;

                case FileStartCacheMessage cache:
                    using (var cacheChannel = new MessageChannel(Channel.Connection, cache.ChannelNumber))
                    {
                        // ensure the file exists and send an error if not
                        if (!File.Exists(cache.Filename))
                        {
                            cacheChannel.Send(new FileChunkMessage(cache.Filename, new FileNotFoundException(null, cache.Filename)));
                        }
                        else
                        {
                            try
                            {
                                var fileInfo = new FileInfo(cache.Filename);

                                var info = new FileInfoMessage(cache.Filename, fileInfo.Length, fileInfo.CreationTimeUtc, fileInfo.LastAccessTimeUtc, fileInfo.LastWriteTimeUtc, new byte[0]);

                                // if hashing is requested, hash the file
                                if (cache.Hash.Length > 0)
                                {
                                    using (var md5 = System.Security.Cryptography.MD5.Create())
                                    using (var stream = File.OpenRead(cache.Filename))
                                    {
                                        info.Hash = md5.ComputeHash(stream);
                                    }
                                }

                                // send back the file info
                                cacheChannel.Send(info);

                                // only continue sending file chunks if the file is not empty
                                if (info.Length > 0)
                                {
                                    // if the file was already cached, check whether the content changed
                                    bool same = Enumerable.SequenceEqual(cache.Hash, info.Hash)
                                        && cache.Length == info.Length
                                        && cache.LastWriteTimeUtc == info.LastWriteTimeUtc;
                                    if (same)
                                    {
                                        // if the file is already cached and hasn't changed,
                                        // send a single empty chunk
                                        cacheChannel.Send(new FileChunkMessage(cache.Filename));
                                    }
                                    else
                                    {
                                        // otherwise send the file in chunks
                                        using (var file = File.Open(cache.Filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                                        {
                                            var buffer = new byte[512 * 1024]; // 0.5MB
                                            long remaining = file.Length;
                                            while (remaining > 0)
                                            {
                                                var count = (int)Math.Min(remaining, buffer.Length);
                                                int read = file.Read(buffer, 0, count);
                                                var data = buffer;
                                                // the message only sends a contiguous byte array containing all data,
                                                // so resize if needed (which should only happen on the last chunk)
                                                if (read != buffer.Length)
                                                {
                                                    data = new byte[read];
                                                    Buffer.BlockCopy(buffer, 0, data, 0, read);
                                                }
                                                cacheChannel.Send(new FileChunkMessage(cache.Filename, data, file.Position, file.Length));
                                                remaining -= read;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                cacheChannel.Send(new FileChunkMessage(cache.Filename, ex));
                            }
                        }
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unknown message type '{message.GetType()}'");
            }
        }
    }
}
