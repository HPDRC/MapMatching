using System;
using System.Collections.Generic;
using System.Linq;
using MatchingService.Astar;
using MatchingService.MapMatching;
using MatchingService.rtree;
using MatchingService.SysTools;

namespace MatchingService.Routing
{
    public class RoutingMachine
    {
        private readonly Graph _graph;
        private readonly RTree<RoadLeaf> _rTree;
        private readonly List<Node> _tempNodes;

        public RoutingMachine(Graph graph, RTree<RoadLeaf> rtree)
        {
            _graph = graph;
            _rTree = rtree;
            _tempNodes=new List<Node>();
        }

        //public double GetRouteLength(NeighborRoad p1, NeighborRoad p2)
        //{
        //    if (p1.Equals(p2))
        //        return Geometry.Haversine(p1.ProjPoint, p2.ProjPoint);
        //    Arc[] route = FindRoute(p1, p2);
        //    return route.Sum(arc => arc.Length);
        //}

        /// <summary>
        /// Route between two nodes.
        /// </summary>
        /// <param name="n1"></param>
        /// <param name="n2"></param>
        /// <returns></returns>
        public Arc[] FindRoute(Node n1, Node n2)
        {
            AStar AS = new AStar(_graph);
            bool isFound = AS.SearchPath(n1, n2);
            Arc[] result = AS.PathByArcs;
            return isFound ? result : new Arc[0];
        }

        /// <summary>
        /// Route between two points on way segments.
        /// </summary>
        /// <param name="startRoad"></param>
        /// <param name="endRoad"></param>
        /// <returns></returns>
        public List<Node> FindRoute(NeighborRoad startRoad, NeighborRoad endRoad, out double length)
        {
            if (startRoad.Equals(endRoad))
            {
                length = Geometry.Haversine(startRoad.ProjPoint, endRoad.ProjPoint);
                return new List<Node>();
            }
            AStar AS = new AStar(_graph);
            Node start = GetNodeOnArc(startRoad);
            Node end = GetNodeOnArc(endRoad);
            bool isFound = AS.SearchPath(start, end);
            if (!isFound)
            {
                length = double.MaxValue;
                return new List<Node>();
            }
            Arc[] arcs = AS.PathByArcs;
            length = arcs.Sum(arc => arc.Length);
            List<Node> result = AS.PathByNodes.ToList();
            if(result[0].Id>=2000000000)
                result.RemoveAt(0);
            if (result[result.Count-1].Id >= 2000000000)
                result.RemoveAt(result.Count - 1);
            CleanTempNodes();
            return result;
        }

        /// <summary>
        /// Route between two random points
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        public Arc[] FindRoute(Point2D p1, Point2D p2)
        {
            Node originNode = AddTempNode(p1); // new Node(0, lat1, lon1);
            Node destinationNode = AddTempNode(p2); // new Node(1, lat2, lon2);

            RoadLeaf closestRoadStart = GetNearestRoad(originNode.X, originNode.Y, 0.01);
            if (closestRoadStart == null)
                throw new Exception("Start point out of map border.");
            RoadLeaf closestRoadEnd = GetNearestRoad(destinationNode.X, destinationNode.Y, 0.01);
            if (closestRoadEnd == null)
                throw new Exception("End point out of map border.");
            
            NeighborRoad startRoad = new NeighborRoad(closestRoadStart.Arc1, p1);
            Node startNode = GetNodeOnArc(startRoad);
            if (!closestRoadStart.IsOneWay)
            {
                NeighborRoad startRoad2 = new NeighborRoad(closestRoadStart.Arc2, p1);
                GetNodeOnArc(startRoad2, startNode);
            }
            NeighborRoad endRoad = new NeighborRoad(closestRoadEnd.Arc1, p2);
            Node endNode = GetNodeOnArc(endRoad);
            if (!closestRoadEnd.IsOneWay)
            {
                NeighborRoad endRoad2 = new NeighborRoad(closestRoadEnd.Arc2, p1);
                GetNodeOnArc(endRoad2, endNode);
            }
            Arc arcStart = new Arc(originNode, startNode, 1);
            Arc arcEnd = new Arc(endNode, destinationNode, 1);

            if (Equals(closestRoadStart.StartNode, closestRoadEnd.StartNode) &&
                Equals(closestRoadStart.EndNode, closestRoadEnd.EndNode))
                return new[] { arcStart, new Arc(startNode, endNode, 1), arcEnd };
            
            _graph.AddArc(arcStart);
            _graph.AddArc(arcEnd);
            Arc[] result = FindRoute(originNode, destinationNode);
            CleanTempNodes();
            return result;
        }

        public RoadLeaf GetNearestRoad(double lat, double lon, double distance)
        {
            ElementDistance<RoadLeaf> ed = _rTree.Nearest(new Point(lat, lon), distance);
            return ed!=null ? ed.Element : null;
        }

        public List<ElementDistance<RoadLeaf>> GetKNearestRoads(double lat, double lon, double distance)
        {
            List<ElementDistance<RoadLeaf>> eds=_rTree.KNN(new Point(lat, lon), distance);
            eds.Sort();
            return eds;
        }

        internal Node GetNodeOnArc(NeighborRoad neighborRoad, Node node=null)
        {
            if (neighborRoad.ProjPosition == NearestPostion.Start)
                node = neighborRoad.RoadArc.StartNode;
            else if (neighborRoad.ProjPosition == NearestPostion.End)
                node = neighborRoad.RoadArc.EndNode;
            else
            {
                if (node == null)
                    node = AddTempNode(neighborRoad.ProjPoint);
                Arc arcAo = new Arc(neighborRoad.RoadArc.StartNode, node, neighborRoad.RoadArc.Weight);
                _graph.AddArc(arcAo);
                Arc arcOb = new Arc(node, neighborRoad.RoadArc.EndNode, neighborRoad.RoadArc.Weight);
                _graph.AddArc(arcOb);
                if (neighborRoad.IsOneway) return node;
                Arc arcBo = new Arc(neighborRoad.RoadArc.EndNode, node, neighborRoad.RoadArc.Weight);
                _graph.AddArc(arcBo);
                Arc arcOa = new Arc(node, neighborRoad.RoadArc.StartNode, neighborRoad.RoadArc.Weight);
                _graph.AddArc(arcOa);
            }
            return node;
        }

        public string RouteToJson(Arc[] arcs)
        {
            string json = "[";
            json += "[" + arcs[0].StartNode.X + "," + arcs[0].StartNode.Y + "]";
            for (int i = 0; i < arcs.Length; i++)
            {
                if (arcs[i]!=null)
                json += ",[" + arcs[i].EndNode.X + "," + arcs[i].EndNode.Y + "]";
            }
            return json + "]";
        }

        public void CleanTempNodes()
        {
            foreach (Node tempNode in _tempNodes)
            {
                _graph.RemoveNode(tempNode);
            }
            _tempNodes.Clear();
        }

        public Node AddTempNode(Point2D p)
        {
            Node tempNode = _graph.AddNode(_tempNodes.Count+2000000000, p.X, p.Y);
            _tempNodes.Add(tempNode);
            return tempNode;
        }

        public string BoxQuery(double lat1, double lon1, double lat2, double lon2)
        {
            string json = "[";
            Rectangle searchBox = new Rectangle(lat1, lon1, lat2, lon2);
            List<RoadLeaf> roads = _rTree.Intersects(searchBox);
            foreach (RoadLeaf roadLeaf in roads)
            {
                json += "{i:" + roadLeaf.Id + ",p:";
                json += roadLeaf.Arc1 + ",o:'" + roadLeaf.IsOneWay + "'},";
            }
            return json.TrimEnd(',') + "]";
        }
    }
}