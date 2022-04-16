using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Linq.Expressions;

namespace DidoNet
{
    internal class Node
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public ExpressionType ExpressionType { get; set; }
    }
}
