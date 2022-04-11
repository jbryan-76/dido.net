namespace AnywhereNET
{
    internal class UnaryNode : Node
    {
        public MethodInfoModel? Method { get; set; }
        public Node Operand { get; set; }
        public TypeModel Type { get; set; }
    }
}
