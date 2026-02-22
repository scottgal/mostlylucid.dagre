using System;
using System.Collections.Generic;
using System.Linq;


namespace Dagre
{
    public class DagreGraph
    {
        public DagreGraph(bool compound)
        {
            _isCompound = compound;
            if (compound)
            {
                _children.Add(GRAPH_NODE, new Dictionary<string, bool>(StringComparer.Ordinal));
            }
        }

        public bool _isDirected = true;
        public bool _isCompound = false;
        public bool _isMultigraph = false;

        public DagreGraph SetDefaultNodeLabel(Func<string, NodeLabel> p)
        {
            _defaultNodeLabelFn = p;
            return this;
        }

        public string[] Nodes()
        {
            return NodesRaw();
        }
        public string[] NodesRaw()
        {
            if (_nodesCache == null)
                _nodesCache = _nodesRaw.Keys.ToArray();
            return _nodesCache;
        }
        string[] _nodesCache;
        internal void InvalidateNodesCache() => _nodesCache = null;

        public Dictionary<string, NodeLabel> _nodesRaw = new Dictionary<string, NodeLabel>(StringComparer.Ordinal);

        GraphLabel _label = new GraphLabel();
        public GraphLabel Graph() { return _label; }


        public NodeLabel NodeRaw(string v)
        {
            return _nodesRaw.TryGetValue(v, out var value) ? value : null;
        }

        internal bool IsMultigraph()
        {
            return _isMultigraph;
        }

        public NodeLabel Node(string v)
        {
            return NodeRaw(v);
        }

        public List<DagreEdgeIndex> _edgesIndexes = new List<DagreEdgeIndex>();

        public const string EDGE_KEY_DELIM = "\x01";
        public const string DEFAULT_EDGE_NAME = "\x00";

        public static string EdgeArgsToId(bool isDirected, string v, string w, string name)
        {
            if (!isDirected && string.CompareOrdinal(v, w) > 0)
                (v, w) = (w, v);
            var n = name ?? DEFAULT_EDGE_NAME;
            return string.Concat(v, EDGE_KEY_DELIM, w, EDGE_KEY_DELIM, n);
        }

        /// <summary>
        /// SetEdge — most common 2-arg form.
        /// </summary>
        public DagreGraph SetEdge(string v, string w)
        {
            return SetEdgeInternal(v, w, null, null, false);
        }

        /// <summary>
        /// SetEdge with label.
        /// </summary>
        public DagreGraph SetEdge(string v, string w, EdgeLabel label)
        {
            return SetEdgeInternal(v, w, label, null, true);
        }

        /// <summary>
        /// SetEdge with label and name.
        /// </summary>
        public DagreGraph SetEdge(string v, string w, EdgeLabel label, string name)
        {
            return SetEdgeInternal(v, w, label, name, true);
        }

        private DagreGraph SetEdgeInternal(string v, string w, EdgeLabel value, string name, bool valueSpecified)
        {
            var e = EdgeArgsToId(_isDirected, v, w, name);
            if (_edgeLabels.ContainsKey(e))
            {
                if (valueSpecified)
                    _edgeLabels[e] = value;
                return this;
            }
            if (name != null && !_isMultigraph)
                throw new DagreException("Cannot set a named edge when isMultigraph = false");

            SetNode(v);
            SetNode(w);
            _edgeLabels[e] = valueSpecified ? value : _defaultEdgeLabelFn(v, w, name);

            if (!_isDirected && string.CompareOrdinal(v, w) > 0)
                (v, w) = (w, v);

            var edgeObj = new DagreEdgeIndex { v = v, w = w, name = name };
            _edgeObjs[e] = edgeObj;
            InvalidateEdgesCache();

            var predsW = _predecessors[w];
            if (predsW.TryGetValue(v, out var predsVal))
                predsW[v] = predsVal + 1;
            else
                predsW.Add(v, 1);

            var sucsV = _successors[v];
            if (sucsV.TryGetValue(w, out var sucsVal))
                sucsV[w] = sucsVal + 1;
            else
                sucsV.Add(w, 1);

            _in[w][e] = edgeObj;
            _out[v][e] = edgeObj;
            _edgeCount++;
            return this;
        }

        /// <summary>
        /// Get edge label by source, target, and optional name.
        /// </summary>
        internal EdgeLabel EdgeRaw(string v, string w, string name = null)
        {
            var key = EdgeArgsToId(_isDirected, v, w, name);
            return _edgeLabels.TryGetValue(key, out var value) ? value : null;
        }

        public EdgeLabel Edge(DagreEdgeIndex v)
        {
            return Edge(v.v, v.w, v.name);
        }
        public EdgeLabel Edge(string v, string w, string name = null)
        {
            var e = EdgeArgsToId(_isDirected, v, w, name);
            return _edgeLabels.TryGetValue(e, out var label) ? label : null;
        }

        internal string[] Neighbors(string v)
        {
            if (!_predecessors.TryGetValue(v, out var preds))
                return null;
            var sucs = _successors.TryGetValue(v, out var sucsDict) ? sucsDict : null;
            if (sucs == null || sucs.Count == 0)
                return preds.Keys.ToArray();
            var set = new HashSet<string>(preds.Keys, StringComparer.Ordinal);
            foreach (var s in sucs.Keys)
                set.Add(s);
            var result = new string[set.Count];
            set.CopyTo(result);
            return result;
        }

        public string[] Predecessors(string v)
        {
            if (_predecessors.TryGetValue(v, out var preds))
            {
                return preds.Keys.ToArray();
            }
            return null;
        }

        public Dictionary<string, EdgeLabel> _edgeLabels = new Dictionary<string, EdgeLabel>(StringComparer.Ordinal);
        public DagreEdgeIndex[] Edges()
        {
            return EdgesRaw();
        }

        public DagreEdgeIndex[] EdgesRaw()
        {
            if (_edgesCache == null)
            {
                var vals = _edgeObjs.Values;
                var result = new DagreEdgeIndex[vals.Count];
                vals.CopyTo(result, 0);
                _edgesCache = result;
            }
            return _edgesCache;
        }
        DagreEdgeIndex[] _edgesCache;
        internal void InvalidateEdgesCache() => _edgesCache = null;
        internal DagreGraph RemoveEdge(DagreEdgeIndex edgeIndex)
        {
            return RemoveEdgeByKey(EdgeArgsToId(_isDirected, edgeIndex.v, edgeIndex.w, edgeIndex.name));
        }

        internal DagreGraph RemoveEdge(string v, string w, string name = null)
        {
            return RemoveEdgeByKey(EdgeArgsToId(_isDirected, v, w, name));
        }

        private DagreGraph RemoveEdgeByKey(string key)
        {
            if (!_edgeObjs.TryGetValue(key, out var edge)) return this;
            var v = edge.v;
            var w = edge.w;
            _edgeLabels.Remove(key);
            _edgeObjs.Remove(key);
            var predsW = _predecessors[w];
            var val = predsW[v] - 1;
            if (val == 0)
                predsW.Remove(v);
            else
                predsW[v] = val;

            var sucsV = _successors[v];
            var val2 = sucsV[w] - 1;
            if (val2 == 0)
                sucsV.Remove(w);
            else
                sucsV[w] = val2;

            _in[w].Remove(key);
            _out[v].Remove(key);
            _edgeCount--;
            InvalidateEdgesCache();
            return this;
        }




        public int _edgeCount;


        internal string[] Sources()
        {
            var nodes = NodesRaw();
            var count = 0;
            for (var i = 0; i < nodes.Length; i++)
            {
                if (_in.TryGetValue(nodes[i], out var inDict) && inDict.Count == 0)
                    count++;
            }
            var result = new string[count];
            var idx = 0;
            for (var i = 0; i < nodes.Length; i++)
            {
                if (_in.TryGetValue(nodes[i], out var inDict) && inDict.Count == 0)
                    result[idx++] = nodes[i];
            }
            return result;
        }

        internal bool HasEdge(string v, string w, string name = null)
        {
            return EdgeRaw(v, w, name) != null;
        }

        public Dictionary<string, DagreEdgeIndex> _edgeObjs = new Dictionary<string, DagreEdgeIndex>(StringComparer.Ordinal);

        private Func<string, string, string, EdgeLabel> _defaultEdgeLabelFn = (x, y, z) => new EdgeLabel();

        internal bool HasNode(string v)
        {
            return _nodesRaw.ContainsKey(v);
        }

        public string[] Successors(string v)
        {
            if (_successors.TryGetValue(v, out var sucs))
            {
                return sucs.Keys.ToArray();
            }
            return null;
        }
        internal string[] Children(string v = null)
        {
            v ??= GRAPH_NODE;
            if (_isCompound)
            {
                if (_children.TryGetValue(v, out var children))
                {
                    return children.Keys.ToArray();
                }
            }
            else if (v == GRAPH_NODE)
            {
                return Nodes();
            }
            else if (HasNode(v))
            {
                return Array.Empty<string>();
            }
            return Array.Empty<string>();
        }



        internal DagreEdgeIndex[] OutEdges(string v, string w = null)
        {
            if (_out.TryGetValue(v, out var outDict) && outDict.Count > 0)
            {
                if (w == null)
                {
                    var result = new DagreEdgeIndex[outDict.Count];
                    outDict.Values.CopyTo(result, 0);
                    return result;
                }
                int count = 0;
                foreach (var edge in outDict.Values)
                    if (edge.w == w) count++;
                if (count == 0) return Array.Empty<DagreEdgeIndex>();
                var filtered = new DagreEdgeIndex[count];
                int j = 0;
                foreach (var edge in outDict.Values)
                    if (edge.w == w) filtered[j++] = edge;
                return filtered;
            }
            return Array.Empty<DagreEdgeIndex>();
        }

        internal DagreEdgeIndex[] NodeEdges(string v, string w = null)
        {
            var inEdges = this.InEdges(v, w);
            if (inEdges != null)
            {
                var outEdges = OutEdges(v, w);
                var result = new DagreEdgeIndex[inEdges.Length + outEdges.Length];
                inEdges.CopyTo(result, 0);
                outEdges.CopyTo(result, inEdges.Length);
                return result;
            }
            return null;
        }

        internal EdgeLabel EdgeRaw(DagreEdgeIndex e)
        {
            var key = EdgeArgsToId(_isDirected, e.v, e.w, e.name);
            return _edgeLabels.TryGetValue(key, out var value) ? value : null;
        }
        internal DagreGraph RemoveNode(string v)
        {
            if (HasNode(v))
            {
                _nodesRaw.Remove(v);
                InvalidateNodesCache();
                if (_isCompound)
                {
                    if (_parent.TryGetValue(v, out var parentVal))
                    {
                        _children[parentVal].Remove(v);
                        _parent.Remove(v);
                    }
                    else if (_children.TryGetValue(GRAPH_NODE, out var graphNodeChildren))
                    {
                        graphNodeChildren.Remove(v);
                    }
                    foreach (var child in Children(v))
                    {
                        SetParent(child);
                    }
                    _children.Remove(v);
                }
                var inDict = _in[v];
                var keys2 = new string[inDict.Count];
                inDict.Keys.CopyTo(keys2, 0);
                foreach (var e in keys2)
                {
                    RemoveEdge(_edgeObjs[e]);
                }
                _in.Remove(v);
                _predecessors.Remove(v);

                var outDict = _out[v];
                var keys = new string[outDict.Count];
                outDict.Keys.CopyTo(keys, 0);
                foreach (var e in keys)
                {
                    RemoveEdge(_edgeObjs[e]);
                }
                _out.Remove(v);
                _successors.Remove(v);
                --_nodeCount;
            }
            return this;
        }

        internal int NodeCount()
        {
            return _nodeCount;
        }

        internal DagreEdgeIndex[] InEdges(string v, string u = null)
        {
            if (_in.TryGetValue(v, out var inDict))
            {
                if (u == null)
                {
                    var result = new DagreEdgeIndex[inDict.Count];
                    inDict.Values.CopyTo(result, 0);
                    return result;
                }
                int count = 0;
                foreach (var edge in inDict.Values)
                    if (edge.v == u) count++;
                if (count == 0) return Array.Empty<DagreEdgeIndex>();
                var filtered = new DagreEdgeIndex[count];
                int j = 0;
                foreach (var edge in inDict.Values)
                    if (edge.v == u) filtered[j++] = edge;
                return filtered;
            }
            return null;
        }

            public Func<string, NodeLabel> _defaultNodeLabelFn = (t) => new NodeLabel();


        public DagreGraph SetNode(string v, NodeLabel o2 = null)
        {
            var key = v;
            if (_nodesRaw.ContainsKey(key))
            {
                if (o2 != null)
                    _nodesRaw[key] = o2;
                return this;
            }

            _nodesRaw[key] = o2 ?? (NodeLabel)_defaultNodeLabelFn(v);
            InvalidateNodesCache();
            if (_isCompound)
            {
                _parent[key] = GRAPH_NODE;
                _children[key] = new Dictionary<string, bool>(StringComparer.Ordinal);
                if (!_children.ContainsKey(GRAPH_NODE))
                    _children.Add(GRAPH_NODE, new Dictionary<string, bool>(StringComparer.Ordinal));
                _children[GRAPH_NODE][key] = true;
            }

            _in[key] = new Dictionary<string, DagreEdgeIndex>(StringComparer.Ordinal);
            _predecessors[key] = new Dictionary<string, int>(StringComparer.Ordinal);
            _out[key] = new Dictionary<string, DagreEdgeIndex>(StringComparer.Ordinal);
            _successors[key] = new Dictionary<string, int>(StringComparer.Ordinal);

            _nodeCount++;
            return this;
        }


        int _nodeCount;

        public DagreGraph SetGraph(GraphLabel v)
        {
            _label = v;
            return this;
        }
        public void SetParent(string v, string parent = null)
        {
            if (!_isCompound)
            {
                throw new DagreException("cannot set parent in non-compound graph");
            }
            if (parent == null)
            {
                parent = GRAPH_NODE;
            }
            else
            {
                parent += "";
                for (var ancestor = parent; ancestor != null; ancestor = Parent(ancestor))
                {
                    if (ancestor == v)
                    {
                        throw new DagreException("Setting " + parent + " as parent of " + v + " would create a cycle.");
                    }
                }
                this.SetNode(parent);
            }
            SetNode(v);
            if (_parent.TryGetValue(v, out var oldParent))
            {
                _children[oldParent].Remove(v);
                _parent[v] = parent;
            }
            _children[parent][v] = true;
        }



        public Dictionary<string, Dictionary<string, bool>> _children = new Dictionary<string, Dictionary<string, bool>>(StringComparer.Ordinal);
        public Dictionary<string, Dictionary<string, int>> _predecessors = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        public Dictionary<string, Dictionary<string, int>> _successors = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        public Dictionary<string, Dictionary<string, DagreEdgeIndex>> _in = new Dictionary<string, Dictionary<string, DagreEdgeIndex>>(StringComparer.Ordinal);
        public Dictionary<string, Dictionary<string, DagreEdgeIndex>> _out = new Dictionary<string, Dictionary<string, DagreEdgeIndex>>(StringComparer.Ordinal);
        public static string GRAPH_NODE = "\x00";

        public Dictionary<string, string> _parent = new Dictionary<string, string>(StringComparer.Ordinal);
        internal string Parent(string v)
        {
            if (_isCompound)
            {
                if (_parent.TryGetValue(v, out var parentStr))
                {
                    if (parentStr != GRAPH_NODE)
                        return parentStr;
                }
            }
            return null;
        }
    }

    public class SelfEdgeInfo
    {
        public EdgeLabel label;
        public DagreEdgeIndex e;
    }

    public class DagreEdgeIndex
    {
        public string v;
        public string w;
        public string name;
    }

    public class DagreException : Exception
    {
        public DagreException() { }
        public DagreException(string str) : base(str)
        {
        }
    }
}
