using System;
using System.Collections.Generic;
using MapMatchingLib.Astar;
using MapMatchingLib.MapMatching;

namespace MapMatchingLib.Tour
{
    public class TrackInfo
    {
        public List<RoadOverlap> Overlaps;
        public List<OverlapParts> RelatedArcs;
        public Dictionary<string, OverlapParts> ArcDict;
        public int Weight;
        public string PathString;
        private List<Point2D> _path;

        public TrackInfo()
        {
            //Overlaps = new List<RoadOverlap>();
            RelatedArcs = new List<OverlapParts>();
            ArcDict = new Dictionary<string, OverlapParts>();
        }

        public int GetTimeByPoint(Point2D position, int min, int max)
        {
            if (_path == null)
                ParsePath();
            double minDis = double.MaxValue;
            int minTime = -1;
            if (_path != null)
            {
                for (int i = min; i < max; i++)
                {

                    double dis = Point2D.DistanceBetween(_path[i], position);
                    if (dis >= minDis) continue;
                    minDis = dis;
                    minTime = i;
                }
            }
            return minTime;
        }

        private void ParsePath()
        {
            _path=new List<Point2D>();
            string sub = PathString.Substring(PathString.IndexOf("[{", StringComparison.Ordinal));
            sub = sub.Substring(0, sub.IndexOf("}]", StringComparison.Ordinal));
            string[] points = sub.Split(new string[] { "},{" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (string point in points)
            {
                string[] elements = point.Split(',');
                string lat = elements[1].Split(':')[1];
                string lon = elements[2].Split(':')[1];
                _path.Add(new Point2D(double.Parse(lat), double.Parse(lon)));
            }
        }
    }
}