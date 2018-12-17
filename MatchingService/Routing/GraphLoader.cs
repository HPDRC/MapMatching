using System;
using System.Data.SqlClient;
using MatchingService.Astar;
using MatchingService.MultiCore;
using MatchingService.MultiCore.Generic;
using MatchingService.rtree;
using MatchingService.SysTools;

namespace MatchingService.Routing
{
    public class GraphLoader
    {
        protected ILogger Logger;
        protected MultiTaskCallbackFunc CallBack;
        private IProgressReporter _reporter;

        private static readonly double[] PriorityByType =
        {
            1, //0. motorway
            1, //1. motorway_link 
            1, //2. trunk
            1, //3. trunk_link
            2, //4. primary
            2, //5. primary_link
            3, //6. secondary
            3, //7. secondary_link
            4, //8. tertiary
            4, //9. tertiary_link
            5, //10.unclassified
            10, //11.service
            10, //12.residential
            10, //13.track
            10 // 14.path
        };
        private  readonly string _tableName ;
        public RoutingMachine RMachine { get; set; }

        public GraphLoader(ILogger logger,IProgressReporter reporter, MultiTaskCallbackFunc callBack, string tableName)
        {
            Logger = logger;
            CallBack = callBack;
            _tableName = tableName;
            _reporter = reporter;
        }

        public void LoadGraph()
        {
            Logger.WriteLog("Loading graph");
            SqlHelper s = new SqlHelper();
            int count = (int)s.ExecuteScalar("SELECT COUNT(*) FROM [routing].[dbo].[" + _tableName + "]");
            s.Close();
            MultiTaskWorkManager workManager = new SingleThreadWorkManager(_reporter, LoadGraph, CallBack, count);
            workManager.StartWork();
        }

        private void LoadGraph(object o)
        {
            MultiTaskThreadProperties properties = (MultiTaskThreadProperties)o;
            //properties.SubmitCount.Increase(6010490);
            //return;
            SqlHelper s = new SqlHelper();
            string query = "SELECT [id],[name],[type],[oneway],[lanes],[maxspeed]," +
                           "[start_lat],[start_lon],[end_lat],[end_lon],[start_node],[end_node] " +
                           "FROM [routing].[dbo].[" + _tableName + "]";
            SqlDataReader reader = s.GetReader(query);
            Graph graph = new Graph();
            var rTree = new RTree<RoadLeaf>();
            while (reader.Read())
            {
                double startLat = (double)reader["start_lat"];
                double startLon = (double)reader["start_lon"];
                double endLat = (double)reader["end_lat"];
                double endLon = (double)reader["end_lon"];
                bool isOneWay = (bool)reader["oneway"];
                int type = (int) reader["type"];
                double priority = PriorityByType[type];
                long startNodeId = (long)reader["start_node"];
                long endNodeId = (long)reader["end_node"];

                Node n1 = graph.AddNode(startNodeId, startLat, startLon);
                Node n2 = graph.AddNode(endNodeId, endLat, endLon);
                Arc newArc1 = new Arc(n1, n2, priority);
                graph.AddArc(newArc1);
                RoadLeaf roadLeaf;
                if (!isOneWay)
                {
                    Arc newArc2 = new Arc(n2, n1, priority);
                    graph.AddArc(newArc2);
                    roadLeaf = new RoadLeaf(newArc1, newArc2);
                }
                else
                    roadLeaf = new RoadLeaf(newArc1);
                roadLeaf.Type = type;
                roadLeaf.Id = (long)reader["id"];
                rTree.Add(
                    new Rectangle(new[]
                    {
                        Math.Min(startLat, endLat),
                        Math.Min(startLon, endLon)
                    }, new[]
                    {
                        Math.Max(startLat, endLat),
                        Math.Max(startLon, endLon)
                    }), roadLeaf);
                properties.SubmitCount.Increase();
            }
            
            reader.Close();
            Logger.WriteLog("Building RoutingMachine");
            Logger.WriteLog("Arcs:"+graph.ArcCount+", Nodes:"+graph.NodeCount);
            RMachine = new RoutingMachine(graph, rTree);
        }
    }
}
