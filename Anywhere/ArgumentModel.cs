using Newtonsoft.Json;

namespace Anywhere
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ArgumentModel
    {
        [JsonProperty]
        public object Value { get; set; }

        [JsonProperty]
        public TypeModel Type { get; set; }
    }
}
