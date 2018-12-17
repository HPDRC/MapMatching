using System;
using MapMatchingLib.Astar;

namespace MapMatchingLib.Tour
{
    public class TrackEvent : IComparable<TrackEvent>
    {
        public double Position;
        public Point2D Point;
        public bool IsStart;
        public string TrackId;
        public double VideoTime;
        public int RoadIndex;
        public TrackEvent(double position, Point2D point, bool isStart, string trackId, double videoTime, int roadIndex)
        {
            Position = position;
            Point = point;
            IsStart = isStart;
            TrackId = trackId;
            VideoTime = videoTime;
            RoadIndex = roadIndex;
        }

        public int CompareTo(TrackEvent other)
        {
            if (Position > other.Position)
                return 1;
            if (Position < other.Position)
                return -1;
            //If same position, process start first, then close other end
            if (IsStart && !other.IsStart)
                return -1;
            if (!IsStart && other.IsStart)
                return 1;
            return 0;
        }
    }
}