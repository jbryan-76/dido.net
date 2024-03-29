﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DidoNet.IO
{
    /// <summary>
    /// Provides a limited replica API for System.IO.File which is implemented over a network
    /// connection from the ExecutionContext of a Runner to a remote application.
    /// </summary>
    public class RunnerFileProxy
    {
        /// <summary>
        /// The dedicated message channel marshaling file IO requests by the executing expression on a
        /// Runner to the remote application.
        /// </summary>
        internal MessageChannel? Channel { get; set; }

        /// <summary>
        /// The path on the runner file-system where files are cached, or null/empty if caching is disabled.
        /// </summary>
        internal string? CachePath { get; set; }

        /// <summary>
        /// The maximum age for a cached file before it is deleted or replaced.
        /// A timespan less than or equal to zero indicates cached files never expire.
        /// </summary>
        internal TimeSpan CacheMaxAge { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// The set of currently open files managed by this instance, keyed to the file path.
        /// </summary>
        private ConcurrentDictionary<string, RunnerFileStreamProxy> Files = new ConcurrentDictionary<string, RunnerFileStreamProxy>();

        /// <summary>
        /// Create a new instance to proxy file IO over a connection.
        /// If a connection is not provided, the class instance API will pass-through to the local file-system.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="configuration"></param>
        /// <param name="applicationId"></param>
        internal RunnerFileProxy(Connection? connection, RunnerConfiguration? configuration = null, string? applicationId = null)
        {
            CachePath = configuration?.FileCachePath;

            // update the cache path where necessary to ensure each application has its own
            // folder to help prevent filename collisions
            if (!string.IsNullOrEmpty(CachePath) && !string.IsNullOrEmpty(applicationId))
            {
                CachePath = Path.Combine(CachePath, applicationId);
            }

            // ensure the cache folder exists, if necessary
            if (!string.IsNullOrEmpty(CachePath) && !Directory.Exists(CachePath))
            {
                Directory.CreateDirectory(CachePath);
            }

            CacheMaxAge = configuration?.CacheMaxAge ?? TimeSpan.Zero;
            if (connection != null)
            {
                Channel = new MessageChannel(connection, Constants.AppRunner_FileChannelId);
            }
        }

        /// <summary>
        /// Copies (or overwrites as needed) the file from the provided source path (with respect to the application's local
        /// file-system) to the provided destination path (with respect to the runner's local file-system).
        /// <para/>Existing files are not updated if they are heuristically determined to be the same as the source file.
        /// Nominally this is done by comparing the source and destination file lengths and last write time, but this
        /// comparison can be made more robust by setting the 'checksum' parameter to 'true' to additionally incorporate
        /// an MD5 hash into the heuristic.
        /// <para/>Note: the destination path MUST be a relative (non-rooted) path, which will be combined with
        /// the runner's configured location for cached files, to create the final returned absolute (rooted) path 
        /// referencing the cached file.
        /// </summary>
        /// <param name="srcPath"></param>
        /// <param name="dstPath"></param>
        /// <param name="checksum"></param>
        /// <returns>The resulting final path of the cached destination file.</returns>
        /// <exception cref="IOException"></exception>
        public async Task<string> CacheAsync(string srcPath, string dstPath, bool checksum = false)
        {
            if (string.IsNullOrEmpty(CachePath))
            {
                throw new InvalidOperationException($"File caching is not enabled. To enable, ensure the '{nameof(RunnerConfiguration)}.{nameof(RunnerConfiguration.CachePath)}' property is set to a valid value.");
            }

            if (Path.IsPathRooted(dstPath))
            {
                throw new IOException($"Destination path '{dstPath}' must be a relative (not rooted) path.");
            }

            // get the destination path for the cached file
            var dstCachedPath = GetCachedPath(dstPath);

            if (Channel == null)
            {
                // when running locally, just short-circuit and copy the file
                File.Copy(srcPath, dstCachedPath, true);
            }
            else
            {
                string channelId = Guid.NewGuid().ToString();

                // if the cached file already exists, get its info and send with the start request
                // (if the files are the same, the application will send back a degenerate chunk message
                // instead of sending the whole file)
                if (File.Exists(dstCachedPath))
                {
                    var info = new FileInfo(dstCachedPath);

                    // if the cached file is expired, force a new copy to get transferred
                    if (CacheMaxAge > TimeSpan.Zero &&
                        DateTime.UtcNow - File.GetLastWriteTimeUtc(dstCachedPath) > CacheMaxAge)
                    {
                        File.Delete(dstCachedPath);
                        Channel.Send(new FileStartCacheMessage(srcPath, channelId));
                    }
                    else
                    {
                        // otherwise send the current file info to the application to decide
                        // whether to transmit the file
                        byte[]? hash = null;
                        if (checksum)
                        {
                            using (var md5 = System.Security.Cryptography.MD5.Create())
                            using (var stream = File.OpenRead(dstCachedPath))
                            {
                                hash = md5.ComputeHash(stream);
                            }
                        }
                        Channel.Send(new FileStartCacheMessage(srcPath, channelId, info.Length, info.LastWriteTimeUtc, hash));
                    }
                }
                else
                {
                    Channel.Send(new FileStartCacheMessage(srcPath, channelId));
                }

                // on a successful open, both sides agreed to create a new dedicated channel for
                // IO of the indicated file using the acquired channel number
                using (var fileChannel = new MessageChannel(Channel.Connection, channelId))
                {
                    // receive a corresponding message containing the details of the requested file
                    var info = fileChannel.ReceiveMessage<FileInfoMessage>();
                    info.ThrowOnError();

                    // handle the degenerate case of an empty file
                    if (info.Length == 0)
                    {
                        File.WriteAllBytes(dstCachedPath, new byte[0]);
                    }
                    else
                    {
                        // otherwise loop and receive the entire file in chunks
                        FileStream? file = null;
                        FileChunkMessage chunk;
                        do
                        {
                            chunk = fileChannel.ReceiveMessage<FileChunkMessage>();
                            chunk.ThrowOnError();

                            if (chunk.Length >= 0)
                            {
                                // don't open the file until absolutely necessary:
                                // if the identical file is already cached, a degenerate chunk will be received
                                // and the file should not be overwritten
                                if (file == null)
                                {
                                    // make sure the directory exists
                                    Directory.CreateDirectory(Path.GetDirectoryName(dstCachedPath)!);
                                    file = File.Open(dstCachedPath, FileMode.Create, FileAccess.Write);
                                }
                                file.Write(chunk.Bytes);
                            }
                        } while (!chunk.EOF);

                        file?.Dispose();
                    }

                    // now force the cached last write time to match the source
                    // to support the cache heuristic for determining identical files
                    File.SetLastWriteTimeUtc(dstCachedPath, info.LastWriteTimeUtc);
                }
            }
            return dstCachedPath;
        }

        /// <summary>
        /// Copies (or overwrites as needed) the file from the provided source path (with respect to the runner's local file-system)
        /// to the provided destination path (with respect to the application's local file-system).
        /// <para/>Existing files are not updated if they are heuristically determined to be the same as the source file.
        /// Nominally this is done by comparing the source and destination file lengths and last write time, but this
        /// comparison can be made more robust by setting the 'checksum' parameter to 'true' to additionally incorporate
        /// an MD5 hash into the heuristic.
        /// <para/>Note: the source path MUST be a relative (non-rooted) path, which will be combined with
        /// the runner's configured location for cached files, to create the final returned absolute (rooted) path 
        /// referencing the application file.
        /// </summary>
        /// <param name="srcPath"></param>
        /// <param name="dstPath"></param>
        /// <exception cref="IOException"></exception>
        public async Task StoreAsync(string srcPath, string dstPath, bool checksum = false)
        {
            // if the source file doesn't exist, throw an error
            if (!File.Exists(srcPath))
            {
                throw new FileNotFoundException(null, srcPath);
            }

            if (Channel == null)
            {
                // when running locally, just short-circuit and copy the file
                File.Copy(srcPath, dstPath, true);
            }
            else
            {
                string channelId = Guid.NewGuid().ToString();

                // get the file info and send with the start request
                var fileInfo = new FileInfo(srcPath);
                byte[] hash = new byte[0];
                if (checksum)
                {
                    using (var md5 = System.Security.Cryptography.MD5.Create())
                    using (var stream = File.OpenRead(srcPath))
                    {
                        hash = md5.ComputeHash(stream);
                    }
                }
                var storeMessage = new FileStartStoreMessage(dstPath, channelId, fileInfo.Length, fileInfo.LastWriteTimeUtc, hash);
                Channel.Send(storeMessage);

                // on a successful open, both sides agreed to create a new dedicated channel for
                // IO of the indicated file using the acquired channel number
                using (var fileChannel = new MessageChannel(Channel.Connection, channelId))
                {
                    // receive a corresponding message containing the details of the requested file
                    var info = fileChannel.ReceiveMessage<FileInfoMessage>();
                    info.ThrowOnError();

                    // handle the degenerate case of an empty file
                    if (fileInfo.Length == 0)
                    {
                        fileChannel.Send(new FileChunkMessage(dstPath, new byte[0], 0, 0));
                    }
                    else
                    {
                        // otherwise if the file already exists, the application will have sent back its info.
                        // check whether the content changed
                        bool same = Enumerable.SequenceEqual(hash, info.Hash)
                            && fileInfo.Length == info.Length
                            && fileInfo.LastWriteTimeUtc == info.LastWriteTimeUtc;
                        if (same)
                        {
                            // if the destination file hasn't changed, send a degenerate chunk
                            fileChannel.Send(new FileChunkMessage(dstPath));
                        }
                        else
                        {
                            // otherwise send the file in chunks
                            using (var file = File.Open(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
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
                                    fileChannel.Send(new FileChunkMessage(dstPath, data, file.Position, file.Length));
                                    remaining -= read;
                                }
                            }
                        }

                        // receive confirmation
                        var ack = fileChannel.ReceiveMessage<FileAckMessage>();
                        ack.ThrowOnError();
                    }
                }
            }
        }

        /// <summary>
        /// Combines the provided relative path with the runner's configured path for cached files on its local
        /// file-system to create a final absolute (rooted) path referencing a cached file.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        /// <exception cref="IOException"></exception>
        public string GetCachedPath(string relativePath)
        {
            if (string.IsNullOrEmpty(CachePath))
            {
                throw new InvalidOperationException($"File caching is not enabled. To enable, ensure the '{nameof(RunnerConfiguration)}.{nameof(RunnerConfiguration.CachePath)}' property is set to a valid value.");
            }
            // only relative paths are supported
            if (Path.IsPathRooted(relativePath))
            {
                throw new IOException($"Path '{relativePath}' must be a relative (not rooted) path.");
            }
            if (relativePath.Contains(".."))
            {
                throw new IOException($"Path '{relativePath}' must be a strict relative path, and not contain any parent directory (../) notations.");
            }
            return Path.Combine(CachePath, relativePath);
        }

        /// <summary>
        /// Appends lines to a file, and then closes the file. If the specified file does
        /// not exist, this method creates a file, writes the specified lines to the file,
        /// and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        public void AppendAllLines(string path, IEnumerable<string> contents)
        {
            AppendAllLines(path, contents, new UTF8Encoding(false));
        }

        /// <summary>
        /// Appends lines to a file by using a specified encoding, and then closes the file.
        /// If the specified file does not exist, this method creates a file, writes the
        /// specified lines to the file, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        /// <param name="encoding"></param>
        public void AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding)
        {
            if (Channel != null)
            {
                Channel.Send(new FileAtomicWriteMessage(path, contents, append: true, encoding));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
            }
            else
            {
                File.AppendAllLines(path, contents, encoding);
            }
        }

        /// <summary>
        /// Opens a file, appends the specified string to the file, and then closes the file.
        /// If the file does not exist, this method creates a file, writes the specified
        /// string to the file, then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        public void AppendAllText(string path, string contents)
        {
            AppendAllText(path, contents, new UTF8Encoding(false));
        }

        /// <summary>
        /// Appends the specified string to the file using the specified encoding, creating
        /// the file if it does not already exist.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        /// <param name="encoding"></param>
        public void AppendAllText(string path, string contents, Encoding encoding)
        {
            if (Channel != null)
            {
                Channel.Send(new FileAtomicWriteMessage(path, contents, append: true, encoding));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
            }
            else
            {
                File.AppendAllText(path, contents, encoding);
            }
        }

        /// <summary>
        /// Creates a System.IO.StreamWriter that appends UTF-8 encoded text to an existing
        /// file, or to a new file if the specified file does not exist.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public StreamWriter AppendText(string path)
        {
            if (Channel != null)
            {
                return new StreamWriter(Open(path, FileMode.Append), new UTF8Encoding(false));
            }
            else
            {
                return File.AppendText(path);
            }
        }

        // void Copy(string sourceFileName, string destFileName)
        // void Copy(string sourceFileName, string destFileName, bool overwrite)

        /// <summary>
        /// Creates or overwrites a file in the specified path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Stream Create(string path)
        {
            return Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        }

        /// <summary>
        /// Creates or overwrites a file in the specified path.
        /// <para/>NOTE: bufferSize is not used due to the underlying proxied IO over a network connection.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="bufferSize"></param>
        /// <returns></returns>
        public Stream Create(string path, int bufferSize)
        {
            return Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        }

        //Stream Create(string path, int bufferSize, FileOptions options)

        /// <summary>
        /// Creates or opens a file for writing UTF-8 encoded text. If the file already exists,
        /// its contents are overwritten.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public StreamWriter CreateText(string path)
        {
            return new StreamWriter(Create(path));
        }

        // void Delete(string path)
        // bool Exists([NotNullWhen(true)] string? path)

        // FileAttributes GetAttributes(string path)
        // DateTime GetCreationTime(string path)
        // DateTime GetCreationTimeUtc(string path)
        // DateTime GetLastAccessTime(string path)
        // DateTime GetLastAccessTimeUtc(string path)
        // DateTime GetLastWriteTime(string path)
        // DateTime GetLastWriteTimeUtc(string path)

        // void Move(string sourceFileName, string destFileName)
        // void Move(string sourceFileName, string destFileName, bool overwrite)

        /// <summary>
        /// Opens a file stream on the specified path with read/write access with no sharing.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public Stream Open(string path, FileMode mode)
        {
            return Open(path, mode, (mode == FileMode.Append ? FileAccess.Write : FileAccess.ReadWrite), FileShare.None);
        }

        /// <summary>
        /// Opens a file stream on the specified path, with the specified mode and
        /// access with no sharing.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <param name="access"></param>
        /// <returns></returns>
        public Stream Open(string path, FileMode mode, FileAccess access)
        {
            return Open(path, mode, access, FileShare.None);
        }

        /// <summary>
        /// Opens a file stream on the specified path, having the specified mode
        /// with read, write, or read/write access and the specified sharing option.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <param name="access"></param>
        /// <param name="share"></param>
        /// <returns></returns>
        /// <exception cref="ConcurrencyException"></exception>
        public Stream Open(string path, FileMode mode, FileAccess access, FileShare share)
        {
            if (Channel != null)
            {
                // enter a critical section: 
                // for a given connection, only one thread can attempt to open a file at a time
                lock (Channel)
                {
                    // confirm another thread has not already opened the file or failed to close it
                    if (Files.ContainsKey(path))
                    {
                        throw new ConcurrencyException($"File '{path}' was opened or created by another thread and is not yet closed.");
                    }

                    // opening a file: create a dedicated channel
                    string channelId = Guid.NewGuid().ToString();

                    // send the open file request and wait for the corresponding response.
                    // because this code block has a lock on the connection, the request/response
                    // pair will be correlated and not interleaved with another thread's attempt
                    // to open or create a file
                    Channel.Send(new FileOpenMessage(path, channelId, mode, access, share));
                    var ack = Channel.ReceiveMessage<FileAckMessage>();
                    ack.ThrowOnError();

                    // on a successful open, both sides agreed to create a new dedicated channel for
                    // IO of the indicated file using the acquired channel number
                    var fileChannel = new MessageChannel(Channel.Connection, channelId);
                    var stream = new RunnerFileStreamProxy(path, ack.Position, ack.Length, fileChannel, (filename) => Close(filename));
                    Files.TryAdd(path, stream);
                    return stream;
                }
            }
            else
            {
                return File.Open(path, mode, access, share);
            }
        }

        /// <summary>
        /// Opens an existing file for reading.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Stream OpenRead(string path)
        {
            return Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        /// <summary>
        /// Opens an existing UTF-8 encoded text file for reading.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public StreamReader OpenText(string path)
        {
            return new StreamReader(OpenRead(path));
        }

        /// <summary>
        /// Opens an existing file or creates a new file for writing.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Stream OpenWrite(string path)
        {
            return Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
        }

        /// <summary>
        /// Opens a binary file, reads the contents of the file into a byte array, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="IOException"></exception>
        public byte[] ReadAllBytes(string path)
        {
            using (var stream = OpenRead(path))
            {
                if (stream.Length > Int32.MaxValue)
                {
                    throw new IOException("File length is greater than the 2GB limit.");
                }
                return stream.ReadBytes((int)stream.Length);
            }
        }

        /// <summary>
        /// Opens a text file, reads all lines of the file, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public string[] ReadAllLines(string path)
        {
            return ReadAllLines(path, Encoding.UTF8);
        }

        /// <summary>
        /// Opens a file, reads all lines of the file with the specified encoding, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public string[] ReadAllLines(string path, Encoding encoding)
        {
            using (var reader = new StreamReader(new BufferedStream(OpenRead(path)), encoding))
            {
                // implementation copied from .NET mscorlib
                string? line;
                var lines = new List<string>();
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
                return lines.ToArray();
            }
        }

        /// <summary>
        /// Opens a text file, reads all the text in the file, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public string ReadAllText(string path)
        {
            return ReadAllText(path, Encoding.UTF8);
        }

        /// <summary>
        /// Opens a file, reads all text in the file with the specified encoding, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public string ReadAllText(string path, Encoding encoding)
        {
            using (var reader = new StreamReader(OpenRead(path), encoding))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Reads the lines of a file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public IEnumerable<string> ReadLines(string path)
        {
            return ReadLines(path, Encoding.UTF8);
        }

        /// <summary>
        /// Read the lines of a file that has a specified encoding.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public IEnumerable<string> ReadLines(string path, Encoding encoding)
        {
            using (var reader = new StreamReader(new BufferedStream(OpenRead(path)), encoding))
            {
                for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    yield return line;
                }
            }
        }

        // void SetAttributes(string path, FileAttributes fileAttributes)
        // void SetCreationTime(string path, DateTime creationTime)
        // void SetCreationTimeUtc(string path, DateTime creationTimeUtc)
        // void SetLastAccessTime(string path, DateTime lastAccessTime)
        // void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)
        // void SetLastWriteTime(string path, DateTime lastWriteTime)
        // void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)

        /// <summary>
        /// Creates a new file, writes the specified byte array to the file, and then closes
        /// the file. If the target file already exists, it is overwritten.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="bytes"></param>
        public void WriteAllBytes(string path, byte[] bytes)
        {
            if (Channel != null)
            {
                Channel.Send(new FileAtomicWriteMessage(path, bytes, append: true));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
            }
            else
            {
                File.WriteAllBytes(path, bytes);
            }
        }

        /// <summary>
        /// Creates a new file, writes a collection of strings to the file, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        public void WriteAllLines(string path, IEnumerable<string> contents)
        {
            WriteAllLines(path, contents, new UTF8Encoding(false));
        }

        /// <summary>
        /// Creates a new file by using the specified encoding, writes a collection of strings
        /// to the file, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        /// <param name="encoding"></param>
        public void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding)
        {
            if (Channel != null)
            {
                Channel.Send(new FileAtomicWriteMessage(path, contents, append: false, encoding));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
            }
            else
            {
                File.WriteAllLines(path, contents, encoding);
            }
        }

        /// <summary>
        /// Creates a new file, write the specified string array to the file, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        public void WriteAllLines(string path, string[] contents)
        {
            WriteAllLines(path, contents, new UTF8Encoding(false));
        }

        /// <summary>
        /// Creates a new file, writes the specified string array to the file by using the
        /// specified encoding, and then closes the file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        /// <param name="encoding"></param>
        public void WriteAllLines(string path, string[] contents, Encoding encoding)
        {
            if (Channel != null)
            {
                Channel.Send(new FileAtomicWriteMessage(path, contents, append: false, encoding));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
            }
            else
            {
                File.WriteAllLines(path, contents, encoding);
            }
        }

        /// <summary>
        /// Creates a new file, writes the specified string to the file, and then closes
        /// the file. If the target file already exists, it is overwritten.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        public void WriteAllText(string path, string contents)
        {
            WriteAllText(path, contents, new UTF8Encoding(false));
        }

        /// <summary>
        /// Creates a new file, writes the specified string to the file using the specified
        /// encoding, and then closes the file. If the target file already exists, it is
        /// overwritten.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        /// <param name="encoding"></param>
        public void WriteAllText(string path, string contents, Encoding encoding)
        {
            if (Channel != null)
            {
                Channel.Send(new FileAtomicWriteMessage(path, contents, append: false, encoding));
                var ack = Channel.ReceiveMessage<FileAckMessage>();
                ack.ThrowOnError();
            }
            else
            {
                File.WriteAllText(path, contents, encoding);
            }
        }

        /// <summary>
        /// Closes the indicated file.
        /// </summary>
        /// <param name="path"></param>
        private void Close(string path)
        {
            if (Files.TryRemove(path, out var stream))
            {
                // inform the remote side the file connection is closing
                Channel?.Send(new FileCloseMessage(path));
            }
        }
    }
}
