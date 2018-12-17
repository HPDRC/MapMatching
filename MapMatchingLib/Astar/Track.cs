using System;

namespace MapMatchingLib.Astar
{
    /// <summary>
    /// A track is a succession of nodes which have been visited.
    /// Thus when it leads to the target node, it is easy to return the result path.
    /// These objects are contained in Open and Closed lists.
    /// </summary>
    public class Track : IComparable<Track>
    {
        private static double _coeff = 0.5;
        private static Heuristic _choosenHeuristic = AStar.EuclidianHeuristic;

        public static Node Target { set; get; }

        public Node EndNode;
        public Track Queue;

        public static double DijkstraHeuristicBalance
        {
            get { return _coeff; }
            set
            {
                if (value < 0 || value > 1)
                    throw new ArgumentException(
                        @"The coefficient which balances the respective influences of Dijkstra and the Heuristic must belong to [0; 1].
-> 0 will minimize the number of nodes explored but will not take the real cost into account.
-> 0.5 will minimize the cost without developing more nodes than necessary.
-> 1 will only consider the real cost without estimating the remaining cost.");
                _coeff = value;
            }
        }

        public static Heuristic ChoosenHeuristic
        {
            set { _choosenHeuristic = value; }
            get { return _choosenHeuristic; }
        }

        private readonly int _nbArcsVisited;

        public int NbArcsVisited
        {
            get { return _nbArcsVisited; }
        }

        private readonly double _cost;

        public double Cost
        {
            get { return _cost; }
        }

        private readonly double _evaluation;
        public virtual double Evaluation
        {
            get
            {
                return _evaluation;
                //return _coeff*_cost + (1 - _coeff)*_choosenHeuristic(EndNode, Target);
            }
        }

        public bool Succeed
        {
            get { return Equals(EndNode, Target); }
        }

        public Track(Node graphNode)
        {
            _cost = 0;
            _nbArcsVisited = 0;
            Queue = null;
            EndNode = graphNode;
            double dx = EndNode.Position.X - Target.Position.X;
            double dy = EndNode.Position.Y - Target.Position.Y;
            _evaluation = 0.5*(dx*dx + dy*dy);
        }

        public Track(Track previousTrack, Arc transition)
        {
            Queue = previousTrack;
            _cost = Queue.Cost + transition.Cost;
            _nbArcsVisited = Queue._nbArcsVisited + 1;
            EndNode = transition.EndNode;
            double dx = EndNode.Position.X - Target.Position.X;
            double dy = EndNode.Position.Y - Target.Position.Y;
            _evaluation = 0.5 * _cost + 0.5 * (dx * dx + dy * dy);
        }

        public int CompareTo(Track trackObjet)
        {
            return Evaluation.CompareTo(trackObjet.Evaluation);
        }
    }
}