namespace DidoNet
{
    internal class MemberNode : Node
    {
        public Node? Expression { get; set; }

        public MemberInfoModel? Member { get; set; }
    }
}
