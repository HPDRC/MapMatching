using System;
using MatchingService.Astar;

namespace MatchingService.MapMatching
{
    public class WayPoint : ICloneable
    {
        public DateTime Time;
        public Point2D Pos;
        public float Speed;
        public float Altitude;

        public WayPoint(DateTime time, Point2D pos, float speed, float altitude)
        {
            Time = time;
            Pos = pos;
            Speed = speed;
            Altitude = altitude;
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
