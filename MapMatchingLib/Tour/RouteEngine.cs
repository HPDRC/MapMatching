using System.Collections.Generic;
using System.Linq;

namespace MapMatchingLib.Tour
{
    public class RouteEngine
    {
        public RouteEngine(List<GraphNode> locations)
        {
            Locations = locations;
        }

        public List<GraphNode> Locations { get; set; }
        public RouteEngine()
        {
            Locations = new List<GraphNode>();
        }

        /// <summary>
        /// Calculates the shortest route to all the other locations
        /// </summary>
        /// <param name="startNode"></param>
        /// <param name="endNode"></param>
        /// <returns>List of all locations and their shortest route</returns>
        public List<GraphNode> CalculateMinCost(GraphNode startNode, GraphNode endNode)
        {
            //Initialise a new empty route list
            //Dictionary<GraphNode, GraphRoute> shortestPaths = new Dictionary<GraphNode, GraphRoute>();
            //Initialise a new empty handled locations list
            List<GraphNode> handledLocations = new List<GraphNode>();

            //The startPosition has a weight 0. 
            //shortestPaths[startNode].Cost = 0;
            startNode.TotalCost = 0;
            //If all locations are handled, stop the engine and return the result
            while (handledLocations.Count != Locations.Count)
            {
                //Order the locations
                List<GraphNode> shortestLocations = (Locations.OrderBy(s => s.TotalCost)).ToList();

                GraphNode graphNodeToProcess = null;
                //Search for the nearest location that isn't handled
                foreach (GraphNode location in shortestLocations)
                {
                    if (handledLocations.Contains(location)) continue;
                    //If the cost equals int.max, there are no more possible connections to the remaining locations
                    if (location.TotalCost == int.MaxValue)
                        return endNode.Connections;
                    graphNodeToProcess = location;
                    break;
                }

                //Iterate through all connections and search for a connection which is shorter
                if (graphNodeToProcess != null)
                {
                    foreach (GraphNode nodeNext in graphNodeToProcess.Next)
                    {
                        if (nodeNext.TotalCost > nodeNext.Weight + graphNodeToProcess.TotalCost)
                        {
                            nodeNext.Connections = graphNodeToProcess.Connections.ToList();
                            nodeNext.Connections.Add(graphNodeToProcess);
                            nodeNext.TotalCost = nodeNext.Weight + graphNodeToProcess.TotalCost;
                        }
                    }
                    //Add the location to the list of processed locations
                    handledLocations.Add(graphNodeToProcess);
                }
            }
            return endNode.Connections;
        }
    }
}