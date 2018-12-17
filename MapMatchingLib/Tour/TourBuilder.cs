using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using MapMatchingLib.Astar;
using MapMatchingLib.MapMatching;
using MapMatchingLib.Routing;
using MapMatchingLib.SysTools;

namespace MapMatchingLib.Tour
{
    public class TourBuilder
    {
        private readonly string[] _roads;
        private readonly RoutingMachine _routing;
        private double _routeLength;
        private readonly Arc _startArc;
        private readonly Point2D _startPoint;
        private readonly double _startLength;
        private readonly Arc _endArc;
        private readonly Point2D _endPoint;
        private readonly double _endLength;
        private readonly List<Arc> _roadArcs;
        private Dictionary<string, TrackInfo> _trackInfos;
        private string _timeOfDay;

        public TourBuilder(string[] roads, Point2D startPoint, Point2D endPoint, RoutingMachine routing,string time)
        {
            _roads = roads;
            _routing = routing;
            _startPoint = startPoint;
            _startArc = _routing.GetArcById(long.Parse(_roads[0]));
            _startLength = Point2D.DistanceBetween(_startArc.StartNode.Position, _startPoint);
            _endPoint = endPoint;
            _endArc = _routing.GetArcById(long.Parse(_roads[_roads.Length-1]));
            _endLength = Point2D.DistanceBetween(_endArc.StartNode.Position, _endPoint);
            _roadArcs=new List<Arc>();
            _timeOfDay = time;
        }

        public string GetTour()
        {
            _trackInfos = FindOverlapRoad();
            TransitionGraph root = GetTrackTransGraph(_trackInfos);
            TourScriptManager scripts = GenerateMovieScript(root);
            string scriptString = scripts.ToString();
            SqlHelper s=new SqlHelper();

            string uniqueId = Guid.NewGuid().ToString();
            SqlParameter id=new SqlParameter("id", uniqueId);
            SqlParameter script = new SqlParameter("script", scriptString);
            s.ExecuteNonQuery("INSERT INTO [dbo].[scripts] ([id],[script]) VALUES (@id,@script)", id, script);
            s.Close();
            return uniqueId;
        }

        private Dictionary<string, TrackInfo> FindOverlapRoad()
        {
            Dictionary<string, TrackInfo> overlappedTrack = new Dictionary<string, TrackInfo>();
            double accumulatedLength =-_startLength; ;

            for (int i = 0; i < _roads.Length; i++)
            {
                SqlHelper s = new SqlHelper();
                _roadArcs.Add(_routing.GetArcById(long.Parse(_roads[i])));
                //Get tracks by arc
                SqlDataReader reader = s.GetReader("SELECT t.[track_id],r.[roads_by_tracks],r.[time_of_day],r.[route] " +
                                                   "FROM [tracks_by_roads] AS t join [routes] AS r ON t.track_id=r.id " +
                                                   "WHERE t.[arc_id]=" + _roads[i]);
                while (reader.Read())
                {
                    string trackId = reader["track_id"].ToString();
                    // For track not analyzed, get all overlaps from it
                    if (!overlappedTrack.ContainsKey(trackId))
                    {
                        //initialize track info
                        TrackInfo currentTrack = new TrackInfo();
                        overlappedTrack[trackId] = currentTrack;
                        overlappedTrack[trackId].PathString = reader["route"].ToString();
                        // Read all related arcs and their overlapping parts into Dictionary
                        string[] roadsByTracks = reader["roads_by_tracks"].ToString().Split(';');
                        currentTrack.Weight = (reader["time_of_day"].ToString() == _timeOfDay) ? 1 : 100;
                        for (int j = 0; j < roadsByTracks.Length; j++)
                        {
                            OverlapParts newPart = OverlapParts.Parse(roadsByTracks[j]);
                            if (newPart.EndPercent != "")
                            {
                                currentTrack.RelatedArcs.Add(newPart);
                                currentTrack.ArcDict[newPart.Id] = newPart;
                            }
                        }

                        // Find all overlaps between this track and given route
                        currentTrack.Overlaps = FindSingleTrackOverlaps(currentTrack, accumulatedLength, i);
                    }
                }
                s.Close();
                accumulatedLength += _roadArcs[i].LengthPlane;
            }
            _routeLength = accumulatedLength;
            return overlappedTrack;
        }

        /// <summary>
        /// Find all overlaps between this track and given route
        /// </summary>
        /// <param name="currentTrack"></param>
        /// <param name="accumulatedLength"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        private List<RoadOverlap> FindSingleTrackOverlaps(TrackInfo currentTrack, double accumulatedLength, int startIndex)
        {
            List<RoadOverlap> overlaps = new List<RoadOverlap>();
            RoadOverlap lastOverlap = new RoadOverlap();

            for (int j = startIndex; j < _roads.Length; j++)
            {
                Arc currentArc = _routing.GetArcById(long.Parse(_roads[j]));
                if (currentTrack.ArcDict.ContainsKey(_roads[j]))
                {
                    OverlapParts overlapParts = currentTrack.ArcDict[_roads[j]];
                    double overlapBeginLength = double.Parse(overlapParts.StartPercent) * currentArc.LengthPlane;
                    double overlapEndLength = double.Parse(overlapParts.EndPercent) * currentArc.LengthPlane;
                    if (lastOverlap.StartPos < 0)
                    {
                        // The very first one
                        if (j == 0 && overlapBeginLength <= _startLength)
                        {
                            if (overlapEndLength <= _startLength)
                                continue;   //no overlap
                            lastOverlap.StartPos = 0;
                            lastOverlap.StartPoint = _startPoint;
                            // see note page 8, middle
                            lastOverlap.StartSecond = overlapParts.StartSecond +
                                    (_startLength - overlapBeginLength)*
                                    (overlapParts.EndSecond - overlapParts.StartSecond)/
                                    (overlapEndLength - overlapBeginLength);
                            lastOverlap.StartRoadIndex = 0;
                        }
                        else
                        {
                            lastOverlap.StartPoint = currentArc.GetPointByLength(overlapBeginLength);
                            lastOverlap.StartPos = accumulatedLength + overlapBeginLength;
                            lastOverlap.StartSecond = overlapParts.StartSecond;
                            lastOverlap.StartRoadIndex = j;
                        }
                    }
                    if (j == _roads.Length - 1)
                    {
                        lastOverlap.EndRoadIndex = j;
                        lastOverlap.EndSecond = overlapParts.EndSecond;
                        lastOverlap.EndPos = accumulatedLength + overlapEndLength;
                        lastOverlap.EndPoint = currentArc.GetPointByLength(overlapEndLength);
                        overlaps.Add(lastOverlap);
                        //if (overlapEndLength <= _endLength)
                        //{
                        //    lastOverlap.EndSecond = overlapParts.EndSecond;
                        //    lastOverlap.EndPos = accumulatedLength + overlapEndLength;
                        //    lastOverlap.EndPoint = currentArc.GetPointByLength(overlapEndLength);
                        //    overlaps.Add(lastOverlap);
                        //}
                        //else
                        //{
                        //    lastOverlap.EndPos = accumulatedLength + _endLength;
                        //    lastOverlap.EndPoint = currentArc.GetPointByLength(_endLength);
                        //    lastOverlap.EndSecond = overlapParts.EndSecond - (overlapParts.EndSecond - overlapParts.StartSecond) * (overlapEndLength - _endLength) / (overlapEndLength - overlapBeginLength);
                        //    overlaps.Add(lastOverlap);
                        //}
                    }
                    else
                    {
                        lastOverlap.EndSecond = overlapParts.EndSecond;
                        if (overlapParts.EndPercent != "1")
                        {
                            //track closed
                            lastOverlap.EndPos = accumulatedLength + overlapEndLength;
                            lastOverlap.EndPoint = currentArc.GetPointByLength(overlapEndLength);
                            overlaps.Add(lastOverlap);
                            lastOverlap.EndRoadIndex = j;
                            lastOverlap = new RoadOverlap();
                        }
                    }
                }
                else
                {
                    //road closed
                    if (lastOverlap.StartPos >= 0 && lastOverlap.EndPos < 0)
                    {
                        lastOverlap.EndPos = accumulatedLength;
                        lastOverlap.EndPoint = currentArc.StartNode.Position;
                        overlaps.Add(lastOverlap);
                        lastOverlap.EndRoadIndex = j;
                        lastOverlap = new RoadOverlap();
                    }
                }
                accumulatedLength += currentArc.LengthPlane;
            }
            //close final part
            if (lastOverlap.StartPos >= 0 && lastOverlap.EndPos < 0)
            {
                lastOverlap.EndPos = accumulatedLength;
                lastOverlap.EndPoint =_endPoint;
                lastOverlap.EndRoadIndex = _roads.Length-1;
                overlaps.Add(lastOverlap);
            }
            return overlaps;
        }

        private TransitionGraph GetTrackTransGraph(Dictionary<string, TrackInfo> overlapRoad)
        {
            
            Dictionary<TrackEvent, TrackNode> allTrackNodes = new Dictionary<TrackEvent, TrackNode>();
            List<TrackEvent> trackEvents = new List<TrackEvent>();
            List<GraphNode> activeTracks = new List<GraphNode>();

            HeadNode headNode = new HeadNode(_startPoint);
            TailNode tailNode=new TailNode(_routeLength,_endPoint,_roads.Length-1);
            TransitionGraph graph = new TransitionGraph(headNode, tailNode);
            allTrackNodes[headNode.EndEvent] = headNode;
            allTrackNodes[tailNode.StartEvent] = tailNode;
            activeTracks.Add(headNode);

            //activeTracks.Add(graph);
            foreach (KeyValuePair<string, TrackInfo> keyValuePair in overlapRoad)
            {
                foreach (RoadOverlap roadOverlap in keyValuePair.Value.Overlaps)
                {
                   TrackNode node = new TrackNode
                    {
                        StartEvent = new TrackEvent(roadOverlap.StartPos, roadOverlap.StartPoint, true, keyValuePair.Key,
                            roadOverlap.StartSecond, roadOverlap.StartRoadIndex),
                        EndEvent = new TrackEvent(roadOverlap.EndPos, roadOverlap.EndPoint, false, keyValuePair.Key,
                            roadOverlap.EndSecond, roadOverlap.EndRoadIndex)
                    };
                    node.Weight = keyValuePair.Value.Weight;
                    trackEvents.Add(node.StartEvent);
                    trackEvents.Add(node.EndEvent);
                    allTrackNodes[node.StartEvent] = node;
                    allTrackNodes[node.EndEvent] = node;
                    graph.AllNodes.Add(node);
                }
            }
            
            trackEvents.Add(headNode.EndEvent);
            trackEvents.Add(tailNode.StartEvent);
            trackEvents.Sort(); //sort by start VideoTime
            foreach (TrackEvent trackEvent in trackEvents)
            {
                TrackNode currentNode = allTrackNodes[trackEvent];
                if (trackEvent.IsStart)
                {
                    foreach (GraphNode node in activeTracks)
                    {
                        node.Next.Add(currentNode);
                    }
                    if (activeTracks.Count == 1&& activeTracks[0].GetType() == typeof(GapNode))    //only gap node inside
                    {
                        // do something to fill the gap
                        GapNode gap = (GapNode) activeTracks[0];
                        gap.EndRoadIndex = currentNode.StartEvent.RoadIndex;
                        for (int i = gap.StartRoadIndex+1; i <= gap.EndRoadIndex; i++)
                        {
                            gap.NotCoveredPart.Add(_roadArcs[i].StartNode.Position);
                        }
                        gap.NotCoveredPart.Add(currentNode.StartEvent.Point);
                        activeTracks.Clear();
                    }
                    activeTracks.Add(currentNode);
                }
                else
                {
                    if (activeTracks.Count == 1)    //last track is current one
                    {
                        GapNode gapNode=new GapNode();
                        graph.AllNodes.Add(gapNode);
                        currentNode.Next.Add(gapNode);
                        gapNode.StartRoadIndex = currentNode.EndEvent.RoadIndex;
                        gapNode.NotCoveredPart.Add(currentNode.EndEvent.Point);
                        //currentNode.EndEvent.
                        activeTracks.Add(gapNode);
                    }
                    activeTracks.Remove(currentNode);
                }
            }
            if (activeTracks.Count == 1 && activeTracks[0].GetType() == typeof(TailNode))
                return graph;
            return null;    //not possible
        }

        private TourScriptManager GenerateMovieScript(TransitionGraph graph)
        {
            List<TourScript> scripts = new List<TourScript>();
            //BreadthFirstSearch(graph.Head);
            RouteEngine routeEngine= new RouteEngine(graph.AllNodes);
            List<GraphNode> routing = routeEngine.CalculateMinCost(graph.Head, graph.Tail);
            //GraphNode nodeTraveler = graph.Tail;
            TrackNode lastNode=null;
            MovieScript lastScript = null;
            foreach (GraphNode currentNode in routing)
            {
                if (currentNode.GetType() == typeof(TrackNode))
                {
                    TrackNode node = (TrackNode)currentNode;
                    MovieScript script = new MovieScript()
                    {
                        TrackId = node.StartEvent.TrackId,
                        StartSecond = node.StartEvent.VideoTime,
                        EndSecond = node.EndEvent.VideoTime
                    };
                    if (lastNode == null)
                    {
                        lastNode = node;
                        lastScript = script;
                    }
                    else
                    {
                        // remove overlap
                        Point2D middlePoint = new Point2D((lastNode.EndEvent.Point.X + node.StartEvent.Point.X)/2,
                            (lastNode.EndEvent.Point.Y + node.StartEvent.Point.Y)/2);
                        lastScript.EndSecond = _trackInfos[lastScript.TrackId].GetTimeByPoint(middlePoint,
                            (int)lastScript.StartSecond, (int)lastScript.EndSecond);
                        script.StartSecond = _trackInfos[script.TrackId].GetTimeByPoint(middlePoint,
                            (int)script.StartSecond, (int)script.EndSecond);
                    }
                    scripts.Add(script);
                }
                else if (currentNode.GetType() == typeof (GapNode))
                {
                    GapNode node = (GapNode)currentNode;
                    scripts.Add(new TransitScript(node.NotCoveredPart));
                }
                //nodeTraveler = nodeTraveler.Parent;
            }
            //scripts.Reverse();
            return new TourScriptManager(scripts);
        }
        
        //private void BreadthFirstSearch(GraphNode graph)
        //{
        //    Queue<GraphNode> nodeQueue = new Queue<GraphNode>();
        //    graph.Distance = 0;
        //    nodeQueue.Enqueue(graph);
        //    while (nodeQueue.Count > 0)
        //    {
        //        GraphNode current = nodeQueue.Dequeue();
        //        foreach (GraphNode node in current.Next)
        //            if (node.Distance == int.MaxValue)
        //            {
        //                node.Distance = current.Distance + 1;
        //                node.Parent = current;
        //                nodeQueue.Enqueue(node);
        //            }
        //    }
        //}
    }
}
