namespace Mostlylucid.Dagre;

public class Acyclic
{
    public static void Undo(DagreGraph g)
    {
        foreach (var e in g.Edges())
        {
            var label = g.Edge(e);
            if (label.Reversed)
            {
                g.RemoveEdge(e);

                var forwardName = label.ForwardName;
                label.Reversed = false;
                label.ForwardName = null;

                g.SetEdge(e.w, e.v, label, forwardName);
            }
        }
    }

    public static Func<DagreEdgeIndex, int> WeightFn(DagreGraph g)
    {
        return e => g.Edge(e).Weight;
    }

    public static void Run(DagreGraph g)
    {
        var cyclicer = g.Graph().Acyclicer ?? "";
        var fas = cyclicer == "greedy"
            ? GreedyFAS(g, WeightFn(g))
            : DfsFAS(g);
        foreach (var e in fas)
        {
            var label = g.Edge(e);
            g.RemoveEdge(e);
            label.ForwardName = e.name;
            label.Reversed = true;

            g.SetEdge(e.w, e.v, label, Util.UniqueId("rev"));
        }
    }

    public static DagreEdgeIndex[] GreedyFAS(DagreGraph g, Func<DagreEdgeIndex, int> wf)
    {
        var nodes = g.NodesRaw();
        if (nodes.Length <= 1) return [];

        // Build simplified graph with aggregated edge weights and in/out degree tracking.
        // Track both weighted sums (for delta calculation) and structural edge counts
        // (for sink/source classification). Weight-0 edges must NOT make a node appear
        // as a phantom sink/source — only truly disconnected nodes qualify.
        var fasGraph = new DagreGraph(false);
        int maxIn = 0, maxOut = 0;

        foreach (var v in nodes)
            fasGraph.SetNode(v, new NodeLabel
            {
                ["v"] = v, ["in"] = 0f, ["out"] = 0f,
                ["inEdges"] = 0, ["outEdges"] = 0
            });

        foreach (var e in g.Edges())
        {
            var existing = fasGraph.Edge(e.v, e.w);
            var prevWeight = existing?.Weight ?? 0;
            var isNewEdge = existing == null;
            var weight = wf(e);
            var edgeWeight = prevWeight + weight;
            fasGraph.SetEdge(e.v, e.w, new EdgeLabel { Weight = edgeWeight });
            var vNode = fasGraph.Node(e.v);
            var wNode = fasGraph.Node(e.w);
            vNode["out"] = (float)vNode["out"] + weight;
            wNode["in"] = (float)wNode["in"] + weight;
            if (isNewEdge)
            {
                vNode["outEdges"] = (int)vNode["outEdges"] + 1;
                wNode["inEdges"] = (int)wNode["inEdges"] + 1;
            }
            maxOut = Math.Max(maxOut, (int)(float)vNode["out"]);
            maxIn = Math.Max(maxIn, (int)(float)wNode["in"]);
        }

        // Bucket sort: sinks at index 0, sources at last index, others by (out-in) delta
        var bucketCount = maxOut + maxIn + 3;
        var zeroIdx = maxIn + 1;
        var buckets = new List<NodeLabel>[bucketCount];
        for (var i = 0; i < bucketCount; i++) buckets[i] = [];

        foreach (var v in fasGraph.NodesRaw())
            AssignBucket(buckets, zeroIdx, fasGraph.Node(v));

        var results = new List<DagreEdgeIndex>();

        while (fasGraph.NodesRaw().Length > 0)
        {
            // Drain sinks (no outgoing edges)
            while (buckets[0].Count > 0)
                RemoveNodeFromFas(fasGraph, buckets, zeroIdx, buckets[0][^1], false, results);

            // Drain sources (no incoming edges)
            while (buckets[^1].Count > 0)
                RemoveNodeFromFas(fasGraph, buckets, zeroIdx, buckets[^1][^1], false, results);

            if (fasGraph.NodesRaw().Length > 0)
            {
                // Pick node with highest (out - in) delta; its in-edges go into FAS.
                // Among nodes with the same delta, prefer the one with lowest in-weight
                // so that the cheapest edges are reversed (e.g. weight-0 dotted edges).
                for (var i = bucketCount - 2; i > 0; i--)
                {
                    if (buckets[i].Count > 0)
                    {
                        var best = buckets[i][^1];
                        if (buckets[i].Count > 1)
                        {
                            var bestInW = (float)best["in"];
                            for (var j = buckets[i].Count - 2; j >= 0; j--)
                            {
                                var candInW = (float)buckets[i][j]["in"];
                                if (candInW < bestInW)
                                {
                                    best = buckets[i][j];
                                    bestInW = candInW;
                                }
                            }
                        }
                        RemoveNodeFromFas(fasGraph, buckets, zeroIdx, best, true, results);
                        break;
                    }
                }
            }
        }

        // Map simplified edges back to original multi-edges
        return ExpandFasEdges(g, results);
    }

    static void AssignBucket(List<NodeLabel>[] buckets, int zeroIdx, NodeLabel entry)
    {
        // Use structural edge counts for sink/source: a node with weight-0 outgoing edges
        // still has structural edges and is NOT a true sink. Only nodes with zero structural
        // edges qualify, preventing weight-0 edges from creating phantom sinks/sources.
        var outEdges = (int)entry["outEdges"];
        var inEdges = (int)entry["inEdges"];
        var outW = (float)entry["out"];
        var inW = (float)entry["in"];
        int idx;
        if (outEdges == 0)
            idx = 0; // true sink: no structural outgoing edges
        else if (inEdges == 0)
            idx = buckets.Length - 1; // true source: no structural incoming edges
        else
            idx = Math.Clamp((int)(outW - inW) + zeroIdx, 1, buckets.Length - 2);
        entry["in_list"] = buckets[idx];
        buckets[idx].Add(entry);
    }

    static void RemoveNodeFromFas(DagreGraph fasGraph, List<NodeLabel>[] buckets, int zeroIdx,
        NodeLabel entry, bool collectPredEdges, List<DagreEdgeIndex> results)
    {
        var v = (string)entry["v"];

        // Collect predecessor edges as feedback arc set entries
        if (collectPredEdges)
        {
            foreach (var e in fasGraph.InEdges(v))
                results.Add(new DagreEdgeIndex { v = e.v, w = e.w, name = e.name });
        }

        // Update neighbors' in/out weights and structural edge counts before removing
        foreach (var e in fasGraph.InEdges(v))
        {
            var uEntry = fasGraph.Node(e.v);
            if (uEntry == null) continue;
            var edgeWeight = fasGraph.Edge(e)?.Weight ?? 0;
            uEntry["out"] = (float)uEntry["out"] - edgeWeight;
            uEntry["outEdges"] = (int)uEntry["outEdges"] - 1;
            ReassignBucket(buckets, zeroIdx, uEntry);
        }

        foreach (var e in fasGraph.OutEdges(v))
        {
            var wEntry = fasGraph.Node(e.w);
            if (wEntry == null) continue;
            var edgeWeight = fasGraph.Edge(e)?.Weight ?? 0;
            wEntry["in"] = (float)wEntry["in"] - edgeWeight;
            wEntry["inEdges"] = (int)wEntry["inEdges"] - 1;
            ReassignBucket(buckets, zeroIdx, wEntry);
        }

        // Remove from current bucket and from the graph
        if (entry["in_list"] is List<NodeLabel> currentList)
            currentList.Remove(entry);

        fasGraph.RemoveNode(v);
    }

    static void ReassignBucket(List<NodeLabel>[] buckets, int zeroIdx, NodeLabel entry)
    {
        if (entry["in_list"] is List<NodeLabel> oldList)
            oldList.Remove(entry);
        AssignBucket(buckets, zeroIdx, entry);
    }

    static DagreEdgeIndex[] ExpandFasEdges(DagreGraph g, List<DagreEdgeIndex> fasEdges)
    {
        var result = new List<DagreEdgeIndex>();
        foreach (var e in fasEdges)
        {
            foreach (var oe in g.OutEdges(e.v))
            {
                if (oe.w == e.w)
                    result.Add(oe);
            }
        }
        return result.ToArray();
    }

    public static DagreEdgeIndex[] DfsFAS(DagreGraph g)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var fas = new List<DagreEdgeIndex>();
        // HashSet for O(1) cycle detection — need Contains + Remove semantics
        var stack = new HashSet<string>(StringComparer.Ordinal);

        void Dfs(string v)
        {
            if (!visited.Add(v))
                return;

            stack.Add(v);
            foreach (var e in g.OutEdges(v))
                if (stack.Contains(e.w))
                    fas.Add(e);
                else
                    Dfs(e.w);
            stack.Remove(v);
        }

        foreach (var item in g.NodesRaw()) Dfs(item);
        return fas.ToArray();
    }
}