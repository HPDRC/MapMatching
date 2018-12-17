using System;
using MapMatchingLib.Astar;

namespace MapMatchingLib.Routing
{
    [Serializable]
    public class RoadLeaf
    {
        public long Id;
        public bool IsOneWay;
        public Node StartNode;
        public Node EndNode;
        public Arc Arc1;
        public Arc Arc2;
        public int Type;
        public RoadLeaf(Arc arc)
        {
            Arc1 = arc;
            StartNode = arc.StartNode;
            EndNode = arc.EndNode;
            IsOneWay = true;
        }
        public RoadLeaf(Arc arc1, Arc arc2)
        {
            Arc1 = arc1;
            Arc2 = arc2;
            StartNode = arc1.StartNode;
            EndNode = arc1.EndNode;
            IsOneWay = false;
        }

        public bool Equals(RoadLeaf other)
        {
            return StartNode.Equals(other.StartNode) && EndNode.Equals(other.EndNode);
        }
    }
}