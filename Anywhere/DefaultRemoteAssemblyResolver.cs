using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System.Text;

namespace AnywhereNET
{
    public interface IMessage
    {
        void Write(Stream stream);
        void Read(Stream stream);
    }

    internal class AssemblyRequestMessage : IMessage
    {
        public string? AssemblyName { get; private set; }

        public AssemblyRequestMessage() { }

        public AssemblyRequestMessage(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        public void Read(Stream stream)
        {
            AssemblyName = stream.ReadString();
        }

        public void Write(Stream stream)
        {
            if (AssemblyName != null)
            {
                stream.WriteString(AssemblyName);
            }
        }
    }

    internal class AssemblyResponseMessage : IMessage
    {
        public byte[]? Bytes { get; private set; }

        public AssemblyResponseMessage() { }

        public AssemblyResponseMessage(byte[] bytes)
        {
            Bytes = bytes;
        }

        public void Read(Stream stream)
        {
            int length = stream.ReadInt32BE();
            Bytes = stream.ReadBytes(length);
        }

        public void Write(Stream stream)
        {
            if (Bytes != null)
            {
                stream.WriteInt32BE(Bytes.Length);
                stream.Write(Bytes);
            }
        }
    }

    internal class ExpressionRequestMessage : IMessage
    {
        public byte[]? Bytes { get; private set; }

        public ExpressionRequestMessage() { }

        public ExpressionRequestMessage(byte[] bytes)
        {
            Bytes = bytes;
        }

        public void Read(Stream stream)
        {
            int length = stream.ReadInt32BE();
            Bytes = stream.ReadBytes(length);
        }

        public void Write(Stream stream)
        {
            if (Bytes != null)
            {
                stream.WriteInt32BE(Bytes.Length);
                stream.Write(Bytes);
            }
        }
    }

    internal class ExpressionResultMessage : IMessage
    {
        public byte[]? Bytes { get; private set; }

        private object? _result = null;

        public ExpressionResultMessage() { }

        public ExpressionResultMessage(object result)
        {
            using (var stream = new MemoryStream())
            using (var binaryWriter = new BinaryWriter(stream, Encoding.Default, true))
            using (var bsonWriter = new BsonDataWriter(binaryWriter))
            {
                var jsonSerializer = new JsonSerializer();
                jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                jsonSerializer.Serialize(bsonWriter, result);
                Bytes = stream.ToArray();
            }
        }

        public object Result
        {
            get
            {
                if (_result == null)
                {
                    // TODO: throw if bytes is not defined
                    using (var stream = new MemoryStream(Bytes))
                    using (var binaryReader = new BinaryReader(stream, Encoding.Default, true))
                    using (var jsonReader = new BsonDataReader(binaryReader))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                        _result = jsonSerializer.Deserialize(jsonReader)!;
                    }
                }
                return _result;
            }
        }

        public void Read(Stream stream)
        {
            int length = stream.ReadInt32BE();
            Bytes = stream.ReadBytes(length);
        }

        public void Write(Stream stream)
        {
            if (Bytes != null)
            {
                stream.WriteInt32BE(Bytes.Length);
                stream.Write(Bytes);
            }
        }
    }

    public class DefaultRemoteAssemblyResolver
    {
        private Channel Channel { get; set; }

        public DefaultRemoteAssemblyResolver(Channel channel)
        {
            Channel = channel;
            Channel.BlockingReads = true;
        }

        public Task<Stream?> ResolveAssembly(Environment env, string assemblyName)
        {
            if (env.ApplicationChannel == null)
            {
                throw new InvalidOperationException($"{nameof(env.ApplicationChannel)} is null");
            }

            // TODO: request the assembly from the application using the assembliesChannel
            var request = new AssemblyRequestMessage(assemblyName);
            request.Write(Channel);

            // TODO: receive the assembly and yield it to the caller
            // TODO: should we receive is as a message and return eg a memory stream? or just return the stream?

            //await Channel.WaitForDataAsync();
            var response = new AssemblyResponseMessage();
            response.Read(Channel);
            //var length = Channel.ReadInt32BE();
            //var bytes = Channel.ReadBytes(length);

            // the current caller will dispose the stream, so we need to wrap in another stream
            // to keep the channel open
            // TODO: use a different pattern
            return Task.FromResult<Stream?>(new MemoryStream(response.Bytes));

            //return Task.FromResult(env.ApplicationChannel)!;
        }
    }
}