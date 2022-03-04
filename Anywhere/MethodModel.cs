using Newtonsoft.Json;
using System.Reflection;

namespace Anywhere
{
    [JsonObject(MemberSerialization.OptIn)]
    public class MethodModel
    {
        [JsonProperty]
        public bool IsStatic { get; set; }

        [JsonProperty]
        public string MethodName { get; set; }

        public MethodInfo Method { get; set; }

        [JsonProperty]
        public ArgumentModel Instance { get; set; }

        [JsonProperty]
        public ArgumentModel[] Arguments { get; set; }

        [JsonProperty]
        public TypeModel ReturnType { get; set; }

        public object Invoke()
        {
            var result = Method.Invoke(Instance.Value, Arguments.Select(a => a.Value).ToArray());
            return result;
        }
    }
}