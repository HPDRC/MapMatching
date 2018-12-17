using MapMatchingLib.Astar;

namespace MapMatchingLib.Tour
{
    public class HeadNode : TrackNode
    {
        public HeadNode(Point2D startPoint) 
        {
            EndEvent = new TrackEvent(0, startPoint, false, "", 0,0);
        }
    }
}