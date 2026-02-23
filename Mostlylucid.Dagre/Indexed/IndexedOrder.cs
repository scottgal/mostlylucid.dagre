namespace Mostlylucid.Dagre.Indexed;

/// <summary>
///     Edge crossing minimization on IndexedGraph using barycenter heuristic.
///     Replaces the string-dictionary-based Order module.
/// </summary>
internal static class IndexedOrder
{
    public static void Run(IndexedGraph g)
    {
        var maxRank = g.MaxRank();
        if (maxRank < 0) return;

        // Initialize ordering via DFS from sources
        InitOrder(g);

        // Build layer matrix
        var layering = g.BuildLayerMatrix();

        // Assign initial order from layering
        AssignOrder(g, layering);

        // Pre-allocate reusable buffers for cross counting
        var southPos = new int[g.NodeCount];
        var entryBuf = new List<(int pos, int weight)>(g.EdgeCount);
        var perNodeBuf = new List<(int pos, int weight)>(32);

        var bestCC = float.PositiveInfinity;
        int[][] best = null;

        for (int i = 0, lastBest = 0; lastBest < 4; ++i, ++lastBest)
        {
            // Alternate up/down sweeps with left/right bias
            var useDown = i % 2 != 0;
            var biasRight = i % 4 >= 2;

            if (useDown)
                SweepDown(g, layering, biasRight);
            else
                SweepUp(g, layering, biasRight);

            // Re-sort layers by current order (avoids full BuildLayerMatrix rebuild)
            for (var r = 0; r < layering.Length; r++)
                Array.Sort(layering[r], (a, b) => g.NodeOrder[a].CompareTo(g.NodeOrder[b]));

            var cc = CrossCount(g, layering, southPos, entryBuf, perNodeBuf);
            if (cc < bestCC)
            {
                lastBest = 0;
                bestCC = cc;
                // Deep copy best layering
                best = new int[layering.Length][];
                for (var r = 0; r < layering.Length; r++)
                    best[r] = (int[])layering[r].Clone();
            }
        }

        if (best != null)
            AssignOrder(g, best);
    }

    /// <summary>
    ///     DFS-based initial ordering: assign order within each rank.
    /// </summary>
    private static void InitOrder(IndexedGraph g)
    {
        var nc = g.NodeCount;
        var visited = new bool[nc];
        var maxRank = g.MaxRank();
        var layers = new List<int>[maxRank + 1];
        for (var i = 0; i <= maxRank; i++)
            layers[i] = new List<int>();

        // Group source nodes by rank, then DFS
        var sourcesByRank = new SortedDictionary<int, List<int>>();
        for (var n = 0; n < nc; n++)
            if (g.InEdgeCount[n] == 0)
            {
                var r = g.NodeRank[n];
                if (!sourcesByRank.TryGetValue(r, out var list))
                {
                    list = new List<int>();
                    sourcesByRank[r] = list;
                }

                list.Add(n);
            }

        // Iterative DFS to avoid stack overflow on large graphs
        var stack = new Stack<int>(nc);

        void DfsIterative(int start)
        {
            stack.Push(start);
            while (stack.Count > 0)
            {
                var v = stack.Pop();
                if (visited[v]) continue;
                visited[v] = true;
                var r = g.NodeRank[v];
                if (r >= 0 && r <= maxRank)
                    layers[r].Add(v);

                // Push in reverse to maintain order
                var outEdges = g.OutEdges(v);
                for (var i = outEdges.Length - 1; i >= 0; i--)
                    stack.Push(g.EdgeTarget[outEdges[i]]);
            }
        }

        foreach (var kvp in sourcesByRank)
        foreach (var n in kvp.Value)
            DfsIterative(n);

        // Handle any unvisited nodes
        for (var n = 0; n < nc; n++)
            if (!visited[n])
                DfsIterative(n);

        // Assign order within each layer
        for (var r = 0; r <= maxRank; r++)
        for (var i = 0; i < layers[r].Count; i++)
            g.NodeOrder[layers[r][i]] = i;
    }

    private static void AssignOrder(IndexedGraph g, int[][] layering)
    {
        for (var r = 0; r < layering.Length; r++)
        {
            var layer = layering[r];
            for (var i = 0; i < layer.Length; i++)
                g.NodeOrder[layer[i]] = i;
        }
    }

    /// <summary>
    ///     Sweep from top to bottom, reordering each layer based on barycenter of incoming edges.
    /// </summary>
    private static void SweepDown(IndexedGraph g, int[][] layering, bool biasRight)
    {
        for (var r = 1; r < layering.Length; r++)
        {
            ReorderLayerByBarycenter(g, layering[r], true, biasRight);
            // Sort layer to reflect new order (needed for next layer's barycenter calculation)
            Array.Sort(layering[r], (a, b) => g.NodeOrder[a] - g.NodeOrder[b]);
        }
    }

    /// <summary>
    ///     Sweep from bottom to top, reordering each layer based on barycenter of outgoing edges.
    /// </summary>
    private static void SweepUp(IndexedGraph g, int[][] layering, bool biasRight)
    {
        for (var r = layering.Length - 2; r >= 0; r--)
        {
            ReorderLayerByBarycenter(g, layering[r], false, biasRight);
            Array.Sort(layering[r], (a, b) => g.NodeOrder[a] - g.NodeOrder[b]);
        }
    }

    /// <summary>
    ///     Reorder nodes in a layer based on barycenter of adjacent layer connections.
    /// </summary>
    private static void ReorderLayerByBarycenter(IndexedGraph g, int[] layer, bool useInEdges, bool biasRight)
    {
        var len = layer.Length;
        // Compute barycenter for each node in layer
        var barycenters = new float[len];

        for (var i = 0; i < len; i++)
        {
            var node = layer[i];
            float sum = 0;
            var weightSum = 0;

            if (useInEdges)
            {
                var edges = g.InEdges(node);
                for (var e = 0; e < edges.Length; e++)
                {
                    var edgeIdx = edges[e];
                    var w = g.EdgeWeight[edgeIdx];
                    sum += g.NodeOrder[g.EdgeSource[edgeIdx]] * w;
                    weightSum += w;
                }
            }
            else
            {
                var edges = g.OutEdges(node);
                for (var e = 0; e < edges.Length; e++)
                {
                    var edgeIdx = edges[e];
                    var w = g.EdgeWeight[edgeIdx];
                    sum += g.NodeOrder[g.EdgeTarget[edgeIdx]] * w;
                    weightSum += w;
                }
            }

            barycenters[i] = weightSum > 0 ? sum / weightSum : g.NodeOrder[node];
        }

        // Sort layer by barycenter
        var indices = new int[len];
        for (var i = 0; i < len; i++) indices[i] = i;

        Array.Sort(indices, (a, b) =>
        {
            var cmp = barycenters[a].CompareTo(barycenters[b]);
            if (cmp != 0) return cmp;
            return biasRight ? b - a : a - b;
        });

        // Assign new order
        for (var i = 0; i < len; i++) g.NodeOrder[layer[indices[i]]] = i;
    }

    /// <summary>
    ///     Count weighted edge crossings across all adjacent layer pairs.
    ///     Uses pre-allocated buffers to avoid per-call allocations.
    /// </summary>
    private static int CrossCount(IndexedGraph g, int[][] layering, int[] southPos,
        List<(int pos, int weight)> entryBuf, List<(int pos, int weight)> perNodeBuf)
    {
        var cc = 0;
        for (var i = 1; i < layering.Length; i++)
            cc += TwoLayerCrossCount(g, layering[i - 1], layering[i], southPos, entryBuf, perNodeBuf);
        return cc;
    }

    /// <summary>
    ///     Count crossings between two adjacent layers using a Fenwick tree.
    ///     Uses pre-allocated buffers to minimize allocations.
    /// </summary>
    private static int TwoLayerCrossCount(IndexedGraph g, int[] northLayer, int[] southLayer,
        int[] southPos, List<(int pos, int weight)> entries, List<(int pos, int weight)> perNode)
    {
        var southCount = southLayer.Length;
        // Build position lookup for south layer nodes (reuse pre-allocated array)
        for (var i = 0; i < southCount; i++)
            southPos[southLayer[i]] = i;

        // Collect south entries sorted by north position then south position
        entries.Clear();
        foreach (var v in northLayer)
        {
            var outEdges = g.OutEdges(v);
            perNode.Clear();
            for (var e = 0; e < outEdges.Length; e++)
            {
                var edgeIdx = outEdges[e];
                var target = g.EdgeTarget[edgeIdx];
                if (g.NodeRank[target] != g.NodeRank[v]) perNode.Add((southPos[target], g.EdgeWeight[edgeIdx]));
            }

            perNode.Sort((a, b) => a.pos - b.pos);
            entries.AddRange(perNode);
        }

        // Fenwick tree accumulator
        var firstIndex = 1;
        while (firstIndex < southCount) firstIndex <<= 1;
        var treeSize = 2 * firstIndex - 1;
        // Stackalloc for small trees, otherwise heap
        var tree = treeSize <= 2048
            ? stackalloc int[treeSize]
            : new int[treeSize];
        tree.Clear();
        firstIndex -= 1;

        var cc = 0;
        foreach (var (pos, weight) in entries)
        {
            var index = pos + firstIndex;
            tree[index] += weight;
            var weightSum = 0;
            while (index > 0)
            {
                if ((index & 1) != 0)
                    weightSum += tree[index + 1];
                index = (index - 1) >> 1;
                tree[index] += weight;
            }

            cc += weight * weightSum;
        }

        return cc;
    }
}