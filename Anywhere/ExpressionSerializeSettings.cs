namespace AnywhereNET
{
    public class ExpressionSerializeSettings
    {
        public enum Formats
        {
            Json,
            Bson,

            // TODO: explore serializing the node tree to a stream as optimized binary data.
            // TODO: for example, since types will probably be reused, the most compact way might be 
            // TODO: to store the types separately, then store the tree?
            // Binary 
        }

        public Formats Format { get; set; } = Formats.Json;

        public bool LeaveOpen { get; set; } = true;
    }
}
