using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using MapMatchingLib.Astar;
using MapMatchingLib.SysTools;

namespace MapMatchingLib.MapMatching
{
    public class MatchedRoute
    {
        public List<MatchedSegment> RoadList;
        public List<MatchedSegment> ValidRoadList;
        private Dictionary<string, double> _segmentSpeed;
        public double TotalTime = 0;
        //public Node StartPoint;
        //public Node EndPoint;
        //public string StartPercent;
        //public string EndPercent;
        public int WayPointCount = 0;
        public MatchedSegment Head
        {
            get { return RoadList.Count > 0 ? RoadList[0] : null; }
        }

        public MatchedRoute()
        {
            RoadList=new List<MatchedSegment>();
            ValidRoadList = new List<MatchedSegment>();
        }

        public void AppendSegByArc(Arc arc)
        {
            AppendSegment(new MatchedSegment(arc));
        }

        public void AppendSegment(MatchedSegment segment)
        {
            MatchedSegment lastSeg = RoadList.Count > 0 ? RoadList[RoadList.Count - 1] : null;
            if (lastSeg != null)
            {
                lastSeg.Next = segment;
                segment.Prev = lastSeg;
            }
            RoadList.Add(segment);
        }
        
        public string GetConnString()
        {
            string conn= "[";
            foreach (MatchedSegment matchedSegment in RoadList)
            {
                foreach (WayPoint wayPoint in matchedSegment.MatchedWayPoints)
                {
                    conn += "[[" + wayPoint.Pos.X + "," + wayPoint.Pos.Y + "],[" +
                        wayPoint.FixedPos.X + "," + wayPoint.FixedPos.Y + "]],";
                }
            }
            conn = conn.TrimEnd(',') + "]";
            return conn;
        }

        public string GetRouteString()
        {
            string route = "[";
            Random rand=new Random(DateTime.Now.Millisecond);
            foreach (MatchedSegment matchedSegment in ValidRoadList)
            {
                route += "{seg:[[";
                if(matchedSegment.StartPercent!="0")
                    route += matchedSegment.MatchedWayPoints[0].FixedPos.X + "," + matchedSegment.MatchedWayPoints[0].FixedPos.Y + "],[";
                else
                    route += matchedSegment.P1.X + "," + matchedSegment.P1.Y + "],[";
                if (matchedSegment.EndPercent != "1")
                    route += matchedSegment.MatchedWayPoints[matchedSegment.MatchedWayPoints.Count-1].FixedPos.X + "," + matchedSegment.MatchedWayPoints[matchedSegment.MatchedWayPoints.Count - 1].FixedPos.Y + "]],";
                else
                    route += matchedSegment.P2.X + "," + matchedSegment.P2.Y + "]],";
                route += "speed:"+ rand.Next(0,99);
                route += "},";
            }
            route = route.TrimEnd(',') + "]";
            return route;
        }

        private void CalculateSpeed()
        {
            foreach (MatchedSegment matchedSegment in RoadList)
            {
                float totalSpeed = matchedSegment.MatchedWayPoints.Sum(wayPoint => wayPoint.Speed);
                if (totalSpeed > 0)
                    matchedSegment.AvgSpeed = totalSpeed/matchedSegment.MatchedWayPoints.Count;
                else
                {
                    MatchedSegment tempSeg = matchedSegment.Prev;
                    while (matchedSegment.AvgSpeed < 0)
                    {
                        if (tempSeg == null)
                            matchedSegment.AvgSpeed = 0;
                        else 
                            if (tempSeg.MatchedWayPoints.Count > 0)
                                matchedSegment.AvgSpeed =
                                    tempSeg.MatchedWayPoints[tempSeg.MatchedWayPoints.Count - 1].Speed;
                            else
                                tempSeg = tempSeg.Prev;
                    }
                }
            }
        }

        public void UpdateToDatabase(string routeId)
        {
            CalculateSpeed();
            GetRoadsByTracksDataColumn(routeId);
            SqlHelper s = new SqlHelper();
            DataTable table = GetTracksByRoadsTable();
            foreach (KeyValuePair<string, double> keyValuePair in _segmentSpeed)
            {
                DataRow dataRow = table.NewRow();
                dataRow["road_id"] = long.Parse(keyValuePair.Key.Substring(0, keyValuePair.Key.Length - 1));
                dataRow["arc_id"] = long.Parse(keyValuePair.Key);
                dataRow["track_id"] = routeId;
                dataRow["avg_speed"] = keyValuePair.Value;
                table.Rows.Add(dataRow);
            }
            s.BulkCopyWithLock("tracks_by_roads", table);
            table.Dispose();
            s.Close();
        }


        /// <summary>
        /// Get roads_by_tracks column value
        /// </summary>
        /// <returns></returns>
        private void GetRoadsByTracksDataColumn(string routeId)
        {
            SqlHelper s = new SqlHelper();
            _segmentSpeed = new Dictionary<string, double>();
            string route = "";
            foreach (MatchedSegment matchedSegment in ValidRoadList)
            {
                if(!_segmentSpeed.ContainsKey(matchedSegment.RoadSegment.Id))
                    _segmentSpeed.Add(matchedSegment.RoadSegment.Id, matchedSegment.AvgSpeed);
                route += matchedSegment.ToString();
                //route += matchedSegment.RoadSegment.Id + ",";
                //route += matchedSegment.StartPercent + ",";
                //route += matchedSegment.EndPercent + ",";
                //route += matchedSegment.StartSecond.ToString("0.0") + ",";
                //route += matchedSegment.EndSecond.ToString("0.0") + ";";
            }
            route = route.TrimEnd(';');
            SqlParameter roads = new SqlParameter("roads", route);
            SqlParameter id = new SqlParameter("id", routeId);
            SqlParameter time = new SqlParameter("time", TotalTime);
            s.ExecuteNonQuery("UPDATE [dbo].[routes] SET [roads_by_tracks] =@roads,[parse_time]=@time WHERE [id] =@id", roads,id, time);
            s.Close();
        }

        private DataTable GetTracksByRoadsTable()
        {
            DataTable dt = new DataTable();
            DataColumn dc = new DataColumn
            {
                DataType = Type.GetType("System.Int64"),
                ColumnName = "road_id",
                Unique = false
            };
            dt.Columns.Add(dc);
            dc = new DataColumn
            {
                DataType = Type.GetType("System.Int64"),
                ColumnName = "arc_id",
                Unique = false
            };
            dt.Columns.Add(dc);
            dc = new DataColumn
            {
                DataType = Type.GetType("System.Guid"),
                ColumnName = "track_id",
                Unique = false
            };
            dt.Columns.Add(dc);
            dc = new DataColumn
            {
                DataType = Type.GetType("System.Double"),
                ColumnName = "avg_speed",
                Unique = false
            };
            dt.Columns.Add(dc);
            return dt;
        }
    }
}