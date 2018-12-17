using System;
using MapMatchingLib.Astar;

namespace MapMatchingLib.SysTools
{
    public static class Geometry
    {
        private const double EQuatorialEarthRadius = 6378137.0D;    //meters, WGS 84
        private const double D2R = (Math.PI/180D);

        /// <summary>
        /// Distance between to coordinates in meters.
        /// </summary>
        /// <param name="lat1"></param>
        /// <param name="long1"></param>
        /// <param name="lat2"></param>
        /// <param name="long2"></param>
        /// <returns></returns>
        public static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            double dlong = (lon2 - lon1) * D2R;
            double dlat = (lat2 - lat1) * D2R;
            double a = Math.Pow(Math.Sin(dlat / 2D), 2D) + Math.Cos(lat1 * D2R) * Math.Cos(lat2 * D2R) * Math.Pow(Math.Sin(dlong / 2D), 2D);
            double c = 2D * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1D - a));
            double d = EQuatorialEarthRadius * c;
            return d;
        }

        public static double Haversine(Point2D p1, Point2D p2)
        {
            double dlong = (p2.Y - p1.Y) * D2R;
            double dlat = (p2.X - p1.X) * D2R;
            double a = Math.Pow(Math.Sin(dlat / 2D), 2D) + Math.Cos(p1.X * D2R) * Math.Cos(p2.X * D2R) * Math.Pow(Math.Sin(dlong / 2D), 2D);
            double c = 2D * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1D - a));
            double d = EQuatorialEarthRadius * c;
            return d;
        }

        public static double Bearing(double lat1, double lon1, double lat2, double lon2)
        {
            return Math.Atan2(Math.Cos(lat1)*Math.Sin(lat2) - Math.Sin(lat1)*Math.Cos(lat2)*Math.Cos(lon2 - lon1),
                Math.Sin(lon2 - lon1) * Math.Cos(lat2)) * 57.2958;
        }

        public static double Bearing(Point2D p1, Point2D p2)
        {
            return Math.Atan2(Math.Cos(p1.X) * Math.Sin(p2.X) - Math.Sin(p1.X) * Math.Cos(p2.X) * Math.Cos(p2.Y - p1.Y),
                Math.Sin(p2.Y - p1.Y) * Math.Cos(p2.X)) * 57.2958;
        }

        public static double BearingDiff(Point2D p1, Point2D p2, Point2D p3)
        {
            return Math.Abs(Bearing(p1, p2) - Bearing(p2, p3));
        }

        public static double BearingDiff(Point2D p1, Point2D p2, Point2D q1, Point2D q2)
        {
            return Math.Abs(Bearing(p1, p2) - Bearing(q1, q2));
        }
    }
}
