using System.Collections.Generic;
using MapMatchingLib.Astar;

namespace MapMatchingLib.MapMatching
{
    public class MatchedSegment: OverlapParts
    {
        public Arc RoadSegment;
        public List<WayPoint> MatchedWayPoints;
        public int WayPointCount
        {
            get { return MatchedWayPoints.Count; }
        }

        public double AvgSpeed=-1;
        public MatchedSegment Next = null;
        public MatchedSegment Prev = null;
        public Point2D P1
        {
            get { return RoadSegment.StartNode.Position; }
        }

        public Point2D P2
        {
            get { return RoadSegment.EndNode.Position; }
        }

        public Node StartNode
        {
            get { return RoadSegment.StartNode; }
        }

        public Node EndNode
        {
            get { return RoadSegment.EndNode; }
        }

        public MatchedSegment(Arc arc):base(arc.Id)
        {
            RoadSegment = arc;
            MatchedWayPoints = new List<WayPoint>();
        }

        public double Length
        {
            get { return Point2D.DistanceBetween(P1, P2); }
        }

        public Point2D GetPointByLength(Point2D start, double length)
        {
            double scale = length / Length;
            return new Point2D(start.X + (P2.X - P1.X) * scale, start.Y + (P2.Y - P1.Y) * scale);
        }

        public void AddWayPoint(WayPoint waypoint)
        {
            MatchedWayPoints.Add(waypoint);
        }
    }
}