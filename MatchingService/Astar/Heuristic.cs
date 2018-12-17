namespace MatchingService.Astar
{
    /// <summary>
    /// A heuristic is a function that associates a value with a node to gauge it considering the node to reach.
    /// </summary>
    public delegate double Heuristic(Node nodeToEvaluate, Node targetNode);
}