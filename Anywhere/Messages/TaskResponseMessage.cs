﻿using Newtonsoft.Json;

namespace AnywhereNET
{
    internal class TaskResponseMessage : IMessage
    {
        public byte[] Bytes { get; private set; } = new byte[0];

        private object? _result = null;

        public TaskResponseMessage() { }

        public TaskResponseMessage(object? result)
        {
            // TODO: the current JsonSerializer cannot serialize eg built-in
            // TODO: value types (int, string, etc) to BSON. fix this somehow
            using (var stream = new MemoryStream())
            {
                using (var streamWriter = new StreamWriter(stream: stream, leaveOpen: true))
                using (var jsonWriter = new JsonTextWriter(streamWriter))
                {
                    var jsonSerializer = new JsonSerializer();
                    jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                    jsonSerializer.Serialize(jsonWriter, result);
                }
                Bytes = stream.ToArray();
            }

            //using (var stream = new MemoryStream())
            //using (var binaryWriter = new BinaryWriter(stream, Encoding.Default, true))
            //using (var bsonWriter = new BsonDataWriter(binaryWriter))
            //{
            //    var jsonSerializer = new JsonSerializer();
            //    jsonSerializer.TypeNameHandling = TypeNameHandling.All;
            //    jsonSerializer.Serialize(bsonWriter, result);
            //    Bytes = stream.ToArray();
            //}
        }

        public object Result
        {
            get
            {
                if (_result == null)
                {
                    // TODO: throw if bytes is empty

                    using (var stream = new MemoryStream(Bytes))
                    using (var streamReader = new StreamReader(stream: stream, leaveOpen: true))
                    using (var jsonReader = new JsonTextReader(streamReader))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                        _result = jsonSerializer.Deserialize(jsonReader)!;
                    }

                    //using (var stream = new MemoryStream(Bytes))
                    //using (var binaryReader = new BinaryReader(stream, Encoding.Default, true))
                    //using (var jsonReader = new BsonDataReader(binaryReader))
                    //{
                    //    var jsonSerializer = new JsonSerializer();
                    //    jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                    //    _result = jsonSerializer.Deserialize(jsonReader)!;
                    //}
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
            stream.WriteInt32BE(Bytes.Length);
            stream.Write(Bytes);
        }
    }
}