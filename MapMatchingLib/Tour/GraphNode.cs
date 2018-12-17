using System.Collections.Generic;

namespace MapMatchingLib.Tour
{
    public abstract class GraphNode
    {
        public int Id;
        public List<GraphNode> Next;
        //public int Distance = int.MaxValue;
        //public GraphNode Parent;
        private static int _id;
        public int Weight { get; set; }
        public List<GraphNode> Connections { get; set; }
        public int TotalCost { get; set; }

        protected GraphNode()
        {
            Id = _id;
            _id++;
            Next = new List<GraphNode>();
            TotalCost = int.MaxValue;
            Connections = new List<GraphNode>();
            Weight = 1;
        }
    }
}