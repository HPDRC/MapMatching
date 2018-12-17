using System;
using System.Collections.Generic;

namespace MatchingService.Astar
{
    [Serializable]
    public class Graph
    {
        private readonly Dictionary<long, Node> _nodes;
        private readonly Dictionary<long[], Arc> _arcs;

        public int NodeCount
        {
            get { return _nodes.Count; }
        }
        public int ArcCount
        {
            get { return _arcs.Count; }
        }

        public Graph()
        {
            //_nodes = new ArrayList();
            //_arcs = new ArrayList();
            _nodes = new Dictionary<long, Node>();
            _arcs = new Dictionary<long[], Arc>();
        }
        
        public Node AddNode(Node newNode)
        {
            if (_nodes.ContainsKey(newNode.Id)) return _nodes[newNode.Id];
            _nodes[newNode.Id] = newNode;
            return newNode;
        }

        public Node AddNode(long id, double x, double y)
        {
            Node newNode = new Node(id, x, y);
            return AddNode(newNode);
        }

        public bool AddArc(Arc newArc)
        {
            if (newArc == null || _arcs.ContainsKey(newArc.Key)) return false;
            if (!_nodes.ContainsKey(newArc.StartNode.Id) || !_nodes.ContainsKey(newArc.EndNode.Id))
                throw new ArgumentException(
                    "Cannot add an arc if one of its extremity nodes does not belong to the graph.");
            _arcs[newArc.Key] = newArc;
            return true;
        }

        public Arc AddArc(Node startNode, Node endNode, double weight)
        {
            Arc newArc = new Arc(startNode, endNode, weight);
            return AddArc(newArc) ? newArc : null;
        }

        public bool RemoveNode(Node nodeToRemove)
        {
            if (nodeToRemove == null) return false;
            try
            {
                foreach (Arc a in nodeToRemove.IncomingArcs)
                {
                    a.StartNode.OutgoingArcs.Remove(a);
                    _arcs.Remove(a.Key);
                }
                foreach (Arc a in nodeToRemove.OutgoingArcs)
                {
                    a.EndNode.IncomingArcs.Remove(a);
                    _arcs.Remove(a.Key);
                }
                _nodes.Remove(nodeToRemove.Id);
            }
            catch
            {
                return false;
            }
            return true;
        }

        public bool RemoveArc(Arc arcToRemove)
        {
            if (arcToRemove == null) return false;
            try
            {
                arcToRemove.StartNode.OutgoingArcs.Remove(arcToRemove);
                arcToRemove.EndNode.IncomingArcs.Remove(arcToRemove);
                _arcs.Remove(arcToRemove.Key);
            }
            catch
            {
                return false;
            }
            return true;
        }

        public Node GetNode(long id)
        {
            return _nodes[id];
        }

        public Arc GetArc(long[] id)
        {
            return _arcs[id];
        }
    }
}