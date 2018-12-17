using System;
using System.Collections.Generic;
using System.Globalization;
using MapMatchingLib.Astar;
using MapMatchingLib.MapMatching;

namespace MapMatchingLib.mvideo
{
    public class Route{}

    public static class RouteParser
    {
        public static List<WayPoint> ParseRoute(string routeString)
        {
            int idx = 0;
            List<WayPoint> route = new List<WayPoint>();

            char c = routeString[idx++];
            while (c != '[')
            {
                c = routeString[idx++];
            }
            //c='['
            while (c != ']')
            {
                c = routeString[idx++];
                //c='{'
                string pointString;
                route.Add(ParsePoint(ref c, ref routeString, ref idx));
                //c=',' or ']'
            }
            while (c != '}')
            {
                c = routeString[idx++];
            }
            return route;
        }

        private static WayPoint ParsePoint(ref char c, ref string route, ref int idx)
        {
            //c='{' next is 'timestamp'
            WayPoint point = new WayPoint();
            string dateString = ParseStringValue(ref c, ref route, ref idx);
            point.Time = DateTime.ParseExact(dateString, "yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
            //c=',' next is 'lat'
            double lat = double.Parse(ParseNumberValue(ref c, ref route, ref idx));
            //c=',' next is 'lon'
            double lon = double.Parse(ParseNumberValue(ref c, ref route, ref idx));
            point.Pos = new Point2D(lat,lon);
            //c=',' next is 'speed'
            point.Speed = float.Parse(ParseNumberValue(ref c, ref route, ref idx));
            //c=',' next is 'altitude'
            point.Altitude= float.Parse(ParseNumberValue(ref c, ref route, ref idx));
            while (c != '}') c = route[idx++];
            c = route[idx++];
            //c=',' or ']'
            return point;
        }

        private static string ParseNumberValue(ref char c, ref string route, ref int idx)
        {
            while (c != ':')
            {
                c = route[idx++];
            }
            //c=':' next is number
            string num = "";
            c = route[idx++];
            while (c != ',')
            {
                num += c;
                c = route[idx++];
            }
            //c=',' or '}'
            return num;
        }

        private static string ParseStringValue(ref char c, ref string route, ref int idx)
        {
            string str = "";
            while (c != ':')
            {
                c = route[idx++];
            }
            //c=':' next is "'"
            idx++;
            //c="'" next is number
            c = route[idx++];
            while (c != '\'')
            {
                str += c;
                c = route[idx++];
            }
            c = route[idx++];
            //c=',' or '}'
            return str;
        }
    }
}
