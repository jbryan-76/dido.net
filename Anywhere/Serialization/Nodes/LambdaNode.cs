namespace DidoNet
{
    internal class LambdaNode : Node
    {
        public Node Body { get; set; }
        public Node[] Parameters { get; set; }
        public string? Name { get; set; }
        public TypeModel ReturnType { get; set; }
    }
}
