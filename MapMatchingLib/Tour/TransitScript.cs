using System.Collections.Generic;
using MapMatchingLib.Astar;
using MapMatchingLib.SysTools;

namespace MapMatchingLib.Tour
{
    public class TransitScript : TourScript
    {
        public List<Point2D> TransitPoints;


        public TransitScript(List<Point2D> transitPoints)
        {
            TransitPoints = new List<Point2D>();
            if (transitPoints.Count > 0)
            {
                TransitPoints.Add(transitPoints[0]); 
                Point2D lastPoint = TransitPoints[0];
                for (int i = 1; i < transitPoints.Count; i++)
                {
                    if (Geometry.Haversine(transitPoints[i], lastPoint)>1)//transitPoints[i].X != lastPoint.X && transitPoints[i].Y != lastPoint.Y)
                    {
                        TransitPoints.Add(transitPoints[i]);
                        lastPoint = transitPoints[i];
                    }
                }
            }
        }

        public override string ToString()
        {
            if (TransitPoints.Count > 1)
            {
                string json = "{type:'transit',points:[";
                string dist = "";
                double totalLength = 0;
                Point2D lastPoint = TransitPoints[0];
                json += "[" + lastPoint.Y + "," + lastPoint.X + "]";
                for (int i = 1; i < TransitPoints.Count; i++)
                {
                    json += ",[" + TransitPoints[i].Y + "," + TransitPoints[i].X + "]";
                    double length = Geometry.Haversine(lastPoint, TransitPoints[i]);
                    totalLength += length;
                    dist += length.ToString("0.0") + ",";
                    lastPoint = TransitPoints[i];
                }
                json += "],dist:[" + dist.TrimEnd(',') +"],length:"+ totalLength.ToString("0.0") + "}";
                return json;
            }
            else
            {
                return "";
            }
        }
    }
}