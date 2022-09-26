using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DidoNet
{
    /// <summary>
    /// A message sent from a runner to an application when a task completes successfully.
    /// </summary>
    internal class TaskResponseMessage : IMessage
    {
        // TODO: add compression
        public byte[] Bytes { get; private set; } = new byte[0];

        public TaskResponseMessage() { }

        public TaskResponseMessage(object? result)
        {
            // if the result is a task, get its value
            var resultType = result?.GetType();
            if (resultType != null && resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                result = resultType.GetProperty(nameof(Task<object?>.Result))?.GetValue(result);
            }

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

            // TODO: the current JsonSerializer cannot serialize built-in
            // TODO: value types (int, string, etc) to BSON. fix this somehow
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

        public object? Result
        {
            get
            {
                if (_result == null && Bytes.Length > 0)
                {
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

        public T GetResult<T>()
        {
            // _result will always be a non-task value.
            // if the desired type is a Task, wrap the result in a properly typed
            // Task so it can be awaited.
            var intendedType = typeof(T);
            if (intendedType.IsGenericType && intendedType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // convert the result to the expected generic task type
                var resultGenericType = intendedType.GenericTypeArguments[0];
                var resultValue = Convert.ChangeType(Result, resultGenericType);
                // then construct a Task.FromResult using the expected generic type
                var method = typeof(Task).GetMethod(nameof(Task.FromResult), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                method = method!.MakeGenericMethod(resultGenericType);
                return (T)method!.Invoke(null, new[] { resultValue })!;
            }
            else
            {
                return (T)Convert.ChangeType(Result, typeof(T))!;
            }
        }

        public void Read(Stream stream)
        {
            Bytes = stream.ReadByteArray();
            _result = null;
        }

        public void Write(Stream stream)
        {
            stream.WriteByteArray(Bytes);
        }

        private object? _result = null;
    }
}