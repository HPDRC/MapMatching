using System;
using System.Collections.Generic;

namespace MatchingService.Astar
{
    /// <summary>
    /// Basic geometry class : easy to replace
    /// Written so as to be generalized
    /// </summary>
    public class Vector2D
    {
        private readonly double[] _coordinates = new double[2];

        /// <summary>
        /// Vector2D constructor.
        /// </summary>
        /// <exception cref="ArgumentNullException">Argument array must not be null.</exception>
        /// <exception cref="ArgumentException">The Coordinates' array must contain exactly 2 elements.</exception>
        /// <param name="coordinates">An array containing the three coordinates' values.</param>
        public Vector2D(IList<double> coordinates)
        {
            if (coordinates == null) throw new ArgumentNullException();
            if (coordinates.Count != 2)
                throw new ArgumentException("The Coordinates' array must contain exactly 2 elements.");
            Dx = coordinates[0];
            Dy = coordinates[1];
        }

        /// <summary>
        /// Vector2D constructor.
        /// </summary>
        /// <param name="deltaX">DX coordinate.</param>
        /// <param name="deltaY">DY coordinate.</param>
        public Vector2D(double deltaX, double deltaY)
        {
            Dx = deltaX;
            Dy = deltaY;
        }

        /// <summary>
        /// Constructs a Vector2D with two points.
        /// </summary>
        /// <param name="p1">First point of the vector.</param>
        /// <param name="p2">Second point of the vector.</param>
        public Vector2D(Point2D p1, Point2D p2)
        {
            Dx = p2.X - p1.X;
            Dy = p2.Y - p1.Y;
        }

        /// <summary>
        /// Accede to coordinates by indexes.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">Illegal value for CoordinateIndex.</exception>
        public double this[int coordinateIndex]
        {
            get { return _coordinates[coordinateIndex]; }
            set { _coordinates[coordinateIndex] = value; }
        }

        /// <summary>
        /// Gets/Sets delta X value.
        /// </summary>
        public double Dx
        {
            set { _coordinates[0] = value; }
            get { return _coordinates[0]; }
        }

        /// <summary>
        /// Gets/Sets delta Y value.
        /// </summary>
        public double Dy
        {
            set { _coordinates[1] = value; }
            get { return _coordinates[1]; }
        }


        /// <summary>
        /// Multiplication of a vector by a scalar value.
        /// </summary>
        /// <param name="v">Vector to operate.</param>
        /// <param name="factor">Factor value.</param>
        /// <returns>New vector resulting from the multiplication.</returns>
        public static Vector2D operator *(Vector2D v, double factor)
        {
            return new Vector2D(new[] { v[0] * factor, v[1] * factor });
        }

        /// <summary>
        /// Division of a vector by a scalar value.
        /// </summary>
        /// <exception cref="ArgumentException">Divider cannot be 0.</exception>
        /// <param name="v">Vector to operate.</param>
        /// <param name="divider">Divider value.</param>
        /// <returns>New vector resulting from the division.</returns>
        public static Vector2D operator /(Vector2D v, double divider)
        {
            return new Vector2D(new[] { v[0] / divider, v[1] / divider });
        }

        /// <summary>
        /// Gets the square norm of the vector.
        /// </summary>
        public double SquareNorm
        {
            get
            {
                return _coordinates[0] * _coordinates[0] + _coordinates[1] * _coordinates[1];
            }
        }

        /// <summary>
        /// Scalar product between two vectors.
        /// </summary>
        /// <param name="v1">First vector.</param>
        /// <param name="v2">Second vector.</param>
        /// <returns>Value resulting from the scalar product.</returns>
        public static double operator |(Vector2D v1, Vector2D v2)
        {
            return v1[0] * v2[0] + v1[1] * v2[1];
        }

        /// <summary>
        /// Returns a point resulting from the translation of a specified point.
        /// </summary>
        /// <param name="p">Point to translate.</param>
        /// <param name="v">Vector to apply for the translation.</param>
        /// <returns>Point resulting from the translation.</returns>
        public static Point2D operator +(Point2D p, Vector2D v)
        {
            return new Point2D(new[] { p[0] + v[0], p[1] + v[1] });
        }
    }
}