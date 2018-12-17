using System;

namespace MapMatchingLib.Astar
{
    /// <summary>
    /// Basic geometry class : easy to replace
    /// Written so as to be generalized
    /// </summary>
    [Serializable]
    public class Point2D
    {
        private readonly double[] _coordinates = new double[2];

        /// <summary>
        /// Point2D constructor.
        /// </summary>
        /// <exception cref="ArgumentNullException">Argument array must not be null.</exception>
        /// <exception cref="ArgumentException">The Coordinates' array must contain exactly 2 elements.</exception>
        /// <param name="coordinates">An array containing the three coordinates' values.</param>
        public Point2D(double[] coordinates)
        {
            if (coordinates == null) throw new ArgumentNullException();
            if (coordinates.Length != 2)
                throw new ArgumentException("The Coordinates' array must contain exactly 2 elements.");
            X = coordinates[0];
            Y = coordinates[1];
        }

        /// <summary>
        /// Point2D constructor.
        /// </summary>
        /// <param name="coordinateX">X coordinate.</param>
        /// <param name="coordinateY">Y coordinate.</param>
        public Point2D(double coordinateX, double coordinateY)
        {
            X = coordinateX;
            Y = coordinateY;
        }

        /// <summary>
        /// Accede to coordinates by indexes.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">Index must belong to [0;2].</exception>
        public double this[int coordinateIndex]
        {
            get { return _coordinates[coordinateIndex]; }
            set { _coordinates[coordinateIndex] = value; }
        }

        /// <summary>
        /// Gets/Set X coordinate.
        /// </summary>
        public double X
        {
            set { _coordinates[0] = value; }
            get { return _coordinates[0]; }
        }

        /// <summary>
        /// Gets/Set Y coordinate.
        /// </summary>
        public double Y
        {
            set { _coordinates[1] = value; }
            get { return _coordinates[1]; }
        }

        /// <summary>
        /// Returns the distance between two points.
        /// </summary>
        /// <param name="p1">First point.</param>
        /// <param name="p2">Second point.</param>
        /// <returns>Distance value.</returns>
        public static double DistanceBetween(Point2D p1, Point2D p2)
        {
            return Math.Sqrt((p1.X - p2.X)*(p1.X - p2.X) + (p1.Y - p2.Y)*(p1.Y - p2.Y));
        }

        /// <summary>
        /// Returns the projection of a point on the line defined with two other points.
        /// When the projection is out of the segment, then the closest extremity is returned.
        /// </summary>
        /// <exception cref="ArgumentNullException">None of the arguments can be null.</exception>
        /// <exception cref="ArgumentException">P1 and P2 must be different.</exception>
        /// <param name="pt">Point to project.</param>
        /// <param name="p1">First point of the line.</param>
        /// <param name="p2">Second point of the line.</param>
        /// <returns>The projected point if it is on the segment / The closest extremity otherwise.</returns>
        public static Point2D ProjectOnLine(Point2D pt, Point2D p1, Point2D p2, out SegmentPart position)
        {
            Vector2D vLine = new Vector2D(p1, p2);
            Vector2D v1Pt = new Vector2D(p1, pt);
            double distance = (vLine | v1Pt) / vLine.SquareNorm;
            if (distance < 0)
            {
                position = SegmentPart.Start;
                return p1;
            }
            if (distance > 1)
            {
                position = SegmentPart.End;
                return p2;
            }
            position = SegmentPart.Middle;
            return p1 + vLine * distance;
        }

        public static Point2D ProjectOnLine(Point2D pt, Arc arc, out SegmentPart position)
        {
            Vector2D vLine = new Vector2D(arc.StartNode.Position, arc.EndNode.Position);
            Vector2D v1Pt = new Vector2D(arc.StartNode.Position, pt);
            double distance = (vLine | v1Pt) / vLine.SquareNorm;
            if (distance < 0)
            {
                position = SegmentPart.Start;
                return arc.StartNode.Position;
            }
            if (distance > 1)
            {
                position = SegmentPart.End;
                return arc.EndNode.Position;
            }
            position = SegmentPart.Middle;
            return arc.StartNode.Position + vLine * distance;
        }

        /// <summary>
        /// Object.Equals override.
        /// Tells if two points are equal by comparing coordinates.
        /// </summary>
        /// <exception cref="ArgumentException">Cannot compare Point2D with another type.</exception>
        /// <param name="point">The other 2DPoint to compare with.</param>
        /// <returns>'true' if points are equal.</returns>
        public override bool Equals(object point)
        {
            Point2D p = (Point2D) point;
            if (p == null) throw new Exception("Object must be of type " + GetType());
            bool resultat = true;
            resultat &= p[0].Equals(this[0]);
            resultat &= p[1].Equals(this[1]);
            return resultat;
        }

        /// <summary>
        /// Object.GetHashCode override.
        /// </summary>
        /// <returns>HashCode value.</returns>
        public override int GetHashCode()
        {
            double hashCode = 0;
            hashCode += this[0];
            hashCode += this[1];
            return (int) hashCode;
        }

        /// <summary>
        /// Object.GetHashCode override.
        /// Returns a textual description of the point.
        /// </summary>
        /// <returns>String describing this point.</returns>
        public override string ToString()
        {
            return "[" + _coordinates[0] + "," + _coordinates[1] + "]";
        }
    }
}