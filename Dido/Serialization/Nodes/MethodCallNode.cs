﻿namespace DidoNet
{
    internal class MethodCallNode : Node
    {
        public MethodInfoModel? Method { get; set; }

        public Node[] Arguments { get; set; } = new Node[0];

        /// <summary>
        /// The instance object the method is called on, or null if it's a static method.
        /// </summary>
        public Node? Object { get; set; }
    }
}
