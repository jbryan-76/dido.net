namespace DidoNet
{
    internal class BinaryNode : Node
    {
        public Node? Conversion { get; set; }
        public Node Left { get; set; }
        public bool LiftToNull { get; set; }
        public MethodInfoModel? Method { get; set; }
        public Node Right { get; set; }
    }
}
