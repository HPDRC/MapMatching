using System;
using System.Drawing;
namespace MapMatchingLib.SysTools
{
    internal class Transform
    {
        public int Width, Height;
        private readonly double _left;
        private readonly double _up;
        public double Scale;
        private int _boarder;

        public Transform( double left, double right,double up, double down, int maxWidth, int maxHeight, int boarder=50)
        {
            _boarder = boarder;
            _left = left;
            _up =  up;
            double width = right - left;
            double height = up - down;
            double initRatio = width/height;
            double destRatio = (double) (maxWidth - 2*_boarder)/(maxHeight - 2*_boarder);

            if (initRatio > destRatio)
            {
                Scale = (maxWidth - 2 * _boarder) / width;
                Width = maxWidth;
                Height = (int)Math.Ceiling(height * Scale) + 2 * _boarder;
            }
            else
            {
                Scale = (maxHeight - 2 * _boarder) / height;
                Height = maxHeight;
                Width = (int)Math.Ceiling(width * Scale) + 2 * _boarder;
            }
        }

        public Point ToGraph(double lat, double lon)
        {
            return new Point((int)(Scale * (lon - _left)) + _boarder, (int)(Scale * (_up - lat)) + _boarder);
        }
    }
}
