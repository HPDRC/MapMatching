using MatchingService.Astar;

namespace MatchingService.MapMatching
{
    public class LineSegment
    {
        public Point2D P1;
        public Point2D P2;
        public LineSegment Prev=null;
        public LineSegment Next=null;
        public bool IsValid = true;

        public double Length
        {
            get { return Point2D.DistanceBetween(P1, P2); }
        }
        public LineSegment(Point2D p1, Point2D p2)
        {
            P1 = p1;
            P2 = p2;
        }

        public bool Equals(LineSegment segment)
        {
            return Equals(P1.Equals(segment.P1)) && Equals(P2.Equals(segment.P2));
        }

        public Point2D GetPointByLength(Point2D start, double length)
        {
            double scale = length/Length;
            return new Point2D(start.X + (P2.X - P1.X) * scale, start.Y + (P2.Y - P1.Y) * scale);
        }
    }
}
