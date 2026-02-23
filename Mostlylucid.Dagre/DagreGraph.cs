namespace Mostlylucid.Dagre;

public class DagreGraph
{
    public const string EDGE_KEY_DELIM = "\x01";
    public const string DEFAULT_EDGE_NAME = "\x00";
    public static string GRAPH_NODE = "\x00";

    private readonly Func<string, string, string, EdgeLabel> _defaultEdgeLabelFn = (x, y, z) => new EdgeLabel();

    // --- Batch mode: suppresses cache invalidation for bulk operations ---
    private int _batchDepth;


    public Dictionary<string, Dictionary<string, bool>> _children = new(StringComparer.Ordinal);

    public Func<string, NodeLabel> _defaultNodeLabelFn = t => new NodeLabel();


    public int _edgeCount;

    public Dictionary<string, EdgeLabel> _edgeLabels = new(StringComparer.Ordinal);

    public Dictionary<string, DagreEdgeIndex> _edgeObjs = new(StringComparer.Ordinal);

    private DagreEdgeIndex[] _edgesCache;

    public List<DagreEdgeIndex> _edgesIndexes = new();
    public Dictionary<string, Dictionary<string, DagreEdgeIndex>> _in = new(StringComparer.Ordinal);
    public bool _isCompound;

    public bool _isDirected = true;
    public bool _isMultigraph = false;

    private GraphLabel _label = new();


    private int _nodeCount;

    private string[] _nodesCache;

    public Dictionary<string, NodeLabel> _nodesRaw = new(StringComparer.Ordinal);
    public Dictionary<string, Dictionary<string, DagreEdgeIndex>> _out = new(StringComparer.Ordinal);

    public Dictionary<string, string> _parent = new(StringComparer.Ordinal);
    public Dictionary<string, Dictionary<string, int>> _predecessors = new(StringComparer.Ordinal);
    public Dictionary<string, Dictionary<string, int>> _successors = new(StringComparer.Ordinal);

    public DagreGraph(bool compound)
    {
        _isCompound = compound;
        if (compound) _children.Add(GRAPH_NODE, new Dictionary<string, bool>(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Enter batch mode. Cache invalidation is deferred until EndBatch().
    ///     Calls can be nested.
    /// </summary>
    public void BeginBatch()
    {
        _batchDepth++;
    }

    /// <summary>
    ///     Exit batch mode. When the outermost batch ends, caches are invalidated.
    /// </summary>
    public void EndBatch()
    {
        if (--_batchDepth <= 0)
        {
            _batchDepth = 0;
            _nodesCache = null;
            _edgesCache = null;
        }
    }

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

    internal void InvalidateNodesCache()
    {
        if (_batchDepth == 0) _nodesCache = null;
    }

    public GraphLabel Graph()
    {
        return _label;
    }


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

    public static string EdgeArgsToId(bool isDirected, string v, string w, string name)
    {
        if (!isDirected && string.CompareOrdinal(v, w) > 0)
            (v, w) = (w, v);
        var n = name ?? DEFAULT_EDGE_NAME;
        var totalLen = v.Length + 1 + w.Length + 1 + n.Length;
        return string.Create(totalLen, (v, w, n), static (span, state) =>
        {
            state.v.AsSpan().CopyTo(span);
            span[state.v.Length] = '\x01';
            state.w.AsSpan().CopyTo(span[(state.v.Length + 1)..]);
            span[state.v.Length + 1 + state.w.Length] = '\x01';
            state.n.AsSpan().CopyTo(span[(state.v.Length + 1 + state.w.Length + 1)..]);
        });
    }

    /// <summary>
    ///     SetEdge — most common 2-arg form.
    /// </summary>
    public DagreGraph SetEdge(string v, string w)
    {
        return SetEdgeInternal(v, w, null, null, false);
    }

    /// <summary>
    ///     SetEdge with label.
    /// </summary>
    public DagreGraph SetEdge(string v, string w, EdgeLabel label)
    {
        return SetEdgeInternal(v, w, label, null, true);
    }

    /// <summary>
    ///     SetEdge with label and name.
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

        var edgeObj = new DagreEdgeIndex { v = v, w = w, name = name, _key = e };
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
    ///     Get edge label by source, target, and optional name.
    /// </summary>
    internal EdgeLabel EdgeRaw(string v, string w, string name = null)
    {
        var key = EdgeArgsToId(_isDirected, v, w, name);
        return _edgeLabels.TryGetValue(key, out var value) ? value : null;
    }

    public EdgeLabel Edge(DagreEdgeIndex v)
    {
        if (v._key != null)
            return _edgeLabels.TryGetValue(v._key, out var label) ? label : null;
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
        {
            var result = new string[preds.Count];
            preds.Keys.CopyTo(result, 0);
            return result;
        }

        // Merge preds + sucs without HashSet allocation for small counts
        var totalCapacity = preds.Count + sucs.Count;
        var merged = new List<string>(totalCapacity);
        foreach (var k in preds.Keys)
            merged.Add(k);
        foreach (var k in sucs.Keys)
            if (!preds.ContainsKey(k))
                merged.Add(k);
        return merged.ToArray();
    }

    public string[] Predecessors(string v)
    {
        if (_predecessors.TryGetValue(v, out var preds)) return preds.Keys.ToArray();
        return null;
    }

    /// <summary>
    ///     Returns predecessor keys without allocating a new array.
    ///     For internal hot-path iteration only.
    /// </summary>
    internal Dictionary<string, int>.KeyCollection PredecessorKeys(string v)
    {
        return _predecessors.TryGetValue(v, out var preds) ? preds.Keys : null;
    }

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

    internal void InvalidateEdgesCache()
    {
        if (_batchDepth == 0) _edgesCache = null;
    }

    internal DagreGraph RemoveEdge(DagreEdgeIndex edgeIndex)
    {
        return RemoveEdgeByKey(edgeIndex._key ?? EdgeArgsToId(_isDirected, edgeIndex.v, edgeIndex.w, edgeIndex.name));
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


    internal string[] Sources()
    {
        var nodes = NodesRaw();
        var count = 0;
        for (var i = 0; i < nodes.Length; i++)
            if (_in.TryGetValue(nodes[i], out var inDict) && inDict.Count == 0)
                count++;
        var result = new string[count];
        var idx = 0;
        for (var i = 0; i < nodes.Length; i++)
            if (_in.TryGetValue(nodes[i], out var inDict) && inDict.Count == 0)
                result[idx++] = nodes[i];
        return result;
    }

    internal bool HasEdge(string v, string w, string name = null)
    {
        return EdgeRaw(v, w, name) != null;
    }

    internal bool HasNode(string v)
    {
        return _nodesRaw.ContainsKey(v);
    }

    public string[] Successors(string v)
    {
        if (_successors.TryGetValue(v, out var sucs)) return sucs.Keys.ToArray();
        return null;
    }

    /// <summary>
    ///     Returns successor keys without allocating a new array.
    ///     For internal hot-path iteration only.
    /// </summary>
    internal Dictionary<string, int>.KeyCollection SuccessorKeys(string v)
    {
        return _successors.TryGetValue(v, out var sucs) ? sucs.Keys : null;
    }

    /// <summary>
    ///     Returns the first successor without allocating an array.
    /// </summary>
    internal string FirstSuccessor(string v)
    {
        if (_successors.TryGetValue(v, out var sucs))
            foreach (var key in sucs.Keys)
                return key;
        return null;
    }

    /// <summary>
    ///     Returns the first predecessor without allocating an array.
    /// </summary>
    internal string FirstPredecessor(string v)
    {
        if (_predecessors.TryGetValue(v, out var preds))
            foreach (var key in preds.Keys)
                return key;
        return null;
    }

    /// <summary>
    ///     Returns predecessor count without allocating.
    /// </summary>
    internal int PredecessorCount(string v)
    {
        return _predecessors.TryGetValue(v, out var preds) ? preds.Count : 0;
    }

    /// <summary>
    ///     Returns successor count without allocating.
    /// </summary>
    internal int SuccessorCount(string v)
    {
        return _successors.TryGetValue(v, out var sucs) ? sucs.Count : 0;
    }

    internal string[] Children(string v = null)
    {
        v ??= GRAPH_NODE;
        if (_isCompound)
        {
            if (_children.TryGetValue(v, out var children)) return children.Keys.ToArray();
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

    /// <summary>
    ///     Checks if a node has children without allocating an array.
    /// </summary>
    internal bool HasChildren(string v)
    {
        if (_isCompound) return _children.TryGetValue(v, out var children) && children.Count > 0;
        return false;
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

            var count = 0;
            foreach (var edge in outDict.Values)
                if (edge.w == w)
                    count++;
            if (count == 0) return Array.Empty<DagreEdgeIndex>();
            var filtered = new DagreEdgeIndex[count];
            var j = 0;
            foreach (var edge in outDict.Values)
                if (edge.w == w)
                    filtered[j++] = edge;
            return filtered;
        }

        return Array.Empty<DagreEdgeIndex>();
    }

    internal DagreEdgeIndex[] NodeEdges(string v, string w = null)
    {
        if (!_in.TryGetValue(v, out var inDict))
            return null;

        var outDict = _out.TryGetValue(v, out var od) ? od : null;
        var inCount = w == null ? inDict.Count : CountEdgesFiltered(inDict, w, true);
        var outCount = w == null
            ? outDict?.Count ?? 0
            : outDict != null
                ? CountEdgesFiltered(outDict, w, false)
                : 0;

        if (inCount + outCount == 0)
            return Array.Empty<DagreEdgeIndex>();

        var result = new DagreEdgeIndex[inCount + outCount];
        var idx = 0;
        if (w == null)
        {
            foreach (var e in inDict.Values)
                result[idx++] = e;
            if (outDict != null)
                foreach (var e in outDict.Values)
                    result[idx++] = e;
        }
        else
        {
            foreach (var e in inDict.Values)
                if (e.v == w)
                    result[idx++] = e;
            if (outDict != null)
                foreach (var e in outDict.Values)
                    if (e.w == w)
                        result[idx++] = e;
        }

        return result;
    }

    private static int CountEdgesFiltered(Dictionary<string, DagreEdgeIndex> dict, string target, bool matchV)
    {
        var count = 0;
        foreach (var e in dict.Values)
            if (matchV ? e.v == target : e.w == target)
                count++;
        return count;
    }

    internal EdgeLabel EdgeRaw(DagreEdgeIndex e)
    {
        var key = e._key ?? EdgeArgsToId(_isDirected, e.v, e.w, e.name);
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

                foreach (var child in Children(v)) SetParent(child);
                _children.Remove(v);
            }

            var inDict = _in[v];
            var keys2 = new string[inDict.Count];
            inDict.Keys.CopyTo(keys2, 0);
            foreach (var e in keys2) RemoveEdge(_edgeObjs[e]);
            _in.Remove(v);
            _predecessors.Remove(v);

            var outDict = _out[v];
            var keys = new string[outDict.Count];
            outDict.Keys.CopyTo(keys, 0);
            foreach (var e in keys) RemoveEdge(_edgeObjs[e]);
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

            var count = 0;
            foreach (var edge in inDict.Values)
                if (edge.v == u)
                    count++;
            if (count == 0) return Array.Empty<DagreEdgeIndex>();
            var filtered = new DagreEdgeIndex[count];
            var j = 0;
            foreach (var edge in inDict.Values)
                if (edge.v == u)
                    filtered[j++] = edge;
            return filtered;
        }

        return null;
    }

    /// <summary>
    ///     Returns in-edge values without allocating an array. For internal hot-path iteration.
    /// </summary>
    internal Dictionary<string, DagreEdgeIndex>.ValueCollection InEdgeValues(string v)
    {
        return _in.TryGetValue(v, out var inDict) && inDict.Count > 0 ? inDict.Values : null;
    }

    /// <summary>
    ///     Returns out-edge values without allocating an array. For internal hot-path iteration.
    /// </summary>
    internal Dictionary<string, DagreEdgeIndex>.ValueCollection OutEdgeValues(string v)
    {
        return _out.TryGetValue(v, out var outDict) && outDict.Count > 0 ? outDict.Values : null;
    }

    /// <summary>
    ///     Returns in-edge count without allocating.
    /// </summary>
    internal int InEdgeCount(string v)
    {
        return _in.TryGetValue(v, out var inDict) ? inDict.Count : 0;
    }


    public DagreGraph SetNode(string v, NodeLabel o2 = null)
    {
        var key = v;
        if (_nodesRaw.ContainsKey(key))
        {
            if (o2 != null)
                _nodesRaw[key] = o2;
            return this;
        }

        _nodesRaw[key] = o2 ?? _defaultNodeLabelFn(v);
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

    /// <summary>
    ///     Fast SetNode for dummy nodes that are guaranteed to be new.
    ///     Uses capacity-1 inner dictionaries since dummies typically have 1 in + 1 out edge.
    ///     Skips existence check and cache invalidation (use inside BeginBatch/EndBatch).
    /// </summary>
    internal void SetNodeDummy(string key, NodeLabel attrs)
    {
        _nodesRaw[key] = attrs;
        if (_isCompound)
        {
            _parent[key] = GRAPH_NODE;
            _children[key] = new Dictionary<string, bool>(0, StringComparer.Ordinal);
            _children[GRAPH_NODE][key] = true;
        }

        _in[key] = new Dictionary<string, DagreEdgeIndex>(1, StringComparer.Ordinal);
        _predecessors[key] = new Dictionary<string, int>(1, StringComparer.Ordinal);
        _out[key] = new Dictionary<string, DagreEdgeIndex>(1, StringComparer.Ordinal);
        _successors[key] = new Dictionary<string, int>(1, StringComparer.Ordinal);
        _nodeCount++;
    }

    /// <summary>
    ///     Fast SetEdge for new edges between nodes that are guaranteed to exist.
    ///     Skips node existence checks and cache invalidation (use inside BeginBatch/EndBatch).
    /// </summary>
    internal void SetEdgeFast(string v, string w, EdgeLabel value, string name)
    {
        var e = EdgeArgsToId(_isDirected, v, w, name);
        _edgeLabels[e] = value;

        if (!_isDirected && string.CompareOrdinal(v, w) > 0)
            (v, w) = (w, v);

        var edgeObj = new DagreEdgeIndex { v = v, w = w, name = name, _key = e };
        _edgeObjs[e] = edgeObj;

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
    }

    /// <summary>
    ///     Fast removal of a dummy chain node (exactly 1 in-edge, 1 out-edge, no children).
    ///     Removes the node and its incident edges without cascading RemoveEdge calls.
    ///     Use inside BeginBatch/EndBatch.
    /// </summary>
    internal void RemoveChainNode(string v)
    {
        // Get the single in-edge and single out-edge
        var inDict = _in[v];
        var outDict = _out[v];

        // Remove in-edges
        foreach (var kvp in inDict)
        {
            var edgeKey = kvp.Key;
            var edge = kvp.Value;
            _edgeLabels.Remove(edgeKey);
            _edgeObjs.Remove(edgeKey);
            // Update predecessor/successor counts
            var predsV = _predecessors[v];
            predsV.Remove(edge.v);
            var sucsU = _successors[edge.v];
            if (sucsU.TryGetValue(v, out var cnt) && cnt <= 1)
                sucsU.Remove(v);
            else if (cnt > 1)
                sucsU[v] = cnt - 1;
            _out[edge.v].Remove(edgeKey);
            _edgeCount--;
        }

        // Remove out-edges
        foreach (var kvp in outDict)
        {
            var edgeKey = kvp.Key;
            var edge = kvp.Value;
            _edgeLabels.Remove(edgeKey);
            _edgeObjs.Remove(edgeKey);
            var sucsV = _successors[v];
            sucsV.Remove(edge.w);
            var predsW = _predecessors[edge.w];
            if (predsW.TryGetValue(v, out var cnt) && cnt <= 1)
                predsW.Remove(v);
            else if (cnt > 1)
                predsW[v] = cnt - 1;
            _in[edge.w].Remove(edgeKey);
            _edgeCount--;
        }

        // Remove node
        _nodesRaw.Remove(v);
        _in.Remove(v);
        _out.Remove(v);
        _predecessors.Remove(v);
        _successors.Remove(v);

        if (_isCompound)
        {
            if (_parent.TryGetValue(v, out var parentVal))
            {
                if (_children.TryGetValue(parentVal, out var parentChildren))
                    parentChildren.Remove(v);
                _parent.Remove(v);
            }

            _children.Remove(v);
        }

        _nodeCount--;
    }

    /// <summary>
    ///     Fast bulk removal of all dummy chain nodes and their edges.
    ///     Skips adjacency cleanup (_in/_out/_predecessors/_successors) since these
    ///     are not used after Normalize.Undo. Only removes from core dictionaries
    ///     (_nodesRaw, _edgeLabels, _edgeObjs) and invalidates caches.
    /// </summary>
    internal void BulkRemoveDummyChains(List<string> dummyChains)
    {
        // Collect all dummy node IDs
        var dummyNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chainStart in dummyChains)
        {
            var v = chainStart;
            while (v != null)
            {
                var node = _nodesRaw.TryGetValue(v, out var nl) ? nl : null;
                if (node == null || node.Dummy == null) break;
                dummyNodeIds.Add(v);
                // Follow successor via _out dict
                if (_out.TryGetValue(v, out var outEdges))
                {
                    string nextV = null;
                    foreach (var kvp in outEdges)
                    {
                        nextV = kvp.Value.w;
                        break;
                    }

                    v = nextV;
                }
                else
                {
                    break;
                }
            }
        }

        if (dummyNodeIds.Count == 0) return;

        // Remove all edges involving dummy nodes from core dicts
        var edgeKeysToRemove = new List<string>();
        foreach (var kvp in _edgeObjs)
            if (dummyNodeIds.Contains(kvp.Value.v) || dummyNodeIds.Contains(kvp.Value.w))
                edgeKeysToRemove.Add(kvp.Key);
        foreach (var key in edgeKeysToRemove)
        {
            _edgeLabels.Remove(key);
            _edgeObjs.Remove(key);
            _edgeCount--;
        }

        // Remove dummy nodes from core dict
        foreach (var id in dummyNodeIds)
        {
            _nodesRaw.Remove(id);
            // Also clean up compound parent references
            if (_isCompound)
            {
                if (_parent.TryGetValue(id, out var parentVal) &&
                    _children.TryGetValue(parentVal, out var parentChildren))
                    parentChildren.Remove(id);
                _parent.Remove(id);
                _children.Remove(id);
            }

            // Remove adjacency dicts (cheap, just dict removal, no cascading)
            _in.Remove(id);
            _out.Remove(id);
            _predecessors.Remove(id);
            _successors.Remove(id);
            _nodeCount--;
        }

        // Invalidate caches
        _nodesCache = null;
        _edgesCache = null;
    }

    public DagreGraph SetGraph(GraphLabel v)
    {
        _label = v;
        return this;
    }

    public void SetParent(string v, string parent = null)
    {
        if (!_isCompound) throw new DagreException("cannot set parent in non-compound graph");
        if (parent == null)
        {
            parent = GRAPH_NODE;
        }
        else
        {
            parent += "";
            for (var ancestor = parent; ancestor != null; ancestor = Parent(ancestor))
                if (ancestor == v)
                    throw new DagreException("Setting " + parent + " as parent of " + v + " would create a cycle.");

            SetNode(parent);
        }

        SetNode(v);
        if (_parent.TryGetValue(v, out var oldParent))
        {
            _children[oldParent].Remove(v);
            _parent[v] = parent;
        }

        _children[parent][v] = true;
    }

    internal string Parent(string v)
    {
        if (_isCompound)
            if (_parent.TryGetValue(v, out var parentStr))
                if (parentStr != GRAPH_NODE)
                    return parentStr;

        return null;
    }
}

public class SelfEdgeInfo
{
    public DagreEdgeIndex e;
    public EdgeLabel label;
}

public class DagreEdgeIndex
{
    /// <summary>Cached edge key to avoid repeated string concatenation on lookups.</summary>
    internal string _key;

    public string name;
    public string v;
    public string w;
}

public class DagreException : Exception
{
    public DagreException()
    {
    }

    public DagreException(string str) : base(str)
    {
    }
}