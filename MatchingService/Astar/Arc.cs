using System;
using MatchingService.SysTools;

namespace MatchingService.Astar
{
    /// <summary>
    /// An arc is defined with its two extremity nodes StartNode and EndNode therefore it is oriented.
    /// It is also characterized by a crossing factor named 'Weight'.
    /// This value represents the difficulty to reach the ending node from the starting one.
    /// </summary>
    [Serializable]
    public class Arc
    {
        private Node _startNode;
        private Node _endNode;
        private double _weight;
        private bool _passable;
        private readonly double _length;
        private bool _lengthUpdated;
        public long[] Key;
        public Arc(Node start, Node end, double weight)
        {
            Key = new[] {start.Id, end.Id};
            StartNode = start;
            EndNode = end;
            Weight = weight;
            //LengthUpdated = false;
            Passable = true;
            //_length = Point2D.DistanceBetween(_startNode.Position, _endNode.Position);
            _length = Geometry.Haversine(_startNode.Position, _endNode.Position);//Point2D.DistanceBetween(_startNode.Position, _endNode.Position);
            _cost = Weight * Length;
        }

        public Node StartNode
        {
            set
            {
                if (value == null) throw new Exception("StartNode is null");
                if (EndNode != null && value.Equals(EndNode))
                    throw new Exception("StartNode and EndNode must be different");
                if (_startNode != null) _startNode.OutgoingArcs.Remove(this);
                _startNode = value;
                _startNode.OutgoingArcs.Add(this);
            }
            get { return _startNode; }
        }

        public Node EndNode
        {
            set
            {
                if (value == null) throw new Exception("EndNode is null");
                if (StartNode != null && value.Equals(StartNode))
                    throw new Exception("StartNode and EndNode must be different");
                if (_endNode != null) _endNode.IncomingArcs.Remove(this);
                _endNode = value;
                _endNode.IncomingArcs.Add(this);
            }
            get { return _endNode; }
        }

        /// <summary>
        /// Sets/Gets the weight of the arc.
        /// This value is used to determine the cost of moving through the arc.
        /// </summary>
        public double Weight
        {
            set { _weight = value; }
            get { return _weight; }
        }

        /// <summary>
        /// Gets/Sets the functional state of the arc.
        /// 'true' means that the arc is in its normal state.
        /// 'false' means that the arc will not be taken into account (as if it did not exist or if its cost were infinite).
        /// </summary>
        public bool Passable
        {
            set { _passable = value; }
            get { return _passable; }
        }

        //internal bool LengthUpdated
        //{
        //    set { _lengthUpdated = value; }
        //    get { return _lengthUpdated; }
        //}

        /// <summary>
        /// Gets arc's length.
        /// </summary>
        public double Length
        {
            get
            {
                //if (LengthUpdated == false)
                //{
                //    _length = CalculateLength();
                //    LengthUpdated = true;
                //}
                return _length;
            }
        }

        /// <summary>
        /// Performs the calculous that returns the arc's length
        /// Can be overriden for derived types of arcs that are not linear.
        /// </summary>
        /// <returns></returns>
        //protected virtual double CalculateLength()
        //{
        //    return Point2D.DistanceBetween(_startNode.Position, _endNode.Position);
        //}

        private double _cost;
        /// <summary>
        /// Gets the cost of moving through the arc.
        /// Can be overriden when not simply equals to Weight*Length.
        /// </summary>
        public virtual double Cost
        {
            get { return _cost; }
        }

        /// <summary>
        /// Returns the textual description of the arc.
        /// object.ToString() override.
        /// </summary>
        /// <returns>String describing this arc.</returns>
        public override string ToString()
        {
            return "[" + _startNode + "," + _endNode + "]";
        }

        /// <summary>
        /// Object.Equals override.
        /// Tells if two arcs are equal by comparing StartNode and EndNode.
        /// </summary>
        /// <exception cref="ArgumentException">Cannot compare an arc with another type.</exception>
        /// <param name="o">The arc to compare with.</param>
        /// <returns>'true' if both arcs are equal.</returns>
        public bool Equals(Arc o)
        {
            //Arc a = (Arc)o;
            //if (a == null)
            //    throw new ArgumentException("Cannot compare type " + GetType() + " with type " + o.GetType() + " !");
            //return _startNode.Equals(a._startNode) && _endNode.Equals(a._endNode);
            return _startNode.Id == o._startNode.Id && _endNode.Id == o._endNode.Id;
        }

        /// <summary>
        /// Object.GetHashCode override.
        /// </summary>
        /// <returns>HashCode value.</returns>
        public override int GetHashCode()
        {
            return (int) Length;
        }

        public Point2D GetPointByLength(Point2D start, double length)
        {
            double scale = length / Length;
            return new Point2D(start.X + (EndNode.X - StartNode.X) * scale, start.Y + (EndNode.Y - StartNode.Y) * scale);
        }
    }
}