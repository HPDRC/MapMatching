using System;
using MapMatchingLib.Astar;

namespace MapMatchingLib.MapMatching
{
    public class WayPoint : ICloneable
    {
        public DateTime Time;
        public Point2D Pos;
        public Point2D FixedPos;
        public float Speed;
        public float Altitude;
        public bool IsValid=true;
        public int Id;
        public MatchedSegment SegmentMatched;
        public SegmentPart ProjectPosition;

        public WayPoint(DateTime time, Point2D pos, float speed, float altitude,int id=-1)
        {
            Time = time;
            Pos = pos;
            Speed = speed;
            Altitude = altitude;
            Id = id;
        }

        public WayPoint()
        {
        }

        public object Clone()
        {
            return new WayPoint(Time, new Point2D(Pos.X, Pos.Y), Speed, Altitude);
        }
    }
}
