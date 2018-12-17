using System.Collections.Generic;

namespace MapMatchingLib.Tour
{
    public class TransitionGraph
    {
        public HeadNode Head;
        public TailNode Tail;
        public List<GraphNode> AllNodes; 
        public TransitionGraph(HeadNode head, TailNode tail)
        {
            Head = head;
            Tail = tail;
            AllNodes=new List<GraphNode>();
            AllNodes.Add(head);
            AllNodes.Add(tail);
        }
    }
}