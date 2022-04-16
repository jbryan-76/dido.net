using System;
using System.Linq;
using System.Threading;
using Xunit;

namespace DidoNet.Test
{
    public class QueueBufferStreamTests
    {
        byte[] GenerateRandomData(int length)
        {
            var data = new byte[length];
            var rand = new System.Random();
            rand.NextBytes(data);
            return data;
        }

        /// <summary>
        /// Verify normal reading and writing.
        /// </summary>
        [Fact]
        public void ReadAndWrite()
        {
            using (var stream = new QueueBufferStream(false))
            {
                var data = GenerateRandomData(1024);
                var buffer = new byte[256];

                // confirm initial state
                Assert.True(stream.IsEmpty);
                Assert.Equal(0, stream.Length);
                Assert.Equal(0, stream.Position);

                // confirm can't read if empty
                var read = stream.Read(buffer, 0, buffer.Length);
                Assert.Equal(0, read);

                // write a little data and confirm
                stream.Write(data, 0, 100);
                Assert.False(stream.IsEmpty);
                Assert.Equal(100, stream.Length);
                Assert.Equal(0, stream.Position);

                // confirm can't read if not enough data
                read = stream.Read(buffer, 0, buffer.Length);
                Assert.Equal(0, read);

                // write a little more and confirm
                stream.Write(data, 100, 155);
                Assert.False(stream.IsEmpty);
                Assert.Equal(255, stream.Length);
                Assert.Equal(0, stream.Position);

                // confirm still can't read if not enough data
                read = stream.Read(buffer, 0, buffer.Length);
                Assert.Equal(0, read);

                // write one more byte to push over the limit and confirm
                stream.Write(data, 255, 1);
                Assert.False(stream.IsEmpty);
                Assert.Equal(256, stream.Length);
                Assert.Equal(0, stream.Position);

                // confirm can read now and the content matches
                read = stream.Read(buffer, 0, buffer.Length);
                Assert.Equal(256, read);
                Assert.False(stream.IsEmpty);
                Assert.Equal(256, stream.Length);
                Assert.Equal(256, stream.Position);
                Enumerable.SequenceEqual(data.Take(buffer.Length), buffer);

                // write some more data and confirm
                stream.Write(data, 256, 200);
                Assert.False(stream.IsEmpty);
                Assert.Equal(456, stream.Length);
                Assert.Equal(256, stream.Position);

                // truncate and confirm the already-read segments were removed
                // (which changes the position and length)
                stream.Truncate();
                Assert.False(stream.IsEmpty);
                Assert.Equal(200, stream.Length);
                Assert.Equal(0, stream.Position);

                // read the entire last segment and confirm
                read = stream.Read(buffer, 0, 200);
                Assert.Equal(200, read);
                Assert.False(stream.IsEmpty);
                Assert.Equal(200, stream.Length);
                Assert.Equal(200, stream.Position);
                Enumerable.SequenceEqual(data.Take(200), buffer.Take(200));

                // truncate and confirm the stream is empty again
                stream.Truncate();
                Assert.True(stream.IsEmpty);
                Assert.Equal(0, stream.Length);
                Assert.Equal(0, stream.Position);
            }
        }

        /// <summary>
        /// Verify the auto-truncate behavior works as expected
        /// </summary>
        [Fact]
        public void AutoTruncate()
        {
            using (var stream = new QueueBufferStream(true))
            {
                var data = GenerateRandomData(1024);
                var buffer = new byte[256];

                // write two chunks of data
                stream.Write(data, 0, 100);
                stream.Write(data, 100, 57);
                Assert.Equal(157, stream.Length);
                Assert.Equal(0, stream.Position);

                // read part of the first chunk and confirm
                var read = stream.Read(buffer, 0, 90);
                Assert.Equal(90, read);
                Assert.Equal(157, stream.Length);
                Assert.Equal(90, stream.Position);
                Enumerable.SequenceEqual(data.Take(90), buffer.Take(90));

                // read another part that overlaps the boundary between
                // the first and second chunk and confirm
                read = stream.Read(buffer, 0, 50);
                Assert.Equal(50, read);
                Assert.Equal(57, stream.Length); // only the first chunk was consumed, leaving the second
                Assert.Equal(40, stream.Position);
                Enumerable.SequenceEqual(data.Skip(90).Take(50), buffer.Take(50));
            }
        }

        /// <summary>
        /// Verify the stream is thread-safe with a reader and writer operating
        /// in separate threads.
        /// </summary>
        [Fact]
        public void ProducerConsumer()
        {
            ProducerConsumer_Implementation(false);
            ProducerConsumer_Implementation(true);
        }

        private void ProducerConsumer_Implementation(bool autoTruncate)
        {
            var dataToWrite = GenerateRandomData(16 * 1024);
            var dataToRead = new byte[dataToWrite.Length];
            using (var stream = new QueueBufferStream(autoTruncate))
            {
                var writeThread = new Thread(() =>
                {
                    // write the data in randomly sized chunks at random intervals
                    var rand = new Random();
                    int position = 0;
                    while (position < dataToWrite.Length)
                    {
                        int nextChunkSize = 1 + rand.Next(512);
                        nextChunkSize = Math.Min(nextChunkSize, dataToWrite.Length - position);
                        stream.Write(dataToWrite, position, nextChunkSize);
                        position += nextChunkSize;
                        Thread.Sleep(rand.Next(5));
                    }
                });

                var readThread = new Thread(() =>
                {
                    // read the data in randomly sized chunks at random intervals
                    var rand = new Random();
                    int position = 0;
                    while (position < dataToRead.Length)
                    {
                        int nextChunkSize = 1 + rand.Next(512);
                        nextChunkSize = Math.Min(nextChunkSize, dataToRead.Length - position);
                        int read = stream.Read(dataToRead, position, nextChunkSize);
                        position += read;
                        Thread.Sleep(rand.Next(5));
                    }
                });

                // run the threads to completion
                writeThread.Start();
                readThread.Start();
                writeThread.Join();
                readThread.Join();
            }

            // confirm the data was written and read correctly
            Enumerable.SequenceEqual(dataToWrite, dataToRead);
        }
    }
}