using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;

namespace DidoNet
{
    internal class ConstantNode : Node
    {
        private ISerializationBinder TypeBinder;

        private Environment _environment;

        [JsonProperty]
        private byte[] EncodedValue;

        private object CachedValue;

        public TypeModel Type { get; set; }

        [JsonIgnore]
        public Environment Environment
        {
            get { return _environment; }
            set
            {
                _environment = value;
                TypeBinder = new DeserializeTypeBinder(_environment);
            }
        }

        [JsonIgnore]
        public object Value
        {
            get
            {
                if (CachedValue == null)
                {
                    // deserialize the value from the encoded byte array
                    using (var stream = new MemoryStream(EncodedValue))
                    using (var streamReader = new StreamReader(stream: stream, leaveOpen: true))
                    using (var jsonReader = new JsonTextReader(streamReader))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                        jsonSerializer.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full;
                        if (TypeBinder != null)
                        {
                            jsonSerializer.SerializationBinder = TypeBinder;
                        }
                        CachedValue = jsonSerializer.Deserialize(jsonReader)!;
                    }

                    // since the value was serialized as json, if it is numeric and its
                    // type differs from the intended deserialized type, convert it
                    if (
                        (CachedValue.GetType() == typeof(long) && Type != typeof(long)) ||
                        (CachedValue.GetType() == typeof(double) && Type != typeof(double))
                        )
                    {
                        CachedValue = Convert.ChangeType(CachedValue, Type.ToType(_environment));
                    }
                }
                return CachedValue;
            }
            set
            {
                // serialize the value to an encoded byte array
                using (var stream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(stream: stream, leaveOpen: true))
                    using (var jsonWriter = new JsonTextWriter(streamWriter))
                    {
                        var jsonSerializer = new JsonSerializer();
                        jsonSerializer.TypeNameHandling = TypeNameHandling.All;
                        jsonSerializer.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full;
                        if (TypeBinder != null)
                        {
                            jsonSerializer.SerializationBinder = TypeBinder;
                        }
                        jsonSerializer.Serialize(jsonWriter, value);
                    }
                    EncodedValue = stream.ToArray();
                }
                CachedValue = value;
            }
        }
    }
}
