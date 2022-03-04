using Newtonsoft.Json;

namespace Anywhere
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TypeModel
    {
        public TypeModel() { }

        public TypeModel(Type type)
        {
            Name = type.FullName;
            AssemblyName = type.Assembly.FullName;
            RuntimeVersion = type.Assembly.ImageRuntimeVersion;
        }

        [JsonProperty]
        public string Name { get; set; } = String.Empty;

        [JsonProperty]
        public string AssemblyName { get; set; } = String.Empty;

        [JsonProperty]
        public string RuntimeVersion { get; set; } = String.Empty;
    }
}
