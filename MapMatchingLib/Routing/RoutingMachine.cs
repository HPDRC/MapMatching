using System;
using System.Collections.Generic;
using System.Linq;
using MapMatchingLib.Astar;
using MapMatchingLib.MapMatching;
using MapMatchingLib.RTree;
using MapMatchingLib.SysTools;

namespace MapMatchingLib.Routing
{
    public class TempArc : Arc
    {
        public Arc SourceArc;
        public TempArc(Node start, Node end, double weight) : base(start, end, weight)
        {
        }
    }

    public class RoutingMachine
    {
        private readonly Graph _graph;
        private readonly RTree<RoadLeaf> _rTree;
        private readonly List<Node> _tempNodes;
        private bool _isStartNodeAdded, _isEndNodeAdded;
        private Arc _startArc,_endArc;

        public RoutingMachine(Graph graph, RTree<RoadLeaf> rtree)
        {
            _graph = graph;
            _rTree = rtree;
            _tempNodes=new List<Node>();
        }

        public Arc GetArcById(long id)
        {
            return _graph.GetArcById(id);
        }

        //public double GetRouteLength(NeighborRoad p1, NeighborRoad p2)
        //{
        //    if (p1.Equals(p2))
        //        return Geometry.Haversine(p1.ProjPoint, p2.ProjPoint);
        //    Arc[] route = FindRoute(p1, p2);
        //    return route.Sum(arc => arc.LengthEllipsoid);
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
        public List<Node> FindRoute(NeighborRoad startRoad, NeighborRoad endRoad, out double length,out Arc[] arcs)
        {
            if (startRoad.Equals(endRoad))
            {
                length = Geometry.Haversine(startRoad.ProjPoint, endRoad.ProjPoint);
                arcs=new Arc[0];
                return new List<Node>();
            }
            AStar AS = new AStar(_graph);
            Node start = GetNodeOnArc(startRoad);
            Node end = GetNodeOnArc(endRoad);
            bool isFound = AS.SearchPath(start, end);
            if (!isFound)
            {
                length = double.MaxValue;
                arcs = new Arc[0];
                return new List<Node>();
            }
            arcs = AS.PathByArcs;
            if(arcs[0] is TempArc)
                arcs[0] = ((TempArc) arcs[0]).SourceArc;
            if (arcs[arcs.Length-1] is TempArc)
                arcs[arcs.Length - 1] = ((TempArc)arcs[arcs.Length - 1]).SourceArc;
            length = arcs.Sum(arc => arc.LengthEllipsoid);
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
            if (!closestRoadStart.IsOneWay)
            {
                startRoad.IsOneway = false;
                startRoad.ReversedArc = closestRoadStart.Arc2;
                //NeighborRoad startRoad2 = new NeighborRoad(closestRoadStart.Arc2, p1);
                //GetNodeOnArc(startRoad2, startNode);
            }
            Node startNode = GetNodeOnArc(startRoad);
            NeighborRoad endRoad = new NeighborRoad(closestRoadEnd.Arc1, p2);
            if (!closestRoadEnd.IsOneWay)
            {
                endRoad.IsOneway = false;
                endRoad.ReversedArc = closestRoadEnd.Arc2;
                //NeighborRoad endRoad2 = new NeighborRoad(closestRoadEnd.Arc2, p1);
                //GetNodeOnArc(endRoad2, endNode);
            }
            Node endNode = GetNodeOnArc(endRoad);
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

        internal Node GetNodeOnArc(NeighborRoad neighborRoad)
        {
            Node node = null;
            if (neighborRoad.ProjPosition == SegmentPart.Start)
                node = neighborRoad.RoadArc.StartNode;
            else if (neighborRoad.ProjPosition == SegmentPart.End)
                node = neighborRoad.RoadArc.EndNode;
            else
            {
                node = AddTempNode(neighborRoad.ProjPoint);
                TempArc arcAo = new TempArc(neighborRoad.RoadArc.StartNode, node, neighborRoad.RoadArc.Weight);
                arcAo.Id=neighborRoad.RoadArc.Id;
                arcAo.SourceArc = neighborRoad.RoadArc;
                _graph.AddArc(arcAo);
                TempArc arcOb = new TempArc(node, neighborRoad.RoadArc.EndNode, neighborRoad.RoadArc.Weight);
                arcOb.Id=neighborRoad.RoadArc.Id;
                arcOb.SourceArc = neighborRoad.RoadArc;
                _graph.AddArc(arcOb);
                if (neighborRoad.IsOneway) return node;
                TempArc arcBo = new TempArc(neighborRoad.RoadArc.EndNode, node, neighborRoad.RoadArc.Weight);
                arcBo.Id = neighborRoad.ReversedArc.Id;
                arcBo.SourceArc = neighborRoad.ReversedArc;
                _graph.AddArc(arcBo);
                TempArc arcOa = new TempArc(node, neighborRoad.RoadArc.StartNode, neighborRoad.RoadArc.Weight);
                arcOa.Id = neighborRoad.ReversedArc.Id;
                arcOa.SourceArc = neighborRoad.ReversedArc;
                _graph.AddArc(arcOa);
            }
            return node;
        }

        public string RouteToJson(Arc[] arcs)
        {
            string json = "[";
            json += "[" + arcs[0].StartNode.X + "," + arcs[0].StartNode.Y + ","+arcs[0].Id+"]";
            for (int i = 0; i < arcs.Length; i++)
            {
                if (arcs[i]!=null)
                json += ",[" + arcs[i].EndNode.X + "," + arcs[i].EndNode.Y + ","+arcs[i].Id+"]";
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