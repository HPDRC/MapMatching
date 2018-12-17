using System.Collections.Generic;
using MapMatchingLib.Astar;

namespace MapMatchingLib.Tour
{
    public class GapNode : GraphNode
    {
        public List<Point2D> NotCoveredPart;
        public int StartRoadIndex = -1;
        public int EndRoadIndex = -1;

        public GapNode()
        {
            NotCoveredPart=new List<Point2D>();
        }
    }
}