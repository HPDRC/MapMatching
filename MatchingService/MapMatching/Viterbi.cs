using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using MatchingService.Astar;
using MatchingService.MultiCore.Generic;
using MatchingService.rtree;
using MatchingService.Routing;
using MatchingService.SysTools;
using Point = System.Drawing.Point;

namespace MatchingService.MapMatching
{
    public class Viterbi
    {
        public List<WayPoint> RawWayPoints;
        public List<WayPoint> CleanedWayPoints;
        public List<int> FilteredIds; //Observations
        public Dictionary<int, List<NeighborRoad>> RoadSegments; //States
        private readonly RoutingMachine _routing;

        private readonly double _sigma=4.07;
        private readonly double _beta=5;
        private readonly double _distance=0.0003;
        private double SigmaMultiSqrt2Pi => _sigma*Math.Sqrt(Math.PI*2);
        private readonly double _interval=200;
        private readonly double _percent;
        private const int MaxStateQuantity = 15;  //We only keep this number of most possible road.
        private const double ValidPointDistance = 10;  //point is invalid outside 10 meter radius of the road while matching
        private const double ValidPointThreshold = 30;  //max distance for an invalid point to match
        private const double LowSpeedThreshold = 3; //Group the points together with speed lower than this mph.
        private const double TurnAngleThreshold = 30; //We keep the point when the turning degree greater than this value  
        private const double MinDisForAngleFilter = 7;  //To avoid many points while turning, we set a threshold
        private const double FilterSkipDistance = 10; //The point outside this range will be skipped.
        
        private List<NeighborRoad> _resultList;
        private readonly ILogger _logger;
        private List<Node> _finalRoute;
        private List<WayPoint> _fixedWayPoints;
        //private List<WayPoint> _secondRunPoints;
        private int _firstValidId;
        private int _lastValidId;
        private string _invalidSegments = "[]";
        private double _avgSpeed;

        public Viterbi(ILogger logger, RoutingMachine routing, List<WayPoint> rawWayPoints)
        {
            _logger = logger;
            _routing = routing;
            RawWayPoints = rawWayPoints;
            FilteredIds = new List<int>();
            RoadSegments = new Dictionary<int, List<NeighborRoad>>();
            //_sigma = logger.GetSigma();
            //_beta = logger.GetBeta();
            //_distance = logger.GetDistance();
            //_interval = logger.GetInterval();
            //_percent = logger.GetDiffPercent();
            //_sigmaMultiSqrt2Pi = _sigma*Math.Sqrt(Math.PI*2);
        }
        
        public Viterbi(ILogger logger, RoutingMachine routing, List<WayPoint> rawWayPoints, double sigma, double beta,double distance, double interval,double diff)
            :this(logger, routing, rawWayPoints)
        {
            _sigma = sigma;
            _beta = beta;
            _distance = distance;
            _interval = interval;
            _percent = diff;
        }


        private void Calculate(bool isTest=false)
        {
            CleanWayPoints(isTest);
            DateTime startTime = DateTime.Now;
            //get closest ways for each waypoint, as state
            int wayPointCounter = 0;

            for (int i = 0; i < FilteredIds.Count; i++)
            {
                RoadSegments[FilteredIds[i]] = new List<NeighborRoad>();
                double dis = _distance;
                if (CleanedWayPoints[FilteredIds[i]].Speed > 25 && CleanedWayPoints[FilteredIds[i]].Speed <= 45)
                    dis = dis/2;
                else if (CleanedWayPoints[FilteredIds[i]].Speed > 45 && CleanedWayPoints[FilteredIds[i]].Speed <= 55)
                    dis = dis / 3;
                else if (CleanedWayPoints[FilteredIds[i]].Speed>55)
                    dis = dis / 4;
                List<ElementDistance<RoadLeaf>> elementDistances =
                    _routing.GetKNearestRoads(CleanedWayPoints[FilteredIds[i]].Pos.X,
                        CleanedWayPoints[FilteredIds[i]].Pos.Y,
                        dis);
                if (elementDistances.Count == 0)
                {
                    while (elementDistances.Count == 0)
                    {
                        dis *= 2;
                        elementDistances = _routing.GetKNearestRoads(CleanedWayPoints[FilteredIds[i]].Pos.X, CleanedWayPoints[FilteredIds[i]].Pos.Y, dis);
                    }
                }
                //if (elementDistances.Count > MaxStateQuantity)
                //{
                //    elementDistances = elementDistances.GetRange(0, MaxStateQuantity);
                //}
                
                foreach (ElementDistance<RoadLeaf> elementDistance in elementDistances)
                {
                    NeighborRoad road = new NeighborRoad(elementDistance.Element.Arc1, CleanedWayPoints[FilteredIds[i]].Pos)
                    {
                        Index = wayPointCounter++,
                        RefPointId = i,
                        IsOneway =true
                    };
                    RoadSegments[FilteredIds[i]].Add(road);
                    if (elementDistance.Element.IsOneWay) continue;
                    road.ReversedArc = elementDistance.Element.Arc2;
                    road.IsOneway = false;
                }
            }
            //calculate initial prob
            foreach (NeighborRoad neighborRoad in RoadSegments[FilteredIds[0]])
            {
                neighborRoad.MaxProbability = Emission(neighborRoad);
            }
            //calculate V for states of each waypoint.
            for (int i = 1; i < FilteredIds.Count; i++)
            {
                foreach (NeighborRoad thisRoad in RoadSegments[FilteredIds[i]])
                {
                    foreach (NeighborRoad prevRoad in RoadSegments[FilteredIds[i - 1]])
                    {
                        double arcDistance;
                        double routeDistance;
                        List<Node> route;
                        double probability = prevRoad.MaxProbability*
                                             Transition(prevRoad, thisRoad,
                                                 out arcDistance, out routeDistance, out route)*
                                             Emission(thisRoad);
                        if (probability < thisRoad.MaxProbability) continue;
                        thisRoad.MaxProbability = probability;
                        thisRoad.MostPossibleAncient = prevRoad;
                        thisRoad.ArcDistanceToAncient = arcDistance;
                        thisRoad.RouteDistanceToAncient = routeDistance;
                        thisRoad.RouteToAncient = route;
                    }
                }
            }
            //from highest V, look back to get route.
            string x = "";
            List<NeighborRoad> matchedRoute = new List<NeighborRoad>();
            NeighborRoad roadTraveler = RoadSegments[FilteredIds[FilteredIds.Count - 1]].Max();
            while (roadTraveler != null)
            {
                x += roadTraveler.Index + "->";
                matchedRoute.Add(roadTraveler);
                roadTraveler = roadTraveler.MostPossibleAncient;
            }
            matchedRoute.Reverse();
            _resultList = matchedRoute;
            _logger.WriteLog("Viterbi in " + ((DateTime.Now - startTime).TotalMilliseconds / 1000.0).ToString("0.000") +
                         "s");
            _logger.WriteStats("\t"+((DateTime.Now - startTime).TotalMilliseconds / 1000.0).ToString("0.000"));
            
            //_logger.WriteLog("Sigma:" + EstimateSigma() + ", Beta:" + EstimateBeta());
            _routing.CleanTempNodes();
        }

        private double Emission(NeighborRoad road)
        {
            double temp = road.DistanceToRefPoint / _sigma;
            return Math.Exp(-0.5 * temp * temp) / SigmaMultiSqrt2Pi;
        }

        private double Transition(NeighborRoad road1, NeighborRoad road2, out double arcDistance, out double routeDistance, out List<Node> route)
        {
            if (road1.ProjPoint == road2.ProjPoint)
            {
                routeDistance = 0;
                route = new List<Node>();
            }
            else
            {
                route = _routing.FindRoute(road1, road2, out routeDistance);
            }
            arcDistance = Geometry.Haversine(road1.RefPoint, road2.RefPoint);
            
            double dt = Math.Abs(arcDistance - routeDistance);
            return Math.Exp(-dt / _beta) / _beta;
        }

        private void GetRoute(bool isTest = false)
        {
            Calculate(isTest);
            _finalRoute = new List<Node>();
            if(_resultList[0].ProjPosition==NearestPostion.End)
                _finalRoute.Add(_resultList[0].RoadArc.EndNode);
            else if (_resultList[0].ProjPosition == NearestPostion.Start)
                _finalRoute.Add(_resultList[0].RoadArc.StartNode);
            else
                _finalRoute.Add(new Node(0, _resultList[0].ProjPoint));
            _fixedWayPoints = new List<WayPoint>();
            _invalidSegments = "[";
            double diffMeterMax = double.MinValue;
            double percentAtMaxMeter = 0;
            double diffPercentMax = double.MinValue;
            double meterAtMaxPercent = 0;
            for (int i = 1; i < _resultList.Count; i++)
            {
                double diffPercent = _resultList[i].RouteDistanceToAncient / _resultList[i].ArcDistanceToAncient;
                //_logger.WriteLog("Diff percent:" + diffPercent);
                if (diffPercent < _percent)
                {
                    foreach (Node node in _resultList[i].RouteToAncient)
                    {
                        if (node != _finalRoute[_finalRoute.Count - 1])
                            _finalRoute.Add(node);
                    }
                    if (i == _resultList.Count - 1 && _resultList[i].ProjPosition==NearestPostion.Middle)
                    {
                        _finalRoute.Add(new Node(0, _resultList[i].ProjPoint));
                    }
                }
                else
                {
                    if (_resultList[i].ProjNode != _finalRoute[_finalRoute.Count - 1])
                    {
                        _finalRoute.Add(new Node(0,_resultList[i].ProjPoint));
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
                if (_finalRoute[i - 1].Id == 0) newSegment.IsValid = false;
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
            NearestPostion projPosition;
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
            _fixedWayPoints = new List<WayPoint>();
            //build temp search queue.
            LineSegment firstSegment = BuildRoadQueue();

            //NearestPostion ProjPosition;
            WayPoint startPoint = CleanedWayPoints[0];
            LineSegment startSegment = firstSegment;
            LineSegment currentSegment = firstSegment;
            //_fixedWayPoints.Add(RawWayPoints[0]);

            for (int i = 0; i < CleanedWayPoints.Count; i++)
            {
                double distance;
                Point2D projPoint;
                GetNearestWay(CleanedWayPoints[i].Pos, ref currentSegment, out projPoint, out distance);
                LineSegment road = currentSegment;
                bool isFirstPoint = i == 0;
                if (distance < ValidPointDistance)
                {
                    startPoint = new WayPoint(CleanedWayPoints[i].Time, projPoint, CleanedWayPoints[i].Speed,
                        CleanedWayPoints[i].Altitude);
                    _fixedWayPoints.Add(startPoint);
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
                        _fixedWayPoints.Add(CleanedWayPoints[0]);
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
                        //Length
                        double totalLength;
                        if (isFirstPoint)
                        {
                            totalLength = startSegment.Length;
                        }
                        else if (startSegment == road)
                        {
                            totalLength = Point2D.DistanceBetween(startPoint.Pos, projPoint);
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
                            totalLength = Point2D.DistanceBetween(startPoint.Pos, startSegment.P2);
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
                        Point2D tempPos = startPoint.Pos;
                        for (int j = 0; j < farPoints.Count; j++)
                        {
                            double length = totalLength*speeds[j]/totalSpeed;
                            Point2D p = GetProjectPoint(tempPos, length, ref startSegment);
                            tempPos = p;
                            WayPoint tempWayPoint = new WayPoint(farPoints[j].Time, p, farPoints[j].Speed,
                                farPoints[j].Altitude);
                            _fixedWayPoints.Add(tempWayPoint);
                        }
                    }
                    else
                    {
                        for (int j = 0; j < farPoints.Count; j++)
                        {
                            WayPoint tempWayPoint = new WayPoint(farPoints[j].Time, farPoints[j].Pos, farPoints[j].Speed,
                                farPoints[j].Altitude);
                            _fixedWayPoints.Add(tempWayPoint);
                        }
                    }
                    startPoint = new WayPoint(CleanedWayPoints[i].Time, projPoint, CleanedWayPoints[i].Speed,
                        CleanedWayPoints[i].Altitude);
                    _fixedWayPoints.Add(startPoint);
                    startSegment = road;
                }
            }
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
            return seg.GetPointByLength(tempP,lengthLeft);
        }

        public Bitmap GetRouteImage(bool isTest = false)
        {
            GetFixedWayPoint(isTest);
            double up = -90, down = 90, left = 180, right = -180;
            for (int i = 0; i < _fixedWayPoints.Count; i++)
            {
                double minLat = Math.Min(CleanedWayPoints[i].Pos.X, _fixedWayPoints[i].Pos.X);
                double maxLat = Math.Max(CleanedWayPoints[i].Pos.X, _fixedWayPoints[i].Pos.X);
                double minLon = Math.Min(CleanedWayPoints[i].Pos.Y, _fixedWayPoints[i].Pos.Y);
                double maxLon = Math.Max(CleanedWayPoints[i].Pos.Y, _fixedWayPoints[i].Pos.Y);
                if (minLon < left) left = minLon;
                if (maxLon > right) right = maxLon;
                if (maxLat > up) up = maxLat;
                if (minLat < down) down = minLat;
            }
            Transform trans = new Transform(left, right, up, down,1920,1080);
            Bitmap myImg = new Bitmap(trans.Width, trans.Height);
            
            Graphics objGraphics = Graphics.FromImage(myImg);
            objGraphics.Clear(Color.White);
            for (int i = 1; i < _fixedWayPoints.Count; i++)
            {

                //connection
                Point start = trans.ToGraph(CleanedWayPoints[i].Pos.X, CleanedWayPoints[i].Pos.Y);
                Point end = trans.ToGraph(_fixedWayPoints[i].Pos.X, _fixedWayPoints[i].Pos.Y);
                objGraphics.DrawLine(new Pen(Color.Blue, 1), start, end);
                //fixed line
                start = trans.ToGraph(_fixedWayPoints[i-1].Pos.X, _fixedWayPoints[i-1].Pos.Y);
                end = trans.ToGraph(_fixedWayPoints[i].Pos.X, _fixedWayPoints[i].Pos.Y);
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
            for (int i = 1; i < _fixedWayPoints.Count; i++)
            {
                route += "{seg:";
                route += "[[" + _fixedWayPoints[i - 1].Pos.X + "," + _fixedWayPoints[i - 1].Pos.Y +
                         "],[" + _fixedWayPoints[i].Pos.X + "," + _fixedWayPoints[i].Pos.Y + "]],";
                route += "speed:" + (_fixedWayPoints[i - 1].Speed + _fixedWayPoints[i].Speed)/2;
                route += "},";
                conn += "[[" + CleanedWayPoints[i].Pos.X + "," + CleanedWayPoints[i].Pos.Y + "],[" +
                        _fixedWayPoints[i].Pos.X + "," + _fixedWayPoints[i].Pos.Y + "]],";
            }
            route = route.TrimEnd(',') + "]";
            conn = conn.TrimEnd(',') + "]";
            return "({route:" + route + ",conn:" + conn + ",invalid:" + _invalidSegments + "})";
        }

        public string GetDebugJson()
        {
            Calculate();
            string route = "[";
            string conn = "[[[" + _resultList[0].ProjPoint.X + "," + _resultList[0].ProjPoint.Y + "],[" + _resultList[0].RefPoint.X + "," + _resultList[0].RefPoint.Y + "]]";
            route += "[" + _resultList[0].ProjPoint.X + "," + _resultList[0].ProjPoint.Y + "]";
            for (int i = 1; i < _resultList.Count; i++)
            {
                route += ",[" + _resultList[i].ProjPoint.X + "," + _resultList[i].ProjPoint.Y + "]";
                conn += ",[[" + _resultList[i].ProjPoint.X + "," + _resultList[i].ProjPoint.Y + "],[" + _resultList[i].RefPoint.X + "," + _resultList[i].RefPoint.Y + "]]";
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
            sigma *= 1.4826;    //radian to degree
            return sigma;
        }

        private double EstimateBeta()
        {
            List<double> distances = new List<double>();
            for (int i=1;i< _resultList.Count;i++)
            {
                distances.Add(Math.Abs(_resultList[i].ArcDistanceToAncient - _resultList[i].RouteDistanceToAncient));
            }
            double beta = Mathmatic.MedianDeviationOfTheMedian(distances);
            return beta/Math.Log(2);
        }

        private void CleanWayPoints(bool isTest)
        {
            //remove initial and ending points with same lat/lon.
            List<WayPoint> firstPass = new List<WayPoint>();
            _firstValidId = 0;
            for (int i = 1; i < RawWayPoints.Count; i++)
            {
                if (!RawWayPoints[0].Pos.Equals(RawWayPoints[i].Pos))
                    break;
                _firstValidId = i;
            }
            _lastValidId = 0;
            for (int i = RawWayPoints.Count - 1; i > 0; i--)
            {
                if (!RawWayPoints[RawWayPoints.Count - 1].Pos.Equals(RawWayPoints[i].Pos))
                    break;
                _lastValidId = i;
            }

            //combine other points with speed<=3 . (red light/stop sign)
            for (int i = _firstValidId; i <= _lastValidId; i++)
            {
                if (RawWayPoints[i].Speed <= LowSpeedThreshold)
                {
                    double avgLat = 0, avgLon = 0;
                    float avgSpeed = 0;
                    int zeroCount = 0;
                    while (i <= _lastValidId && RawWayPoints[i].Speed <= LowSpeedThreshold)
                    {
                        zeroCount++;
                        avgSpeed += RawWayPoints[i].Speed;
                        i++;
                    }
                    if (i == _lastValidId)
                        firstPass.Add(new WayPoint(RawWayPoints[i].Time, RawWayPoints[i].Pos,
                            avgSpeed/zeroCount, RawWayPoints[i].Altitude));
                    else
                        firstPass.Add(new WayPoint(RawWayPoints[i - 1].Time, RawWayPoints[i - 1].Pos,
                            avgSpeed/zeroCount, RawWayPoints[i - 1].Altitude));
                }
                if (i < _lastValidId)
                    firstPass.Add(new WayPoint(RawWayPoints[i].Time, RawWayPoints[i].Pos, RawWayPoints[i].Speed,
                        RawWayPoints[i].Altitude));
            }
            List<WayPoint> secondPass = firstPass;
            CleanedWayPoints = secondPass;
            // Filter points by distance gap, and keep turning points
            int currentPoint = 0;
            int nextPoint = 1;
            FilteredIds.Add(currentPoint);
            double bearMax = double.MinValue;
            double bearMin = double.MaxValue;
            if (!isTest)
            {
                for (int i = 1; i < secondPass.Count; i++)
                {
                    double bearing = Geometry.Bearing(secondPass[i - 1].Pos, secondPass[i].Pos);
                    if (bearing > bearMax) bearMax = bearing;
                    if (bearing < bearMin) bearMin = bearing;
                    double dis;
                    if (((dis = Geometry.Haversine(secondPass[i].Pos, secondPass[currentPoint].Pos)) > _interval ||
                         (Geometry.BearingDiff(secondPass[currentPoint].Pos, secondPass[nextPoint].Pos,
                             secondPass[i - 1].Pos, secondPass[i].Pos) > TurnAngleThreshold &&
                          dis > MinDisForAngleFilter)) && //remove points with turn angle > 30 degree suddenly. 
                        MinDistToArc(secondPass[i].Pos) < FilterSkipDistance)
                    {
                        _avgSpeed += secondPass[currentPoint].Speed;
                        currentPoint = i;
                        if (i + 1 < secondPass.Count)
                            nextPoint = i + 1;
                        if (!FilteredIds.Contains(currentPoint))
                            FilteredIds.Add(currentPoint);
                    }
                }
            }
            else
            {
                for (int i = 1; i < secondPass.Count; i++)
                {
                    double dis;
                    if ( Geometry.Haversine(secondPass[i].Pos, secondPass[currentPoint].Pos) > 20 )
                    {
                        _avgSpeed += secondPass[currentPoint].Speed;
                        currentPoint = i;
                        if (!FilteredIds.Contains(currentPoint))
                            FilteredIds.Add(currentPoint);
                    }
                }
            }
            _avgSpeed /= FilteredIds.Count;
            _logger.WriteStats("\t" + RawWayPoints.Count);
            _logger.WriteStats("\t" + FilteredIds.Count);
            _logger.WriteLog("Average speed:" + _avgSpeed.ToString("0.0"));
            _logger.WriteStats("\t" + _avgSpeed.ToString("0.0"));
            _logger.WriteLog("Filtered points:" + ((RawWayPoints.Count - FilteredIds.Count)*100.0 / RawWayPoints.Count).ToString("0.0")+"%");
            _logger.WriteStats("\t" + ((RawWayPoints.Count - FilteredIds.Count) * 100.0 / RawWayPoints.Count).ToString("0.0") + "%");
            if (!FilteredIds.Contains(secondPass.Count - 1))
                FilteredIds.Add(secondPass.Count - 1);
        }

        private double MinDistToArc(Point2D point)
        {
            List<ElementDistance<RoadLeaf>> x = _routing.GetKNearestRoads(point.X,point.Y,0.0001);
            if (x.Count == 0) return double.MaxValue;
            NearestPostion position;
            Point2D projPoint = Point2D.ProjectOnLine(point, x[0].Element.Arc1, out position);
            return Geometry.Haversine(point, projPoint);
        }
    }
}