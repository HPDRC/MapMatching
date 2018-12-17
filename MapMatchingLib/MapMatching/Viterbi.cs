using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MapMatchingLib.Astar;
using MapMatchingLib.MultiCore.Generic;
using MapMatchingLib.RTree;
using MapMatchingLib.Routing;
using MapMatchingLib.SysTools;
using Point = System.Drawing.Point;

namespace MapMatchingLib.MapMatching
{
    public class Viterbi
    {
        public List<WayPoint> RawWayPoints;
        public List<WayPoint> CleanedWayPoints;
        public List<WayPoint> ObservationWayPoints; //Observations
        public Dictionary<WayPoint, List<NeighborRoad>> RoadSegments; //States

        private readonly double _sigma = 4.07;
        private readonly double _beta = 5;
        private readonly double _distance = 0.0003;
        private double SigmaMultiSqrt2Pi
        {
            get { return _sigma*Math.Sqrt(Math.PI*2); }
        }

        private readonly double _interval = 200;
        private readonly double _percent = 5;
        //private const int MaxStateQuantity = 15;  //We only keep this number of most possible road.
        private const double ValidPointDistance = 10;
            //point is invalid outside 10 meter radius of the road while matching

        private const double ValidPointThreshold = 30; //max distance for an invalid point to match
        private const double LowSpeedThreshold = 3; //Group the points together with speed lower than this mph.

        private const double TurnAngleThreshold = 30;
            //We keep the point when the turning degree greater than this value  

        private const double MinDisForAngleFilter = 7; //To avoid many points while turning, we set a threshold
        private const double FilterSkipDistance = 10; //The point outside this range will be skipped.

        private readonly RoutingMachine _routing;
        private List<NeighborRoad> _resultList;
        private readonly ILogger _logger;
        private List<Node> _finalRoute;
        private MatchedRoute _matchedRoute;
        //private List<WayPoint> _fixedWayPoints;
        //private List<WayPoint> _secondRunPoints;
        private DateTime _startTime;

        private string _invalidSegments = "[]";
        private double _avgSpeed;

        public Viterbi(ILogger logger, RoutingMachine routing, List<WayPoint> rawWayPoints)
        {
            _logger = logger;
            _routing = routing;
            RawWayPoints = rawWayPoints;
            ObservationWayPoints = new List<WayPoint>();
            RoadSegments = new Dictionary<WayPoint, List<NeighborRoad>>();
            _startTime = rawWayPoints[0].Time;
            //_sigma = logger.GetSigma();
            //_beta = logger.GetBeta();
            //_distance = logger.GetDistance();
            //_interval = logger.GetInterval();
            //_percent = logger.GetDiffPercent();
            //_sigmaMultiSqrt2Pi = _sigma*Math.Sqrt(Math.PI*2);
        }

        public Viterbi(ILogger logger, RoutingMachine routing, List<WayPoint> rawWayPoints, double sigma, double beta,
            double distance, double interval, double diff)
            : this(logger, routing, rawWayPoints)
        {
            _sigma = sigma;
            _beta = beta;
            _distance = distance;
            _interval = interval;
            _percent = diff;
        }


        private void Calculate(bool isTest = false)
        {
            CleanWayPoints(isTest);
            DateTime startTime = DateTime.Now;
            //get closest ways for each waypoint, as state
            int wayPointCounter = 0;

            //for (int i = 0; i < ObservationWayPoints.Count; i++)
            List<WayPoint> filteredObservation=new List<WayPoint>();
            foreach (WayPoint wayPoint in ObservationWayPoints)
            {
                RoadSegments[wayPoint] = new List<NeighborRoad>();
                double dis = _distance;
                if (wayPoint.Speed > 25 && wayPoint.Speed <= 45)
                    dis = dis/2;
                else if (wayPoint.Speed > 45 && wayPoint.Speed <= 55)
                    dis = dis/3;
                else if (wayPoint.Speed > 55)
                    dis = dis/4;
                List<ElementDistance<RoadLeaf>> elementDistances =
                    _routing.GetKNearestRoads(wayPoint.Pos.X, wayPoint.Pos.Y, dis);
                if (elementDistances.Count == 0)
                {
                    while (elementDistances.Count == 0)
                    {
                        dis *= 2;
                        elementDistances = _routing.GetKNearestRoads(wayPoint.Pos.X, wayPoint.Pos.Y, dis);
                    }
                }
                //if (elementDistances.Count > MaxStateQuantity)
                //{
                //    elementDistances = elementDistances.GetRange(0, MaxStateQuantity);
                //}
                double nearestDis = 100;
                foreach (ElementDistance<RoadLeaf> elementDistance in elementDistances)
                {
                    NeighborRoad road = new NeighborRoad(elementDistance.Element.Arc1, wayPoint.Pos)
                    {
                        Index = wayPointCounter++,
                        RefWayPoint = wayPoint,
                        IsOneway = true
                    };
                    if (road.DistanceToRefPoint < nearestDis)
                        nearestDis = road.DistanceToRefPoint;
                    RoadSegments[wayPoint].Add(road);
                    if (elementDistance.Element.IsOneWay) continue;
                    road.ReversedArc = elementDistance.Element.Arc2;
                    road.IsOneway = false;
                }
                if(nearestDis< ValidPointDistance)
                    filteredObservation.Add(wayPoint);
            }
            ObservationWayPoints = filteredObservation;
            //calculate initial prob
            foreach (NeighborRoad neighborRoad in RoadSegments[ObservationWayPoints[0]])
            {
                neighborRoad.MaxProbability = Emission(neighborRoad);
            }
            //calculate V for states of each waypoint.
            for (int i = 1; i < ObservationWayPoints.Count; i++)
            {
                foreach (NeighborRoad thisRoad in RoadSegments[ObservationWayPoints[i]])
                {
                    foreach (NeighborRoad prevRoad in RoadSegments[ObservationWayPoints[i - 1]])
                    {
                        double arcDistance;
                        double routeDistance;
                        Arc[] arcs;
                        List<Node> route;
                        double probability = prevRoad.MaxProbability*
                                             Transition(prevRoad, thisRoad,
                                                 out arcDistance, out routeDistance, out route, out arcs)*
                                             Emission(thisRoad);
                        if (probability < thisRoad.MaxProbability) continue;
                        thisRoad.MaxProbability = probability;
                        thisRoad.MostPossibleAncient = prevRoad;
                        thisRoad.ArcDistanceToAncient = arcDistance;
                        thisRoad.RouteDistanceToAncient = routeDistance;
                        thisRoad.RouteToAncient = route;
                        thisRoad.ArcToAncient = arcs;
                    }
                }
            }
            //from highest V, look back to get route.
            string x = "";
            _resultList = new List<NeighborRoad>();
            NeighborRoad roadTraveler = RoadSegments[ObservationWayPoints[ObservationWayPoints.Count - 1]].Max();
            while (roadTraveler != null)
            {
                x += roadTraveler.Index + "->";
                _resultList.Add(roadTraveler);
                roadTraveler = roadTraveler.MostPossibleAncient;
            }
            _resultList.Reverse();
            //_resultList = matchedRoute;
            _logger.WriteLog("Viterbi in " + ((DateTime.Now - startTime).TotalMilliseconds/1000.0).ToString("0.000") +
                             "s");
            _logger.WriteStats("\t" + ((DateTime.Now - startTime).TotalMilliseconds/1000.0).ToString("0.000"));

            //_logger.WriteLog("Sigma:" + EstimateSigma() + ", Beta:" + EstimateBeta());
            _routing.CleanTempNodes();
        }

        private double Emission(NeighborRoad road)
        {
            double temp = road.DistanceToRefPoint/_sigma;
            return Math.Exp(-0.5*temp*temp)/SigmaMultiSqrt2Pi;
        }

        private double Transition(NeighborRoad road1, NeighborRoad road2, out double arcDistance,
            out double routeDistance, out List<Node> route, out Arc[] arcs)
        {
            if (road1.ProjPoint == road2.ProjPoint)
            {
                routeDistance = 0;
                arcs = new Arc[0];
                route = new List<Node>();
            }
            else
            {
                route = _routing.FindRoute(road1, road2, out routeDistance, out arcs);
            }
            arcDistance = Geometry.Haversine(road1.RefPoint, road2.RefPoint);

            double dt = Math.Abs(arcDistance - routeDistance);
            return Math.Exp(-dt/_beta)/_beta;
        }

        private MatchedRoute GetRoadList(bool isTest = false)
        {
            _matchedRoute = new MatchedRoute();
            Calculate(isTest);
            Arc mostRecentArc = _resultList[0].RoadArc;
            _matchedRoute.RoadList.Add(new MatchedSegment(_resultList[0].RoadArc));

            //if (_resultList[0].ProjPosition == SegmentPart.End)
            //{
            //    _matchedRoute.StartPercent = "1";
            //}
            //else if (_resultList[0].ProjPosition == SegmentPart.Start)
            //{
            //    _matchedRoute.StartPercent = "0";
            //}
            //else //(_resultList[0].ProjPosition == SegmentPart.Middle)
            //{
            //    _matchedRoute.StartPercent =
            //        _resultList[0].RoadArc.GetScaleByPoint(_resultList[0].ProjPoint).ToString("0.0000");
            //}
            //_matchedRoute.EndPercent = "1";
            _invalidSegments = "[";
            for (int i = 1; i < _resultList.Count; i++)
            {
                double diffPercent = _resultList[i].RouteDistanceToAncient/_resultList[i].ArcDistanceToAncient;
                //_logger.WriteLog("Diff percent:" + diffPercent);

                foreach (Arc arc in _resultList[i].ArcToAncient)
                {
                    if (arc != mostRecentArc)
                    {
                        _matchedRoute.AppendSegByArc(arc);
                        mostRecentArc = arc;
                    }
                }
                if (i == _resultList.Count - 1)
                {
                    if (_resultList[i].RoadArc != mostRecentArc)
                    {
                        _matchedRoute.AppendSegByArc(_resultList[i].RoadArc);
                        mostRecentArc = _resultList[i].RoadArc;
                    }
                    //if (_resultList[i].ProjPosition == SegmentPart.Middle)
                    //    _matchedRoute.EndPercent =
                    //        _resultList[i].RoadArc.GetScaleByPoint(_resultList[i].ProjPoint).ToString("0.0000");
                }

                if (diffPercent > _percent)
                {
                    //if (_resultList[i].RoadArc != mostRecentArc)
                    //{
                    //    _invalidSegments += "[[" + _finalRoute[_finalRoute.Count - 2].X + "," +
                    //                        _finalRoute[_finalRoute.Count - 2].Y + "],[" +
                    //                        _finalRoute[_finalRoute.Count - 1].X + "," +
                    //                        _finalRoute[_finalRoute.Count - 1].Y + "]],";
                    //}
                }
            }
            _invalidSegments = _invalidSegments.TrimEnd(',') + "]";
            return _matchedRoute;
        }

        public string GetRoadListJson()
        {
            GetRoadList();
            string route = "[";
            route += "[" + _matchedRoute.RoadList[0].StartNode.X + "," + _matchedRoute.RoadList[0].StartNode.Y + "]";
            for (int i = 0; i < _matchedRoute.RoadList.Count; i++)
            {
                route += ",[" + _matchedRoute.RoadList[i].EndNode.X + "," + _matchedRoute.RoadList[i].EndNode.Y + "]";
            }
            route += "]";
            return "({'route':" + route + ",'invalid':" + _invalidSegments + "})";
            //return "({'route':" + route + ",'invalid':" + _invalidSegments + ",start_percent:" +
            //       _matchedRoute.StartPercent + ",end_percent:" + _matchedRoute.EndPercent + "})";
        }

        private void GetRoute(bool isTest = false)
        {
            Calculate(isTest);
            _finalRoute = new List<Node>();
            Node mostRecentNode;
            if (_resultList[0].ProjPosition == SegmentPart.End)
                mostRecentNode = _resultList[0].RoadArc.EndNode;
            //_finalRoute.Add(_resultList[0].RoadArc.EndNode);
            else if (_resultList[0].ProjPosition == SegmentPart.Start)
                mostRecentNode = _resultList[0].RoadArc.StartNode;
            //_finalRoute.Add(_resultList[0].RoadArc.StartNode);
            else //(_resultList[0].ProjPosition == SegmentPart.Middle)
                mostRecentNode = new Node(0, _resultList[0].ProjPoint);
            //_finalRoute.Add(new Node(0, _resultList[0].ProjPoint));
            _finalRoute.Add(mostRecentNode);
            //_fixedWayPoints = new List<WayPoint>();
            _invalidSegments = "[";
            //double diffMeterMax = double.MinValue;
            //double percentAtMaxMeter = 0;
            //double diffPercentMax = double.MinValue;
            //double meterAtMaxPercent = 0;
            for (int i = 1; i < _resultList.Count; i++)
            {
                double diffPercent = _resultList[i].RouteDistanceToAncient/_resultList[i].ArcDistanceToAncient;
                //_logger.WriteLog("Diff percent:" + diffPercent);
                if (diffPercent < _percent)
                {
                    foreach (Node node in _resultList[i].RouteToAncient)
                    {
                        if (node != mostRecentNode)
                        {
                            mostRecentNode = node;
                            _finalRoute.Add(node);
                        }
                    }
                    if (i == _resultList.Count - 1 && _resultList[i].ProjPosition == SegmentPart.Middle)
                    {
                        _finalRoute.Add(new Node(0, _resultList[i].ProjPoint));
                    }
                }
                else
                {
                    if (_resultList[i].ProjNode != mostRecentNode)
                    {
                        mostRecentNode = new Node(0, _resultList[i].ProjPoint);
                        _finalRoute.Add(mostRecentNode);
                        _invalidSegments += "[[" + _finalRoute[_finalRoute.Count - 2].X + "," +
                                            _finalRoute[_finalRoute.Count - 2].Y + "],[" +
                                            _finalRoute[_finalRoute.Count - 1].X + "," +
                                            _finalRoute[_finalRoute.Count - 1].Y + "]],";
                    }
                }
            }
            _invalidSegments = _invalidSegments.TrimEnd(',') + "]";
        }

        public string GetRouteJson()
        {
            GetRoute();
            string route = "[";
            route += "[" + _finalRoute[0].X + "," + _finalRoute[0].Y + "]";
            for (int i = 1; i < _finalRoute.Count; i++)
            {
                route += ",[" + _finalRoute[i].X + "," + _finalRoute[i].Y + "]";
            }
            route += "]";
            return "({'route':" + route + ",'invalid':" + _invalidSegments + "})";
        }

        private LineSegment BuildRoadQueue()
        {
            LineSegment currentSegment = null;
            LineSegment firstSegment = null;
            for (int i = 1; i < _finalRoute.Count; i++)
            {
                LineSegment newSegment = new LineSegment(
                    new Point2D(_finalRoute[i - 1].X, _finalRoute[i - 1].Y),
                    new Point2D(_finalRoute[i].X, _finalRoute[i].Y));
                //if (_finalRoute[i - 1].Id == 0) newSegment.IsValid = false;
                if (currentSegment != null)
                {
                    currentSegment.Next = newSegment;
                    newSegment.Prev = currentSegment;
                }
                else
                    firstSegment = newSegment;
                currentSegment = newSegment;
            }
            return firstSegment;
        }

        private void GetNearestWay(Point2D point, ref LineSegment currentSeg, out Point2D projPoint, out double distance)
        {
            SegmentPart projPosition;
            //projPoint = Point2D.ProjectOnLine(point, currentSeg.P1, currentSeg.P2, out ProjPosition);
            //distance = Geometry.Haversine(point, projPoint);

            int searchDepth = 5;
            LineSegment[] segments = new LineSegment[searchDepth];
            Point2D[] points = new Point2D[searchDepth];
            double[] distances = new double[searchDepth];
            int minId = 0;
            double minDistance = 0;
            for (int i = 0; i < searchDepth; i++)
            {
                segments[i] = currentSeg;
                points[i] = Point2D.ProjectOnLine(point, segments[i].P1, segments[i].P2,
                    out projPosition);
                distances[i] = Geometry.Haversine(point, points[i]);
                if (i == 0)
                {
                    minDistance = distances[0];
                }
                else
                {
                    if (distances[i] < minDistance)
                    {
                        minDistance = distances[i];
                        minId = i;
                    }
                }
                if (currentSeg.Next == null) break;
                currentSeg = currentSeg.Next;
            }
            currentSeg = segments[minId];
            projPoint = points[minId];
            distance = minDistance;
        }

        public void GetFixedWayPoint(bool isTest = false)
        {
            //If distence to matched route<4, then use it. 
            //Else spread invalid points by their distence interval between valid points. 
            GetRoute(isTest);
            //_fixedWayPoints = new List<WayPoint>();
            //build temp search queue.
            LineSegment firstSegment = BuildRoadQueue();

            //SegmentPart ProjPosition;
            WayPoint startPoint = CleanedWayPoints[0];
            LineSegment startSegment = firstSegment;
            LineSegment currentSegment = firstSegment;

            for (int i = 0; i < CleanedWayPoints.Count; i++)
            {
                double distance;
                Point2D projPoint;
                GetNearestWay(CleanedWayPoints[i].Pos, ref currentSegment, out projPoint, out distance);
                LineSegment road = currentSegment;
                bool isFirstPoint = i == 0;
                if (distance < ValidPointDistance)
                {
                    //startPoint = new WayPoint(CleanedWayPoints[i].VideoTime, projPoint, CleanedWayPoints[i].Speed,
                    //    CleanedWayPoints[i].Altitude);
                    //_fixedWayPoints.Add(startPoint);
                    CleanedWayPoints[i].FixedPos = projPoint;
                    startPoint = CleanedWayPoints[i];
                    startSegment = road;
                }
                else
                {
                    List<WayPoint> farPoints = new List<WayPoint>();
                    double maxDistance = 0;
                    while (distance >= ValidPointDistance && i < CleanedWayPoints.Count - 1)
                    {
                        farPoints.Add(CleanedWayPoints[i]);
                        i++;
                        GetNearestWay(CleanedWayPoints[i].Pos, ref currentSegment, out projPoint, out distance);
                        if (distance > maxDistance) maxDistance = distance;
                        road = currentSegment;
                    }
                    if (isFirstPoint)
                    {
                        farPoints.RemoveAt(0);
                        //_fixedWayPoints.Add(CleanedWayPoints[0]);
                        CleanedWayPoints[0].FixedPos = CleanedWayPoints[0].Pos;
                        startSegment = new LineSegment(CleanedWayPoints[0].Pos, projPoint);
                    }
                    if (maxDistance < ValidPointThreshold)
                    {
                        //spread invalid point to segments from startPoint to current projPoint, 
                        //and from startSegment to current road.
                        //Speed
                        float totalSpeed = startPoint.Speed;
                        List<float> speeds = new List<float> {startPoint.Speed};
                        foreach (WayPoint farPoint in farPoints)
                        {
                            totalSpeed += farPoint.Speed;
                            speeds.Add(farPoint.Speed);
                        }
                        //LengthEllipsoid
                        double totalLength;
                        if (isFirstPoint)
                        {
                            totalLength = startSegment.Length;
                        }
                        else if (startSegment == road)
                        {
                            totalLength = Point2D.DistanceBetween(startPoint.FixedPos, projPoint);
                            if (totalLength == 0 && i == CleanedWayPoints.Count - 1)
                            {
                                totalLength = Point2D.DistanceBetween(projPoint, CleanedWayPoints[i].Pos);
                                LineSegment toEnd = new LineSegment(projPoint, CleanedWayPoints[i].Pos);
                                startSegment.Next = toEnd;
                                toEnd.Prev = startSegment;
                            }
                        }
                        else
                        {
                            totalLength = Point2D.DistanceBetween(startPoint.FixedPos, startSegment.P2);
                            LineSegment tempSeg = startSegment.Next;
                            while (tempSeg != road && tempSeg != null)
                            {
                                totalLength += tempSeg.Length;
                                tempSeg = tempSeg.Next;
                            }
                            if (tempSeg != null)
                                totalLength += Point2D.DistanceBetween(tempSeg.P1, projPoint);
                        }
                        //Projection
                        Point2D tempPos = startPoint.FixedPos;
                        for (int j = 0; j < farPoints.Count; j++)
                        {
                            double length = totalLength*speeds[j]/totalSpeed;
                            tempPos = GetProjectPoint(tempPos, length, ref startSegment);

                            //tempPos = p;
                            //WayPoint tempWayPoint = new WayPoint(farPoints[j].VideoTime, p, farPoints[j].Speed,
                            //    farPoints[j].Altitude);
                            //_fixedWayPoints.Add(tempWayPoint);
                            farPoints[j].FixedPos = tempPos;

                        }
                    }
                    else
                    {
                        //these points should be removed from final route.
                        for (int j = 0; j < farPoints.Count; j++)
                        {
                            //WayPoint tempWayPoint = new WayPoint(farPoints[j].VideoTime, farPoints[j].Pos, farPoints[j].Speed,
                            //    farPoints[j].Altitude);
                            //_fixedWayPoints.Add(tempWayPoint);

                            farPoints[j].FixedPos = farPoints[j].Pos;
                        }
                    }
                    startPoint = CleanedWayPoints[i];
                    CleanedWayPoints[i].FixedPos = projPoint;
                    //startPoint = new WayPoint(CleanedWayPoints[i].VideoTime, projPoint, CleanedWayPoints[i].Speed,
                    //    CleanedWayPoints[i].Altitude);
                    //_fixedWayPoints.Add(startPoint);
                    startSegment = road;
                }
            }
        }

        private void GetNearestWay(Point2D point, ref MatchedSegment currentSeg, out Point2D projPoint, out double distance, out SegmentPart segmentPart)
        {
            
            //projPoint = Point2D.ProjectOnLine(point, currentSeg.P1, currentSeg.P2, out ProjPosition);
            //distance = Geometry.Haversine(point, projPoint);

            int searchDepth = 15;
            MatchedSegment[] segments = new MatchedSegment[searchDepth];
            Point2D[] points = new Point2D[searchDepth];
            double[] distances = new double[searchDepth];
            SegmentPart[] projPosition = new SegmentPart[searchDepth];
            int minId = 0;
            double minDistance = 0;
            for (int i = 0; i < searchDepth; i++)
            {
                segments[i] = currentSeg;
                points[i] = Point2D.ProjectOnLine(point, segments[i].P1, segments[i].P2,
                    out projPosition[i]);
                distances[i] = Geometry.Haversine(point, points[i]);
                if (i == 0)
                {
                    minDistance = distances[0];
                }
                else
                {
                    if (distances[i] < minDistance)
                    {
                        minDistance = distances[i];
                        minId = i;
                    }
                }
                if (currentSeg.Next == null) break;
                currentSeg = currentSeg.Next;
            }
            currentSeg = segments[minId];
            projPoint = points[minId];
            distance = minDistance;
            segmentPart = projPosition[minId];
        }
        
        public MatchedRoute GetMatchedRoute(bool isTest = false)
        {
            DateTime start = DateTime.Now;
            //If distence to matched route<4, then use it. 
            //Else spread invalid points by their distence interval between valid points. 
            GetRoadList(isTest);
            
            //SegmentPart ProjPosition;
            WayPoint startPoint=null;
            int startPointId = 0;
            int endPointId = 0;
            MatchedSegment startSegment = _matchedRoute.Head;
            MatchedSegment currentSegment = _matchedRoute.Head;
            MatchedSegment segmentTraveler;
            //pass all invalid points at beginning, find first trustable point
            for (int i = 0; i < CleanedWayPoints.Count; i++)
            {
                double distance;
                Point2D projPoint;
                GetNearestWay(CleanedWayPoints[i].Pos, ref currentSegment, out projPoint, out distance,out CleanedWayPoints[i].ProjectPosition);
                if (!(distance < ValidPointDistance))
                {
                    CleanedWayPoints[i].IsValid = false;
                    continue;
                }
                startPoint = CleanedWayPoints[i];
                startPoint.FixedPos = projPoint;
                startPointId = i;
                startSegment = currentSegment;
                startSegment.AddWayPoint(startPoint);
                startPoint.SegmentMatched = startSegment;
                break;
            }
            if (startPoint == null)
            {
                //nearly impossible, empty output.
            }
            else
            {
                for (int i = startPointId + 1; i < CleanedWayPoints.Count; i++)
                {
                    double distance;
                    Point2D projPoint;
                    GetNearestWay(CleanedWayPoints[i].Pos, ref currentSegment, out projPoint, out distance, out CleanedWayPoints[i].ProjectPosition);

                    if (distance < ValidPointDistance)  
                    {
                        // Fix the valid point's position
                        CleanedWayPoints[i].FixedPos = projPoint;
                        startPoint = CleanedWayPoints[i];
                        endPointId = i;
                        startSegment = currentSegment;
                        currentSegment.AddWayPoint(CleanedWayPoints[i]);
                        CleanedWayPoints[i].SegmentMatched = currentSegment;
                    }
                    else
                    {
                        // Collect all invalid points
                        List<WayPoint> farPoints = new List<WayPoint>();
                        double maxDistance = 0;
                        while (distance >= ValidPointDistance && i < CleanedWayPoints.Count - 1)
                        {
                            farPoints.Add(CleanedWayPoints[i]);
                            i++;
                            GetNearestWay(CleanedWayPoints[i].Pos, ref currentSegment, out projPoint, out distance, out CleanedWayPoints[i].ProjectPosition);
                            if (distance > maxDistance) maxDistance = distance;
                        }
                        if (i == CleanedWayPoints.Count - 1)
                        {
                            // Throw away all invalid points at end
                            foreach (WayPoint wayPoint in farPoints)
                            {
                                wayPoint.IsValid = false;
                            }
                            break;
                        }
                        endPointId = i;
                        // So now both startPoint and CleanedWayPoints[i] are valid, all points between them are invalid.
                        //  startPoint              CleanedWayPoints[i]
                        //       |                          |
                        // |------------|    ...     |--------------|
                        //  startSegment              currentSegment

                        // If the invalid points are fixable, fix them
                        if (maxDistance < ValidPointThreshold)
                        {
                            // Spread invalid points to segments from startPoint to current projPoint, 
                            //and from startSegment to current road.
                            //Speed
                            float totalSpeed = startPoint.Speed;
                            List<float> speeds = new List<float> {startPoint.Speed};
                            foreach (WayPoint farPoint in farPoints)
                            {
                                totalSpeed += farPoint.Speed;
                                speeds.Add(farPoint.Speed);
                            }
                            //LengthEllipsoid
                            double totalLength;
                            if (startSegment == currentSegment)
                                totalLength = Point2D.DistanceBetween(startPoint.FixedPos, projPoint);
                            else
                            {
                                totalLength = Point2D.DistanceBetween(startPoint.FixedPos, startSegment.P2);
                                segmentTraveler = startSegment.Next;
                                while (segmentTraveler != currentSegment && segmentTraveler != null)
                                {
                                    totalLength += segmentTraveler.Length;
                                    segmentTraveler = segmentTraveler.Next;
                                }
                                if (segmentTraveler != null)
                                    totalLength += Point2D.DistanceBetween(segmentTraveler.P1, projPoint);
                            }
                            //Projection
                            Point2D tempPos = startPoint.FixedPos;
                            segmentTraveler = startSegment;
                            for (int j = 0; j < farPoints.Count; j++)
                            {
                                double length = totalLength*speeds[j]/totalSpeed;
                                tempPos = GetProjectPoint(tempPos, length, ref segmentTraveler);
                                farPoints[j].FixedPos = tempPos;
                                segmentTraveler.AddWayPoint(farPoints[j]);
                                farPoints[j].SegmentMatched = segmentTraveler;
                            }
                        }
                        else
                        {
                            // These non-fixable points should be removed from final route.
                            for (int j = 0; j < farPoints.Count; j++)
                            {
                                farPoints[j].IsValid = false;
                            }
                        }
                        startPoint = CleanedWayPoints[i];
                        CleanedWayPoints[i].FixedPos = projPoint;
                        CleanedWayPoints[i].SegmentMatched = currentSegment;
                        currentSegment.AddWayPoint(CleanedWayPoints[i]);
                        startSegment = currentSegment;
                    }
                }
            }

            //int partitionStartId = startPointId;
            segmentTraveler = null;
            bool isInPartition = false;
            //validA and validB are two neighboring valid points 
            WayPoint validA= CleanedWayPoints[startPointId];
            for (int i = startPointId; i <= endPointId; i++)
            {
                if (isInPartition)
                {
                    if (CleanedWayPoints[i].IsValid)
                    {
                        //fix gap between segments, and calculate their start/end time
                        if (segmentTraveler != CleanedWayPoints[i].SegmentMatched)
                        {
                            WayPoint validB = CleanedWayPoints[i];
                            List<MatchedSegment> tempSegments=new List<MatchedSegment>();
                            //lenth = (A to segEnd) + (conn segs) + (segStart to B)
                            double totalLength = 0;
                            double accumulatedLength = 0;
                            double initialSeconds = (validA.Time - _startTime).TotalSeconds;
                            if (validA.ProjectPosition == SegmentPart.Start)
                                accumulatedLength = validA.SegmentMatched.Length;
                            else if (validA.ProjectPosition == SegmentPart.End)
                                accumulatedLength = 0;
                            else
                                accumulatedLength = Point2D.DistanceBetween(validA.FixedPos, validA.SegmentMatched.P2);
                            totalLength = accumulatedLength;

                            //find all connection segments.
                            segmentTraveler = segmentTraveler.Next;
                            while (segmentTraveler != CleanedWayPoints[i].SegmentMatched)
                            {
                                tempSegments.Add(segmentTraveler);
                                _matchedRoute.ValidRoadList.Add(segmentTraveler);
                                totalLength += segmentTraveler.Length;
                                segmentTraveler = segmentTraveler.Next;
                            }
                            _matchedRoute.ValidRoadList.Add(validB.SegmentMatched);

                            if (validB.ProjectPosition == SegmentPart.Start)
                                totalLength += 0;
                            else if (validB.ProjectPosition == SegmentPart.End)
                                totalLength += validB.SegmentMatched.Length;
                            else
                                totalLength += Point2D.DistanceBetween(validB.SegmentMatched.P1, validB.FixedPos);

                            double velocity = totalLength/(validB.Time - validA.Time).TotalSeconds;

                            // calculate connection segs start/end
                            foreach (MatchedSegment matchedSegment in tempSegments)
                            {
                                //this.startTime = prev.endTime = accumulatedLength / velocity
                                matchedSegment.Prev.EndPercent = "1";
                                matchedSegment.Prev.EndSecond = initialSeconds+accumulatedLength / velocity;
                                matchedSegment.StartPercent = "0";
                                matchedSegment.StartSecond = matchedSegment.Prev.EndSecond;
                                accumulatedLength += matchedSegment.Length;
                            }
                            validB.SegmentMatched.Prev.EndPercent = "1";
                            validB.SegmentMatched.Prev.EndSecond = initialSeconds+accumulatedLength / velocity;
                            validB.SegmentMatched.StartPercent = "0";
                            validB.SegmentMatched.StartSecond = validB.SegmentMatched.Prev.EndSecond;
                            
                        }
                        else
                        {
                            validA = CleanedWayPoints[i];
                            validA.SegmentMatched.EndPercent = validA.SegmentMatched.RoadSegment.GetScaleByPoint(validA.FixedPos).ToString("0.0000");
                            validA.SegmentMatched.EndSecond = (validA.Time - _startTime).TotalSeconds;
                        }
                    }
                    else
                    {
                        //close the partition 
                        isInPartition = false;
                        validA.SegmentMatched.EndPercent = validA.SegmentMatched.RoadSegment.GetScaleByPoint(validA.FixedPos).ToString("0.0000");
                        validA.SegmentMatched.EndSecond = (validA.Time-_startTime).TotalSeconds;
                    }
                }
                else
                {
                    if (CleanedWayPoints[i].IsValid)
                    {
                        //Open a new partition
                        isInPartition = true;
                        validA = CleanedWayPoints[i];
                        validA.SegmentMatched.StartSecond = (validA.Time - _startTime).TotalSeconds;
                        if (validA.ProjectPosition == SegmentPart.Start)
                            validA.SegmentMatched.StartPercent = "0";
                        else if (validA.ProjectPosition == SegmentPart.End)
                            validA.SegmentMatched.StartPercent = "1";
                        else
                            validA.SegmentMatched.StartPercent = validA.SegmentMatched.RoadSegment.GetScaleByPoint(validA.FixedPos).ToString("0.0000");
                        segmentTraveler = CleanedWayPoints[i].SegmentMatched;
                        _matchedRoute.ValidRoadList.Add(segmentTraveler);
                    }
                    else
                    {
                        //just pass the invalid points
                    }
                }
            }
            _matchedRoute.TotalTime=(DateTime.Now - start).TotalSeconds;
            return _matchedRoute;
        }

        public string GetFixedJson2()
        {
            GetMatchedRoute();

            return "({route:" + _matchedRoute.GetRouteString() + ",conn:" + _matchedRoute.GetConnString() + ",invalid:" +
                   _invalidSegments + "})";
        }

        public void ParseIntoDataBase(string routeId)
        {
            GetMatchedRoute();
            _matchedRoute.UpdateToDatabase(routeId);
        }

        private static Point2D GetProjectPoint(Point2D p, double length, ref MatchedSegment seg)
        {
            double lengthLeft = length;
            double len;
            Point2D tempP = p;
            while ((len = Point2D.DistanceBetween(tempP, seg.P2)) < lengthLeft)
            {
                lengthLeft = lengthLeft - len;
                if (seg.Next == null)
                    return seg.P2;
                seg = seg.Next;
                tempP = seg.P1;
            }
            return seg.GetPointByLength(tempP, lengthLeft);
        }

        private static Point2D GetProjectPoint(Point2D p, double length, ref LineSegment seg)
        {
            double lengthLeft = length;
            double len;
            Point2D tempP = p;
            while ((len = Point2D.DistanceBetween(tempP, seg.P2)) < lengthLeft)
            {
                lengthLeft = lengthLeft - len;
                if (seg.Next == null)
                    return seg.P2;
                seg = seg.Next;
                tempP = seg.P1;
            }
            return seg.GetPointByLength(tempP, lengthLeft);
        }

        public Bitmap GetRouteImage(bool isTest = false)
        {
            GetFixedWayPoint(isTest);
            double up = -90, down = 90, left = 180, right = -180;
            for (int i = 0; i < CleanedWayPoints.Count; i++)
            {
                double minLat = Math.Min(CleanedWayPoints[i].Pos.X, CleanedWayPoints[i].FixedPos.X);
                double maxLat = Math.Max(CleanedWayPoints[i].Pos.X, CleanedWayPoints[i].FixedPos.X);
                double minLon = Math.Min(CleanedWayPoints[i].Pos.Y, CleanedWayPoints[i].FixedPos.Y);
                double maxLon = Math.Max(CleanedWayPoints[i].Pos.Y, CleanedWayPoints[i].FixedPos.Y);
                if (minLon < left) left = minLon;
                if (maxLon > right) right = maxLon;
                if (maxLat > up) up = maxLat;
                if (minLat < down) down = minLat;
            }
            Transform trans = new Transform(left, right, up, down, 1920, 1080);
            Bitmap myImg = new Bitmap(trans.Width, trans.Height);

            Graphics objGraphics = Graphics.FromImage(myImg);
            objGraphics.Clear(Color.White);
            for (int i = 1; i < CleanedWayPoints.Count; i++)
            {

                //connection
                Point start = trans.ToGraph(CleanedWayPoints[i].Pos.X, CleanedWayPoints[i].Pos.Y);
                Point end = trans.ToGraph(CleanedWayPoints[i].FixedPos.X, CleanedWayPoints[i].FixedPos.Y);
                objGraphics.DrawLine(new Pen(Color.Blue, 1), start, end);
                //fixed line
                start = trans.ToGraph(CleanedWayPoints[i - 1].FixedPos.X, CleanedWayPoints[i - 1].FixedPos.Y);
                end = trans.ToGraph(CleanedWayPoints[i].FixedPos.X, CleanedWayPoints[i].FixedPos.Y);
                objGraphics.DrawLine(new Pen(Color.Green, 2), start, end);
                //raw line
                start = trans.ToGraph(CleanedWayPoints[i - 1].Pos.X, CleanedWayPoints[i - 1].Pos.Y);
                end = trans.ToGraph(CleanedWayPoints[i].Pos.X, CleanedWayPoints[i].Pos.Y);
                objGraphics.DrawLine(new Pen(Color.Red, 2), start, end);
            }
            return myImg;
            //myImg.Save(MemStream, ImageFormat.Png);
        }

        public string GetFixedJson()
        {
            GetFixedWayPoint();
            //_fixedWayPoints.Add(RawWayPoints[0]);

            string route = "[";
            string conn = "[";
            for (int i = 1; i < CleanedWayPoints.Count; i++)
            {
                route += "{seg:";
                route += "[[" + CleanedWayPoints[i - 1].FixedPos.X + "," + CleanedWayPoints[i - 1].FixedPos.Y +
                         "],[" + CleanedWayPoints[i].FixedPos.X + "," + CleanedWayPoints[i].FixedPos.Y + "]],";
                route += "speed:" + (CleanedWayPoints[i - 1].Speed + CleanedWayPoints[i].Speed)/2;
                route += "},";
                conn += "[[" + CleanedWayPoints[i].Pos.X + "," + CleanedWayPoints[i].Pos.Y + "],[" +
                        CleanedWayPoints[i].FixedPos.X + "," + CleanedWayPoints[i].FixedPos.Y + "]],";
            }
            route = route.TrimEnd(',') + "]";
            conn = conn.TrimEnd(',') + "]";
            return "({route:" + route + ",conn:" + conn + ",invalid:" + _invalidSegments + "})";
        }

        public string GetDebugJson()
        {
            Calculate();
            string route = "[";
            string conn = "[[[" + _resultList[0].ProjPoint.X + "," + _resultList[0].ProjPoint.Y + "],[" +
                          _resultList[0].RefPoint.X + "," + _resultList[0].RefPoint.Y + "]]";
            route += "[" + _resultList[0].ProjPoint.X + "," + _resultList[0].ProjPoint.Y + "]";
            for (int i = 1; i < _resultList.Count; i++)
            {
                route += ",[" + _resultList[i].ProjPoint.X + "," + _resultList[i].ProjPoint.Y + "]";
                conn += ",[[" + _resultList[i].ProjPoint.X + "," + _resultList[i].ProjPoint.Y + "],[" +
                        _resultList[i].RefPoint.X + "," + _resultList[i].RefPoint.Y + "]]";
            }
            route += "]";
            conn += "]";
            return "({route:" + route + ",conn:" + conn + "})";
        }

        private double EstimateSigma()
        {
            List<double> distances = new List<double>();
            foreach (NeighborRoad neighborRoad in _resultList)
            {
                distances.Add(neighborRoad.DistanceToRefPoint);
            }
            double sigma = Mathmatic.MedianDeviationOfTheMedian(distances);
            sigma *= 1.4826; //radian to degree
            return sigma;
        }

        private double EstimateBeta()
        {
            List<double> distances = new List<double>();
            for (int i = 1; i < _resultList.Count; i++)
            {
                distances.Add(Math.Abs(_resultList[i].ArcDistanceToAncient - _resultList[i].RouteDistanceToAncient));
            }
            double beta = Mathmatic.MedianDeviationOfTheMedian(distances);
            return beta/Math.Log(2);
        }

        /// <summary>
        /// The raw points include invalid data.
        /// The cleaned points also delete all the low speed data.
        /// </summary>
        /// <param name="isTest">Test is for batch test output</param>
        private void CleanWayPoints(bool isTest)
        {
            WayPoint firstRawPoint = RawWayPoints[0];
            WayPoint lastRawPoint = RawWayPoints[RawWayPoints.Count - 1];
            //remove initial and ending points with same lat/lon.
            int firstValidId = 0;
            for (int i = 1; i < RawWayPoints.Count; i++)
            {
                if (!firstRawPoint.Pos.Equals(RawWayPoints[i].Pos)&& RawWayPoints[i].Speed>0)
                    break;
                RawWayPoints[i - 1].IsValid = false;
                firstValidId = i;
            }
            int lastValidId = RawWayPoints.Count - 1;
            for (int i = RawWayPoints.Count - 2; i > 0; i--)
            {
                if (!lastRawPoint.Pos.Equals(RawWayPoints[i].Pos) && RawWayPoints[i].Speed > 0)
                    break;
                lastValidId = i;
            }
            
            CleanedWayPoints = new List<WayPoint>();
            //combine other points with speed<=3 . (red light/stop sign)
            for (int i = firstValidId; i <= lastValidId; i++)
            {
                if (RawWayPoints[i].Speed <= LowSpeedThreshold)
                {
                    while (i <= lastValidId && RawWayPoints[i].Speed <= LowSpeedThreshold)
                    {
                        RawWayPoints[i].IsValid = false;
                        i++;
                    }
                }
                else
                    CleanedWayPoints.Add(RawWayPoints[i]);
            }

            // Filter points by distance gap, and keep turning points
            int currentPoint = 0;
            int nextPoint = 1;
            ObservationWayPoints.Add(CleanedWayPoints[0]);
            double bearMax = double.MinValue;
            double bearMin = double.MaxValue;
            if (!isTest)
            {
                for (int i = 1; i < CleanedWayPoints.Count; i++)
                {
                    double bearing = Geometry.Bearing(CleanedWayPoints[i - 1].Pos, CleanedWayPoints[i].Pos);
                    if (bearing > bearMax) bearMax = bearing;
                    if (bearing < bearMin) bearMin = bearing;
                    double dis;
                    if (((dis = Geometry.Haversine(CleanedWayPoints[i].Pos, CleanedWayPoints[currentPoint].Pos)) >
                         _interval ||
                         (Geometry.BearingDiff(CleanedWayPoints[currentPoint].Pos, CleanedWayPoints[nextPoint].Pos,
                             CleanedWayPoints[i - 1].Pos, CleanedWayPoints[i].Pos) > TurnAngleThreshold &&
                          dis > MinDisForAngleFilter)) && //remove points with turn angle > 30 degree suddenly. 
                        MinDistToArc(CleanedWayPoints[i].Pos) < FilterSkipDistance)
                    {
                        _avgSpeed += CleanedWayPoints[currentPoint].Speed;
                        currentPoint = i;
                        if (i + 1 < CleanedWayPoints.Count)
                            nextPoint = i + 1;
                        if (!ObservationWayPoints.Contains(CleanedWayPoints[currentPoint]))
                            ObservationWayPoints.Add(CleanedWayPoints[currentPoint]);
                    }
                }
            }
            else //test
            {
                for (int i = 1; i < CleanedWayPoints.Count; i++)
                {
                    //double dis;
                    if (Geometry.Haversine(CleanedWayPoints[i].Pos, CleanedWayPoints[currentPoint].Pos) > 20)
                    {
                        _avgSpeed += CleanedWayPoints[currentPoint].Speed;
                        currentPoint = i;
                        if (!ObservationWayPoints.Contains(CleanedWayPoints[currentPoint]))
                            ObservationWayPoints.Add(CleanedWayPoints[currentPoint]);
                    }
                }
            }
            _avgSpeed /= ObservationWayPoints.Count;
            _logger.WriteStats("\t" + RawWayPoints.Count);
            _logger.WriteStats("\t" + ObservationWayPoints.Count);
            _logger.WriteLog("Average speed:" + _avgSpeed.ToString("0.0"));
            _logger.WriteStats("\t" + _avgSpeed.ToString("0.0"));
            _logger.WriteLog("Filtered points:" +
                             ((RawWayPoints.Count - ObservationWayPoints.Count)*100.0/RawWayPoints.Count).ToString("0.0") +
                             "%");
            _logger.WriteStats("\t" +
                               ((RawWayPoints.Count - ObservationWayPoints.Count)*100.0/RawWayPoints.Count).ToString(
                                   "0.0") + "%");
            if (!ObservationWayPoints.Contains(CleanedWayPoints[CleanedWayPoints.Count - 1]))
                ObservationWayPoints.Add(CleanedWayPoints[CleanedWayPoints.Count - 1]);
        }

        private double MinDistToArc(Point2D point)
        {
            List<ElementDistance<RoadLeaf>> x = _routing.GetKNearestRoads(point.X, point.Y, 0.0001);
            if (x.Count == 0) return double.MaxValue;
            SegmentPart position;
            Point2D projPoint = Point2D.ProjectOnLine(point, x[0].Element.Arc1, out position);
            return Geometry.Haversine(point, projPoint);
        }
    }
}