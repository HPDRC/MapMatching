using System;
using System.Collections.Generic;
using MapMatchingLib.Astar;
using MapMatchingLib.SysTools;

namespace MapMatchingLib.MapMatching
{
    public class NeighborRoad : IComparable<NeighborRoad>
    {
        public Arc RoadArc;
        public Arc ReversedArc;
        public bool IsOneway=true;
        public Point2D RefPoint;
        public WayPoint RefWayPoint;

        public Point2D ProjPoint;
        public Node ProjNode;
        public SegmentPart ProjPosition;
        public double DistanceToRefPoint;

        public NeighborRoad MostPossibleAncient = null;
        public double MaxProbability = double.MinValue;
        public double RouteDistanceToAncient = double.MinValue;
        public double ArcDistanceToAncient = double.MinValue;
        public List<Node> RouteToAncient=null;
        public Arc[] ArcToAncient = null;
        public int Index = 0;

        public NeighborRoad(Arc roadArc, Point2D refPoint)
        {
            RoadArc = roadArc;
            RefPoint = refPoint;
            ProjPoint = Point2D.ProjectOnLine(RefPoint, RoadArc, out ProjPosition);
            DistanceToRefPoint = Geometry.Haversine(RefPoint, ProjPoint);
        }
        
        public bool Equals(NeighborRoad other)
        {
            return RoadArc.Equals(other.RoadArc);
        }

        public int CompareTo(NeighborRoad other)
        {
            return MaxProbability.CompareTo(other.MaxProbability);
        }
    }
}