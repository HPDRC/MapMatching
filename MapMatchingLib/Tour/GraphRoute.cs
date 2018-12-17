using System.Collections.Generic;

namespace MapMatchingLib.Tour
{
    public class GraphRoute
    {
        private readonly string _identifier;
        public List<GraphNode> Connections { get; set; }
        public int Cost { get; set; }

        public GraphRoute(string identifier)
        {
            Cost = int.MaxValue;
            Connections = new List<GraphNode>();
            _identifier = identifier;
        }

        public override string ToString()
        {
            string routeStr = "";
            foreach (GraphNode node in Connections)
            {
                routeStr += node + " -> ";
            }
            return "Id:" + _identifier + " Cost:" + Cost + "\r\n" + routeStr + _identifier + "\r\n";
        }
    }
}