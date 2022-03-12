using Newtonsoft.Json;

namespace AnywhereNET
{
    /// <summary>
    /// Encapsulates a serializable model of the type and value of an object.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class ValueModel
    {
        /// <summary>
        /// The value of the argument.
        /// This value must be serializable.
        /// </summary>
        [JsonProperty]
        public object? Value { get; set; }

        /// <summary>
        /// The type of the argument.
        /// </summary>
        [JsonProperty]
        public TypeModel Type { get; set; }
    }
}
