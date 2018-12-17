using MapMatchingLib.Astar;

namespace MapMatchingLib.Tour
{
    public class TailNode : TrackNode
    {
        public TailNode(double roadLength, Point2D endPoint,int lastIndex)
        {
            StartEvent = new TrackEvent(roadLength, endPoint, true, "", 0, lastIndex);
        }
    }
}