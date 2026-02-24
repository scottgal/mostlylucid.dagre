using System.Numerics;

namespace Mostlylucid.Dagre.Indexed;

/// <summary>
///     Structure-of-Arrays graph for int-indexed layout algorithms.
///     Replaces DagreGraph's nested dictionaries with flat arrays for hot-path performance.
/// </summary>
internal sealed class IndexedGraph
{
    private int _edgeCapacity;

    // Capacity tracking for growth
    private int _nodeCapacity;

    // Current counts
    private Dictionary<string, int> _nodeIndexById;
    public bool[] EdgeDead; // marks edges removed by Normalize

    // Edge SoA arrays (indexed by edge int)
    public int[] EdgeSource, EdgeTarget;
    public int[] EdgeWeight, EdgeMinlen, EdgeCutvalue;
    public float[] EdgeWidth, EdgeHeight;
    public int[] InEdgeList;
    public int[] InEdgeStart, InEdgeCount;
    public string[] NodeBorderType;
    public string[] NodeDummy; // null, "edge", "border", "selfedge", etc.

    // Normalize support: object refs for dummy nodes
    public EdgeLabel[] NodeEdgeLabelRef; // for dummy nodes: the original EdgeLabel
    public DagreEdgeIndex[] NodeEdgeObjRef; // for dummy nodes: the original DagreEdgeIndex

    // String <-> int mapping (used at boundaries only)
    public string[] NodeIdByIndex;
    public string[] NodeLabelPos;
    public int[] NodeLow, NodeLim;
    public int[] NodeMinRank, NodeMaxRank;
    public string[] NodeParentStr; // tree parent as string (for network simplex tree)
    public int[] NodeRank, NodeOrder;

    // Graph config (copied from GraphLabel)
    public int NodeSep, EdgeSep, RankSep;

    // Node SoA arrays (indexed by node int)
    public float[] NodeX, NodeY, NodeWidth, NodeHeight;
    public int[] OutEdgeList;

    // Adjacency (flat edge-index lists per node)
    // OutEdgeList[OutEdgeStart[n] .. OutEdgeStart[n]+OutEdgeCount[n]] = edge indices leaving n
    public int[] OutEdgeStart, OutEdgeCount;

    public int NodeCount { get; private set; }

    public int EdgeCount { get; private set; }

    /// <summary>
    ///     Set node/edge counts (used by Simplify to build a derived graph).
    /// </summary>
    internal void SetCounts(int nodeCount, int edgeCount)
    {
        NodeCount = nodeCount;
        EdgeCount = edgeCount;
    }

    /// <summary>
    ///     Build an IndexedGraph from a DagreGraph, excluding compound parent nodes.
    ///     Equivalent to FromDagreGraph(Util.AsNonCompoundGraph(g)) but avoids creating
    ///     the intermediate DagreGraph copy.
    /// </summary>
    public static IndexedGraph FromDagreGraphNonCompound(DagreGraph g)
    {
        // Check if any nodes have children; if not, this is equivalent to FromDagreGraph
        var hasCompound = false;
        foreach (var v in g.NodesRaw())
            if (g.HasChildren(v))
            {
                hasCompound = true;
                break;
            }

        if (!hasCompound) return FromDagreGraph(g);

        // Build a filtered view: skip compound parent nodes
        var ig = new IndexedGraph();
        var allNodes = g.NodesRaw();
        var leafNodes = new List<string>(allNodes.Length);
        foreach (var v in allNodes)
            if (!g.HasChildren(v))
                leafNodes.Add(v);

        var nc = leafNodes.Count;
        ig.NodeCount = nc;
        ig._nodeCapacity = Math.Max(nc, 16);

        // Allocate node arrays
        ig.NodeX = new float[ig._nodeCapacity];
        ig.NodeY = new float[ig._nodeCapacity];
        ig.NodeWidth = new float[ig._nodeCapacity];
        ig.NodeHeight = new float[ig._nodeCapacity];
        ig.NodeRank = new int[ig._nodeCapacity];
        ig.NodeOrder = new int[ig._nodeCapacity];
        ig.NodeLow = new int[ig._nodeCapacity];
        ig.NodeLim = new int[ig._nodeCapacity];
        ig.NodeMinRank = new int[ig._nodeCapacity];
        ig.NodeMaxRank = new int[ig._nodeCapacity];
        ig.NodeParentStr = new string[ig._nodeCapacity];
        ig.NodeDummy = new string[ig._nodeCapacity];
        ig.NodeLabelPos = new string[ig._nodeCapacity];
        ig.NodeBorderType = new string[ig._nodeCapacity];
        ig.NodeIdByIndex = new string[ig._nodeCapacity];
        ig._nodeIndexById = new Dictionary<string, int>(nc, StringComparer.Ordinal);

        for (var i = 0; i < nc; i++)
        {
            var id = leafNodes[i];
            ig.NodeIdByIndex[i] = id;
            ig._nodeIndexById[id] = i;
            var nl = g.NodeRaw(id);
            if (nl == null) continue;
            ig.NodeX[i] = nl.X;
            ig.NodeY[i] = nl.Y;
            ig.NodeWidth[i] = nl.Width;
            ig.NodeHeight[i] = nl.Height;
            ig.NodeRank[i] = nl.Rank;
            ig.NodeOrder[i] = nl.Order;
            ig.NodeLow[i] = nl.Low;
            ig.NodeLim[i] = nl.Lim;
            ig.NodeMinRank[i] = nl.MinRank;
            ig.NodeMaxRank[i] = nl.MaxRank;
            ig.NodeParentStr[i] = nl.Parent;
            ig.NodeDummy[i] = nl.Dummy;
            ig.NodeLabelPos[i] = nl.LabelPos;
            ig.NodeBorderType[i] = nl.BorderType;
        }

        // Edges: only include edges between leaf nodes
        var edges = g.EdgesRaw();
        var edgeList = new List<(int src, int tgt, EdgeLabel el)>(edges.Length);
        foreach (var e in edges)
            if (ig._nodeIndexById.TryGetValue(e.v, out var srcIdx) &&
                ig._nodeIndexById.TryGetValue(e.w, out var tgtIdx))
                edgeList.Add((srcIdx, tgtIdx, g.EdgeRaw(e)));

        var ec = edgeList.Count;
        ig.EdgeCount = ec;
        ig._edgeCapacity = Math.Max(ec, 16);
        ig.EdgeSource = new int[ig._edgeCapacity];
        ig.EdgeTarget = new int[ig._edgeCapacity];
        ig.EdgeWeight = new int[ig._edgeCapacity];
        ig.EdgeMinlen = new int[ig._edgeCapacity];
        ig.EdgeCutvalue = new int[ig._edgeCapacity];
        ig.EdgeWidth = new float[ig._edgeCapacity];
        ig.EdgeHeight = new float[ig._edgeCapacity];

        for (var i = 0; i < ec; i++)
        {
            var (src, tgt, el) = edgeList[i];
            ig.EdgeSource[i] = src;
            ig.EdgeTarget[i] = tgt;
            if (el != null)
            {
                ig.EdgeWeight[i] = el.Weight;
                ig.EdgeMinlen[i] = el.Minlen;
                ig.EdgeCutvalue[i] = el.Cutvalue;
                ig.EdgeWidth[i] = el.Width;
                ig.EdgeHeight[i] = el.Height;
            }
        }

        var gl = g.Graph();
        ig.NodeSep = gl.NodeSep;
        ig.EdgeSep = gl.EdgeSep;
        ig.RankSep = gl.RankSep;
        ig.RebuildAdjacency();
        return ig;
    }

    /// <summary>
    ///     Swap NodeWidth and NodeHeight arrays (for CoordinateSystem.Adjust on LR/RL layouts).
    /// </summary>
    public void SwapWidthHeight()
    {
        for (var i = 0; i < NodeCount; i++) (NodeWidth[i], NodeHeight[i]) = (NodeHeight[i], NodeWidth[i]);
        for (var i = 0; i < EdgeCount; i++) (EdgeWidth[i], EdgeHeight[i]) = (EdgeHeight[i], EdgeWidth[i]);
    }

    /// <summary>
    ///     Refresh node data from DagreGraph without rebuilding mappings or adjacency.
    ///     Used when DagreGraph has been modified (e.g., InsertSelfEdges updated orders)
    ///     but the graph structure (nodes/edges) hasn't changed.
    /// </summary>
    public void RefreshFromDagreGraph(DagreGraph g)
    {
        for (var i = 0; i < NodeCount; i++)
        {
            var id = NodeIdByIndex[i];
            if (id == null) continue;
            var nl = g.NodeRaw(id);
            if (nl == null) continue;
            NodeX[i] = nl.X;
            NodeY[i] = nl.Y;
            NodeWidth[i] = nl.Width;
            NodeHeight[i] = nl.Height;
            NodeRank[i] = nl.Rank;
            NodeOrder[i] = nl.Order;
        }
    }

    /// <summary>
    ///     Build an IndexedGraph from a DagreGraph (one-time conversion).
    /// </summary>
    public static IndexedGraph FromDagreGraph(DagreGraph g)
    {
        var ig = new IndexedGraph();
        var nodes = g.NodesRaw();
        var edges = g.EdgesRaw();
        var nc = nodes.Length;
        var ec = edges.Length;

        ig.NodeCount = nc;
        ig.EdgeCount = ec;
        ig._nodeCapacity = Math.Max(nc, 16);
        ig._edgeCapacity = Math.Max(ec, 16);

        // Allocate node arrays
        ig.NodeX = new float[ig._nodeCapacity];
        ig.NodeY = new float[ig._nodeCapacity];
        ig.NodeWidth = new float[ig._nodeCapacity];
        ig.NodeHeight = new float[ig._nodeCapacity];
        ig.NodeRank = new int[ig._nodeCapacity];
        ig.NodeOrder = new int[ig._nodeCapacity];
        ig.NodeLow = new int[ig._nodeCapacity];
        ig.NodeLim = new int[ig._nodeCapacity];
        ig.NodeMinRank = new int[ig._nodeCapacity];
        ig.NodeMaxRank = new int[ig._nodeCapacity];
        ig.NodeParentStr = new string[ig._nodeCapacity];
        ig.NodeDummy = new string[ig._nodeCapacity];
        ig.NodeLabelPos = new string[ig._nodeCapacity];
        ig.NodeBorderType = new string[ig._nodeCapacity];

        // Build string->int mapping
        ig.NodeIdByIndex = new string[ig._nodeCapacity];
        ig._nodeIndexById = new Dictionary<string, int>(nc, StringComparer.Ordinal);
        for (var i = 0; i < nc; i++)
        {
            ig.NodeIdByIndex[i] = nodes[i];
            ig._nodeIndexById[nodes[i]] = i;
        }

        // Copy node data
        for (var i = 0; i < nc; i++)
        {
            var nl = g.NodeRaw(nodes[i]);
            if (nl == null) continue;
            ig.NodeX[i] = nl.X;
            ig.NodeY[i] = nl.Y;
            ig.NodeWidth[i] = nl.Width;
            ig.NodeHeight[i] = nl.Height;
            ig.NodeRank[i] = nl.Rank;
            ig.NodeOrder[i] = nl.Order;
            ig.NodeLow[i] = nl.Low;
            ig.NodeLim[i] = nl.Lim;
            ig.NodeMinRank[i] = nl.MinRank;
            ig.NodeMaxRank[i] = nl.MaxRank;
            ig.NodeParentStr[i] = nl.Parent;
            ig.NodeDummy[i] = nl.Dummy;
            ig.NodeLabelPos[i] = nl.LabelPos;
            ig.NodeBorderType[i] = nl.BorderType;
        }

        // Allocate edge arrays
        ig.EdgeSource = new int[ig._edgeCapacity];
        ig.EdgeTarget = new int[ig._edgeCapacity];
        ig.EdgeWeight = new int[ig._edgeCapacity];
        ig.EdgeMinlen = new int[ig._edgeCapacity];
        ig.EdgeCutvalue = new int[ig._edgeCapacity];
        ig.EdgeWidth = new float[ig._edgeCapacity];
        ig.EdgeHeight = new float[ig._edgeCapacity];

        // Copy edge data
        for (var i = 0; i < ec; i++)
        {
            var e = edges[i];
            ig.EdgeSource[i] = ig._nodeIndexById[e.v];
            ig.EdgeTarget[i] = ig._nodeIndexById[e.w];
            var el = g.EdgeRaw(e);
            if (el != null)
            {
                ig.EdgeWeight[i] = el.Weight;
                ig.EdgeMinlen[i] = el.Minlen;
                ig.EdgeCutvalue[i] = el.Cutvalue;
                ig.EdgeWidth[i] = el.Width;
                ig.EdgeHeight[i] = el.Height;
            }
        }

        // Copy graph config
        var gl = g.Graph();
        ig.NodeSep = gl.NodeSep;
        ig.EdgeSep = gl.EdgeSep;
        ig.RankSep = gl.RankSep;

        // Build adjacency
        ig.RebuildAdjacency();
        return ig;
    }

    /// <summary>
    ///     Write layout results back to DagreGraph.
    /// </summary>
    public void WriteTo(DagreGraph g)
    {
        for (var i = 0; i < NodeCount; i++)
        {
            var id = NodeIdByIndex[i];
            if (id == null) continue;
            var nl = g.NodeRaw(id);
            if (nl == null) continue;
            nl.X = NodeX[i];
            nl.Y = NodeY[i];
            nl.Rank = NodeRank[i];
            nl.Order = NodeOrder[i];
        }
    }

    /// <summary>
    ///     Write only rank values back to DagreGraph.
    /// </summary>
    public void WriteRanksTo(DagreGraph g)
    {
        for (var i = 0; i < NodeCount; i++)
        {
            var id = NodeIdByIndex[i];
            if (id == null) continue;
            var nl = g.NodeRaw(id);
            if (nl == null) continue;
            nl.Rank = NodeRank[i];
        }
    }

    /// <summary>
    ///     Write only order values back to DagreGraph.
    /// </summary>
    public void WriteOrdersTo(DagreGraph g)
    {
        for (var i = 0; i < NodeCount; i++)
        {
            var id = NodeIdByIndex[i];
            if (id == null) continue;
            var nl = g.NodeRaw(id);
            if (nl == null) continue;
            nl.Order = NodeOrder[i];
        }
    }

    /// <summary>
    ///     Write X/Y coordinates back to DagreGraph.
    /// </summary>
    public void WritePositionsTo(DagreGraph g)
    {
        for (var i = 0; i < NodeCount; i++)
        {
            var id = NodeIdByIndex[i];
            if (id == null) continue;
            var nl = g.NodeRaw(id);
            if (nl == null) continue;
            nl.X = NodeX[i];
            nl.Y = NodeY[i];
        }
    }

    /// <summary>
    ///     Read X/Y coordinates from DagreGraph into indexed arrays.
    /// </summary>
    public void ReadPositionsFrom(DagreGraph g)
    {
        for (var i = 0; i < NodeCount; i++)
        {
            var id = NodeIdByIndex[i];
            if (id == null) continue;
            var nl = g.NodeRaw(id);
            if (nl == null) continue;
            NodeX[i] = nl.X;
            NodeY[i] = nl.Y;
        }
    }

    /// <summary>
    ///     Rebuild flat adjacency lists from EdgeSource/EdgeTarget arrays.
    ///     Call after batch adding nodes/edges.
    /// </summary>
    public void RebuildAdjacency()
    {
        var nc = NodeCount;
        var ec = EdgeCount;

        // Count out/in edges per node
        OutEdgeCount = new int[nc];
        InEdgeCount = new int[nc];
        for (var e = 0; e < ec; e++)
        {
            OutEdgeCount[EdgeSource[e]]++;
            InEdgeCount[EdgeTarget[e]]++;
        }

        // Compute start positions (prefix sum)
        OutEdgeStart = new int[nc];
        InEdgeStart = new int[nc];
        int outTotal = 0, inTotal = 0;
        for (var n = 0; n < nc; n++)
        {
            OutEdgeStart[n] = outTotal;
            outTotal += OutEdgeCount[n];
            InEdgeStart[n] = inTotal;
            inTotal += InEdgeCount[n];
        }

        // Fill edge lists
        OutEdgeList = new int[outTotal];
        InEdgeList = new int[inTotal];
        var outPos = new int[nc];
        var inPos = new int[nc];
        Array.Copy(OutEdgeStart, outPos, nc);
        Array.Copy(InEdgeStart, inPos, nc);

        for (var e = 0; e < ec; e++)
        {
            OutEdgeList[outPos[EdgeSource[e]]++] = e;
            InEdgeList[inPos[EdgeTarget[e]]++] = e;
        }
    }

    /// <summary>
    ///     Get outgoing edge indices for a node as a span (zero allocation).
    /// </summary>
    public ReadOnlySpan<int> OutEdges(int node)
    {
        if (OutEdgeCount[node] == 0) return ReadOnlySpan<int>.Empty;
        return new ReadOnlySpan<int>(OutEdgeList, OutEdgeStart[node], OutEdgeCount[node]);
    }

    /// <summary>
    ///     Get incoming edge indices for a node as a span (zero allocation).
    /// </summary>
    public ReadOnlySpan<int> InEdges(int node)
    {
        if (InEdgeCount[node] == 0) return ReadOnlySpan<int>.Empty;
        return new ReadOnlySpan<int>(InEdgeList, InEdgeStart[node], InEdgeCount[node]);
    }

    /// <summary>
    ///     Get all edge indices incident on a node (both in and out).
    ///     Returns a temporary array (not zero-alloc, but used in non-hot paths).
    /// </summary>
    public int[] NodeEdges(int node)
    {
        var outCount = OutEdgeCount[node];
        var inCount = InEdgeCount[node];
        var result = new int[outCount + inCount];
        if (outCount > 0)
            Array.Copy(OutEdgeList, OutEdgeStart[node], result, 0, outCount);
        if (inCount > 0)
            Array.Copy(InEdgeList, InEdgeStart[node], result, outCount, inCount);
        return result;
    }

    /// <summary>
    ///     Find nodes with no incoming edges (sources).
    /// </summary>
    public List<int> Sources()
    {
        var result = new List<int>();
        for (var n = 0; n < NodeCount; n++)
            if (InEdgeCount[n] == 0)
                result.Add(n);
        return result;
    }

    /// <summary>
    ///     Try to get the node index for a string ID. Returns -1 if not found.
    /// </summary>
    public int TryGetNodeIndex(string id)
    {
        return _nodeIndexById.TryGetValue(id, out var idx) ? idx : -1;
    }

    /// <summary>
    ///     Check if a node exists (by index).
    /// </summary>
    public bool HasNode(int node)
    {
        return node >= 0 && node < NodeCount;
    }

    /// <summary>
    ///     Get the max rank across all nodes.
    /// </summary>
    public int MaxRank()
    {
        var nc = NodeCount;
        var vecSize = Vector<int>.Count;
        var n = 0;

        if (nc >= vecSize)
        {
            var vMax = new Vector<int>(NodeRank, 0);
            n = vecSize;
            for (; n + vecSize <= nc; n += vecSize)
                vMax = Vector.Max(vMax, new Vector<int>(NodeRank, n));

            var max = 0;
            for (var i = 0; i < vecSize; i++)
                if (vMax[i] > max)
                    max = vMax[i];
            for (; n < nc; n++)
                if (NodeRank[n] > max)
                    max = NodeRank[n];
            return max;
        }
        else
        {
            var max = 0;
            for (var i = 0; i < nc; i++)
                if (NodeRank[i] > max)
                    max = NodeRank[i];
            return max;
        }
    }

    /// <summary>
    ///     Build a layer matrix: for each rank, the list of node indices sorted by order.
    /// </summary>
    public int[][] BuildLayerMatrix()
    {
        var maxRank = MaxRank();
        var layers = new List<int>[maxRank + 1];
        for (var i = 0; i <= maxRank; i++)
            layers[i] = new List<int>();

        for (var n = 0; n < NodeCount; n++)
        {
            var r = NodeRank[n];
            if (r >= 0 && r <= maxRank)
                layers[r].Add(n);
        }

        var result = new int[maxRank + 1][];
        for (var i = 0; i <= maxRank; i++)
        {
            var layer = layers[i];
            layer.Sort((a, b) => NodeOrder[a].CompareTo(NodeOrder[b]));
            result[i] = layer.ToArray();
        }

        return result;
    }

    /// <summary>
    ///     Get successor node indices (targets of outgoing edges).
    /// </summary>
    public List<int> Successors(int node)
    {
        var result = new List<int>();
        var span = OutEdges(node);
        for (var i = 0; i < span.Length; i++)
        {
            var t = EdgeTarget[span[i]];
            if (!result.Contains(t)) result.Add(t);
        }

        return result;
    }

    /// <summary>
    ///     Get predecessor node indices (sources of incoming edges).
    /// </summary>
    public List<int> Predecessors(int node)
    {
        var result = new List<int>();
        var span = InEdges(node);
        for (var i = 0; i < span.Length; i++)
        {
            var s = EdgeSource[span[i]];
            if (!result.Contains(s)) result.Add(s);
        }

        return result;
    }

    /// <summary>
    ///     Get neighbor node indices (union of successors and predecessors).
    /// </summary>
    public List<int> Neighbors(int node)
    {
        var result = new List<int>();
        var outSpan = OutEdges(node);
        for (var i = 0; i < outSpan.Length; i++)
        {
            var t = EdgeTarget[outSpan[i]];
            if (!result.Contains(t)) result.Add(t);
        }

        var inSpan = InEdges(node);
        for (var i = 0; i < inSpan.Length; i++)
        {
            var s = EdgeSource[inSpan[i]];
            if (!result.Contains(s)) result.Add(s);
        }

        return result;
    }

    // ─── Growth methods for Normalize ─────────────────────────────────

    /// <summary>
    ///     Ensure node arrays can hold at least 'needed' nodes.
    /// </summary>
    private void EnsureNodeCapacity(int needed)
    {
        if (needed <= _nodeCapacity) return;
        var newCap = Math.Max(needed, _nodeCapacity * 2);
        Array.Resize(ref NodeX, newCap);
        Array.Resize(ref NodeY, newCap);
        Array.Resize(ref NodeWidth, newCap);
        Array.Resize(ref NodeHeight, newCap);
        Array.Resize(ref NodeRank, newCap);
        Array.Resize(ref NodeOrder, newCap);
        Array.Resize(ref NodeLow, newCap);
        Array.Resize(ref NodeLim, newCap);
        Array.Resize(ref NodeMinRank, newCap);
        Array.Resize(ref NodeMaxRank, newCap);
        Array.Resize(ref NodeParentStr, newCap);
        Array.Resize(ref NodeDummy, newCap);
        Array.Resize(ref NodeLabelPos, newCap);
        Array.Resize(ref NodeBorderType, newCap);
        Array.Resize(ref NodeIdByIndex, newCap);
        if (NodeEdgeLabelRef != null) Array.Resize(ref NodeEdgeLabelRef, newCap);
        if (NodeEdgeObjRef != null) Array.Resize(ref NodeEdgeObjRef, newCap);
        _nodeCapacity = newCap;
    }

    /// <summary>
    ///     Ensure edge arrays can hold at least 'needed' edges.
    /// </summary>
    private void EnsureEdgeCapacity(int needed)
    {
        if (needed <= _edgeCapacity) return;
        var newCap = Math.Max(needed, _edgeCapacity * 2);
        Array.Resize(ref EdgeSource, newCap);
        Array.Resize(ref EdgeTarget, newCap);
        Array.Resize(ref EdgeWeight, newCap);
        Array.Resize(ref EdgeMinlen, newCap);
        Array.Resize(ref EdgeCutvalue, newCap);
        Array.Resize(ref EdgeWidth, newCap);
        Array.Resize(ref EdgeHeight, newCap);
        if (EdgeDead != null) Array.Resize(ref EdgeDead, newCap);
        _edgeCapacity = newCap;
    }

    /// <summary>
    ///     Add a dummy node. Returns the new node index.
    ///     Does NOT rebuild adjacency — call RebuildAdjacency() after all mutations.
    /// </summary>
    public int AddNode(float width, float height, int rank, string dummy, string id = null)
    {
        var idx = NodeCount;
        EnsureNodeCapacity(idx + 1);
        NodeWidth[idx] = width;
        NodeHeight[idx] = height;
        NodeRank[idx] = rank;
        NodeDummy[idx] = dummy;
        if (id != null)
        {
            NodeIdByIndex[idx] = id;
            _nodeIndexById[id] = idx;
        }

        NodeCount++;
        return idx;
    }

    /// <summary>
    ///     Add an edge. Returns the new edge index.
    ///     Does NOT rebuild adjacency — call RebuildAdjacency() after all mutations.
    /// </summary>
    public int AddEdge(int source, int target, int weight)
    {
        var idx = EdgeCount;
        EnsureEdgeCapacity(idx + 1);
        EdgeSource[idx] = source;
        EdgeTarget[idx] = target;
        EdgeWeight[idx] = weight;
        if (EdgeDead != null) EdgeDead[idx] = false;
        EdgeCount++;
        return idx;
    }

    /// <summary>
    ///     Initialize Normalize support arrays.
    /// </summary>
    public void InitNormalizeArrays()
    {
        NodeEdgeLabelRef = new EdgeLabel[_nodeCapacity];
        NodeEdgeObjRef = new DagreEdgeIndex[_nodeCapacity];
        EdgeDead = new bool[_edgeCapacity];
    }

    /// <summary>
    ///     Rebuild adjacency, skipping dead edges.
    /// </summary>
    public void RebuildAdjacencySkipDead()
    {
        var nc = NodeCount;
        var ec = EdgeCount;

        OutEdgeCount = new int[nc];
        InEdgeCount = new int[nc];
        var liveCount = 0;
        for (var e = 0; e < ec; e++)
        {
            if (EdgeDead != null && EdgeDead[e]) continue;
            OutEdgeCount[EdgeSource[e]]++;
            InEdgeCount[EdgeTarget[e]]++;
            liveCount++;
        }

        OutEdgeStart = new int[nc];
        InEdgeStart = new int[nc];
        int outTotal = 0, inTotal = 0;
        for (var n = 0; n < nc; n++)
        {
            OutEdgeStart[n] = outTotal;
            outTotal += OutEdgeCount[n];
            InEdgeStart[n] = inTotal;
            inTotal += InEdgeCount[n];
        }

        OutEdgeList = new int[outTotal];
        InEdgeList = new int[inTotal];
        var outPos = new int[nc];
        var inPos = new int[nc];
        Array.Copy(OutEdgeStart, outPos, nc);
        Array.Copy(InEdgeStart, inPos, nc);

        for (var e = 0; e < ec; e++)
        {
            if (EdgeDead != null && EdgeDead[e]) continue;
            OutEdgeList[outPos[EdgeSource[e]]++] = e;
            InEdgeList[inPos[EdgeTarget[e]]++] = e;
        }
    }

    /// <summary>
    ///     Get the first successor of a node (following out-edges, skipping dead edges).
    ///     Returns -1 if none found.
    /// </summary>
    public int FirstSuccessor(int node)
    {
        var outStart = OutEdgeStart[node];
        var outCount = OutEdgeCount[node];
        for (var i = 0; i < outCount; i++)
        {
            var e = OutEdgeList[outStart + i];
            if (EdgeDead == null || !EdgeDead[e])
                return EdgeTarget[e];
        }

        return -1;
    }

    /// <summary>
    ///     Bulk write dummy nodes and edges to DagreGraph (for ParentDummyChains/AddBorderSegments).
    ///     Only writes nodes added after 'originalNodeCount'.
    /// </summary>
    public void WriteDummyNodesToDagreGraph(DagreGraph g, int originalNodeCount, List<string> dummyChains)
    {
        g.Graph().DummyChains = dummyChains;
        g.BeginBatch();
        try
        {
            // Add dummy nodes
            for (var i = originalNodeCount; i < NodeCount; i++)
            {
                var id = NodeIdByIndex[i];
                if (id == null) continue;
                var nl = new NodeLabel
                {
                    Width = NodeWidth[i],
                    Height = NodeHeight[i],
                    Rank = NodeRank[i],
                    Dummy = NodeDummy[i],
                    LabelPos = NodeLabelPos[i],
                    EdgeLabel = NodeEdgeLabelRef[i],
                    EdgeObj = NodeEdgeObjRef[i]
                };
                g.SetNodeDummy(id, nl);
            }

            // Add edges (skip dead, skip original edges that weren't removed)
            for (var e = 0; e < EdgeCount; e++)
            {
                if (EdgeDead != null && EdgeDead[e]) continue;
                var src = EdgeSource[e];
                var tgt = EdgeTarget[e];
                // Only add edges involving dummy nodes
                if (src >= originalNodeCount || tgt >= originalNodeCount)
                {
                    var srcId = NodeIdByIndex[src];
                    var tgtId = NodeIdByIndex[tgt];
                    if (srcId != null && tgtId != null)
                    {
                        var el = new EdgeLabel { Weight = EdgeWeight[e] };
                        g.SetEdgeFast(srcId, tgtId, el, null);
                    }
                }
            }
        }
        finally
        {
            g.EndBatch();
        }
    }
}