namespace Mostlylucid.Dagre.Indexed;

/// <summary>
///     Network simplex ranking algorithm operating on IndexedGraph.
///     All lookups use direct int[] array access instead of Dictionary&lt;string,...&gt;.
/// </summary>
internal static class IndexedNetworkSimplex
{
    public static void Run(IndexedGraph g)
    {
        // Step 1: Simplify (merge parallel edges)
        var sg = Simplify(g);

        // Step 2: Longest path initial ranking
        LongestPath(sg);

        // Step 3: Build feasible spanning tree
        var treeEdge = new bool[sg.EdgeCount]; // which edges are in the tree
        var treeParent = new int[sg.NodeCount]; // parent in tree (-1 = root/none)
        Array.Fill(treeParent, -1);

        FeasibleTree(sg, treeEdge, treeParent);

        // Step 4: Init low/lim values
        InitLowLimValues(sg, treeParent);

        // Step 5: Init cut values
        var cutValues = new int[sg.EdgeCount];
        InitCutValues(sg, treeEdge, treeParent, cutValues);

        // Step 6: Iterate — leave/enter edge pivoting
        while (true)
        {
            var leaveIdx = LeaveEdge(sg, treeEdge, cutValues);
            if (leaveIdx < 0) break;

            var enterIdx = EnterEdge(sg, treeEdge, treeParent, leaveIdx);
            if (enterIdx < 0) break;

            // Exchange
            treeEdge[leaveIdx] = false;
            treeEdge[enterIdx] = true;

            InitLowLimValues(sg, treeParent);
            InitCutValues(sg, treeEdge, treeParent, cutValues);
            UpdateRanks(sg, treeEdge, treeParent);
        }

        // Copy ranks back to the original graph
        if (sg != g)
            for (var i = 0; i < g.NodeCount; i++)
                g.NodeRank[i] = sg.NodeRank[i];
    }

    /// <summary>
    ///     Simplify: merge parallel edges (sum weights, max minlen).
    ///     Returns the same graph if no parallel edges exist.
    /// </summary>
    private static IndexedGraph Simplify(IndexedGraph g)
    {
        // Check for parallel edges using a hash set
        var seen = new HashSet<long>();
        var hasParallel = false;
        for (var e = 0; e < g.EdgeCount; e++)
        {
            var key = ((long)g.EdgeSource[e] << 32) | (uint)g.EdgeTarget[e];
            if (!seen.Add(key))
            {
                hasParallel = true;
                break;
            }
        }

        if (!hasParallel) return g;

        // Build simplified edge list
        var edgeMap = new Dictionary<long, int>(); // key -> merged edge index
        var newSrc = new List<int>();
        var newTgt = new List<int>();
        var newWeight = new List<int>();
        var newMinlen = new List<int>();

        for (var e = 0; e < g.EdgeCount; e++)
        {
            var key = ((long)g.EdgeSource[e] << 32) | (uint)g.EdgeTarget[e];
            if (edgeMap.TryGetValue(key, out var existing))
            {
                newWeight[existing] += g.EdgeWeight[e];
                if (g.EdgeMinlen[e] > newMinlen[existing])
                    newMinlen[existing] = g.EdgeMinlen[e];
            }
            else
            {
                edgeMap[key] = newSrc.Count;
                newSrc.Add(g.EdgeSource[e]);
                newTgt.Add(g.EdgeTarget[e]);
                newWeight.Add(g.EdgeWeight[e]);
                newMinlen.Add(g.EdgeMinlen[e]);
            }
        }

        // Build a new IndexedGraph sharing node arrays
        var sg = new IndexedGraph();
        // Share node arrays by reference (simplify doesn't change nodes)
        CopyNodeArrays(g, sg, g.NodeCount);
        sg.NodeIdByIndex = g.NodeIdByIndex;

        // Build edge arrays
        var ec = newSrc.Count;
        sg.EdgeSource = newSrc.ToArray();
        sg.EdgeTarget = newTgt.ToArray();
        sg.EdgeWeight = newWeight.ToArray();
        sg.EdgeMinlen = newMinlen.ToArray();
        sg.EdgeCutvalue = new int[ec];
        sg.EdgeWidth = new float[ec];
        sg.EdgeHeight = new float[ec];

        SetCounts(sg, g.NodeCount, ec);
        sg.RebuildAdjacency();
        return sg;
    }

    // Use reflection-free helper to set private fields
    private static void SetCounts(IndexedGraph ig, int nodeCount, int edgeCount)
    {
        // We need access to private fields — use a helper method added to IndexedGraph
        ig.SetCounts(nodeCount, edgeCount);
    }

    private static void CopyNodeArrays(IndexedGraph src, IndexedGraph dst, int count)
    {
        dst.NodeX = src.NodeX;
        dst.NodeY = src.NodeY;
        dst.NodeWidth = src.NodeWidth;
        dst.NodeHeight = src.NodeHeight;
        dst.NodeRank = src.NodeRank;
        dst.NodeOrder = src.NodeOrder;
        dst.NodeLow = src.NodeLow;
        dst.NodeLim = src.NodeLim;
        dst.NodeMinRank = src.NodeMinRank;
        dst.NodeMaxRank = src.NodeMaxRank;
        dst.NodeParentStr = src.NodeParentStr;
        dst.NodeDummy = src.NodeDummy;
        dst.NodeLabelPos = src.NodeLabelPos;
        dst.NodeBorderType = src.NodeBorderType;
    }

    /// <summary>
    ///     Assign initial ranks via longest-path DFS.
    /// </summary>
    private static void LongestPath(IndexedGraph g)
    {
        var visited = new bool[g.NodeCount];

        int Dfs(int v)
        {
            if (visited[v]) return g.NodeRank[v];
            visited[v] = true;

            var rank = int.MaxValue;
            var outEdges = g.OutEdges(v);
            for (var i = 0; i < outEdges.Length; i++)
            {
                var e = outEdges[i];
                var x = Dfs(g.EdgeTarget[e]) - g.EdgeMinlen[e];
                if (x < rank) rank = x;
            }

            if (rank == int.MaxValue) rank = 0;
            g.NodeRank[v] = rank;
            return rank;
        }

        // Start from source nodes (no incoming edges)
        for (var n = 0; n < g.NodeCount; n++)
            if (g.InEdgeCount[n] == 0)
                Dfs(n);

        // Handle any unvisited nodes (cycles broken but still unreachable)
        for (var n = 0; n < g.NodeCount; n++)
            if (!visited[n])
                Dfs(n);
    }

    /// <summary>
    ///     Compute slack: rank(w) - rank(v) - minlen.
    /// </summary>
    private static int Slack(IndexedGraph g, int edgeIdx)
    {
        return g.NodeRank[g.EdgeTarget[edgeIdx]] - g.NodeRank[g.EdgeSource[edgeIdx]] - g.EdgeMinlen[edgeIdx];
    }

    /// <summary>
    ///     Build initial feasible spanning tree by greedily adding tight edges.
    /// </summary>
    private static void FeasibleTree(IndexedGraph g, bool[] treeEdge, int[] treeParent)
    {
        var nc = g.NodeCount;
        var inTree = new bool[nc];
        inTree[0] = true;
        var treeSize = 1;

        while (treeSize < nc)
        {
            // Try to add tight edges via BFS
            int added;
            do
            {
                added = 0;
                for (var e = 0; e < g.EdgeCount; e++)
                {
                    if (treeEdge[e]) continue;
                    var s = g.EdgeSource[e];
                    var t = g.EdgeTarget[e];
                    if (inTree[s] == inTree[t]) continue;
                    if (Slack(g, e) != 0) continue;

                    // Add this edge to tree
                    treeEdge[e] = true;
                    var newNode = inTree[s] ? t : s;
                    inTree[newNode] = true;
                    treeSize++;
                    added++;
                }
            } while (added > 0 && treeSize < nc);

            if (treeSize >= nc) break;

            // Find minimum slack edge crossing the tree cut
            var bestEdge = -1;
            var bestSlack = int.MaxValue;
            for (var e = 0; e < g.EdgeCount; e++)
                if (inTree[g.EdgeSource[e]] != inTree[g.EdgeTarget[e]])
                {
                    var sl = Math.Abs(Slack(g, e));
                    if (sl < bestSlack)
                    {
                        bestSlack = sl;
                        bestEdge = e;
                    }
                }

            if (bestEdge < 0) break;

            // Shift ranks of tree nodes to make this edge tight
            var delta = inTree[g.EdgeSource[bestEdge]]
                ? Slack(g, bestEdge)
                : -Slack(g, bestEdge);

            for (var n = 0; n < nc; n++)
                if (inTree[n])
                    g.NodeRank[n] += delta;
        }

        // Build treeParent from treeEdge (BFS from root)
        Array.Fill(treeParent, -1);
        var visited = new bool[nc];
        var queue = new Queue<int>();
        queue.Enqueue(0);
        visited[0] = true;

        while (queue.Count > 0)
        {
            var v = queue.Dequeue();
            for (var e = 0; e < g.EdgeCount; e++)
            {
                if (!treeEdge[e]) continue;
                var s = g.EdgeSource[e];
                var t = g.EdgeTarget[e];
                var neighbor = -1;
                if (s == v && !visited[t]) neighbor = t;
                else if (t == v && !visited[s]) neighbor = s;
                if (neighbor >= 0)
                {
                    visited[neighbor] = true;
                    treeParent[neighbor] = v;
                    queue.Enqueue(neighbor);
                }
            }
        }
    }

    /// <summary>
    ///     Assign low/lim values via DFS postorder on the spanning tree.
    /// </summary>
    private static void InitLowLimValues(IndexedGraph g, int[] treeParent)
    {
        var nc = g.NodeCount;
        // Find root (node with treeParent == -1)
        var root = 0;
        for (var n = 0; n < nc; n++)
            if (treeParent[n] == -1)
            {
                root = n;
                break;
            }

        // Build children list from treeParent
        var children = new List<int>[nc];
        for (var n = 0; n < nc; n++)
            children[n] = new List<int>();
        for (var n = 0; n < nc; n++)
            if (treeParent[n] >= 0)
                children[treeParent[n]].Add(n);

        // Iterative DFS postorder
        var stack = new Stack<(int node, bool processed)>();
        stack.Push((root, false));
        var nextLim = 1;

        while (stack.Count > 0)
        {
            var (node, processed) = stack.Pop();
            if (processed)
            {
                g.NodeLim[node] = nextLim++;
                continue;
            }

            g.NodeLow[node] = nextLim;
            stack.Push((node, true));

            var ch = children[node];
            for (var i = ch.Count - 1; i >= 0; i--)
                stack.Push((ch[i], false));
        }
    }

    /// <summary>
    ///     Compute cut values for all tree edges via postorder traversal.
    /// </summary>
    private static void InitCutValues(IndexedGraph g, bool[] treeEdge, int[] treeParent, int[] cutValues)
    {
        var nc = g.NodeCount;

        // Find root
        var root = 0;
        for (var n = 0; n < nc; n++)
            if (treeParent[n] == -1)
            {
                root = n;
                break;
            }

        // Build children list
        var children = new List<int>[nc];
        for (var n = 0; n < nc; n++)
            children[n] = new List<int>();
        for (var n = 0; n < nc; n++)
            if (treeParent[n] >= 0)
                children[treeParent[n]].Add(n);

        // Postorder traversal — compute cut values bottom-up
        var postorder = new int[nc];
        var postIdx = 0;
        var dfsStack = new Stack<(int node, bool processed)>();
        dfsStack.Push((root, false));
        while (dfsStack.Count > 0)
        {
            var (node, processed) = dfsStack.Pop();
            if (processed)
            {
                postorder[postIdx++] = node;
                continue;
            }

            dfsStack.Push((node, true));
            var ch = children[node];
            for (var i = ch.Count - 1; i >= 0; i--)
                dfsStack.Push((ch[i], false));
        }

        // Process all non-root nodes in postorder
        for (var pi = 0; pi < postIdx - 1; pi++) // skip root (last in postorder)
        {
            var child = postorder[pi];
            var parent = treeParent[child];
            if (parent < 0) continue;

            // Find tree edge between child and parent
            var treeEdgeIdx = FindTreeEdge(g, treeEdge, child, parent);
            if (treeEdgeIdx < 0) continue;

            cutValues[treeEdgeIdx] = CalcCutValue(g, treeEdge, treeParent, cutValues, child, parent, treeEdgeIdx);
        }
    }

    /// <summary>
    ///     Find the tree edge connecting child and parent.
    /// </summary>
    private static int FindTreeEdge(IndexedGraph g, bool[] treeEdge, int child, int parent)
    {
        for (var e = 0; e < g.EdgeCount; e++)
        {
            if (!treeEdge[e]) continue;
            if ((g.EdgeSource[e] == child && g.EdgeTarget[e] == parent) ||
                (g.EdgeSource[e] == parent && g.EdgeTarget[e] == child))
                return e;
        }

        return -1;
    }

    /// <summary>
    ///     Calculate cut value for tree edge (child -> parent).
    /// </summary>
    private static int CalcCutValue(IndexedGraph g, bool[] treeEdge, int[] treeParent,
        int[] cutValues, int child, int parent, int treeEdgeIdx)
    {
        // Determine direction of the graph edge
        var childIsTail = g.EdgeSource[treeEdgeIdx] == child && g.EdgeTarget[treeEdgeIdx] == parent;
        var graphWeight = g.EdgeWeight[treeEdgeIdx];
        var cutValue = graphWeight;

        // Examine all edges incident on child
        var nodeEdges = g.NodeEdges(child);
        for (var i = 0; i < nodeEdges.Length; i++)
        {
            var e = nodeEdges[i];
            var isOutEdge = g.EdgeSource[e] == child;
            var other = isOutEdge ? g.EdgeTarget[e] : g.EdgeSource[e];
            if (other == parent) continue;

            var pointsToHead = isOutEdge == childIsTail;
            var otherWeight = g.EdgeWeight[e];
            cutValue += pointsToHead ? otherWeight : -otherWeight;

            // If this is also a tree edge, incorporate its cut value
            if (treeEdge[e])
            {
                var otherCutValue = cutValues[e];
                cutValue += pointsToHead ? -otherCutValue : otherCutValue;
            }
        }

        return cutValue;
    }

    /// <summary>
    ///     Find a tree edge with negative cut value (leave edge).
    /// </summary>
    private static int LeaveEdge(IndexedGraph g, bool[] treeEdge, int[] cutValues)
    {
        for (var e = 0; e < g.EdgeCount; e++)
            if (treeEdge[e] && cutValues[e] < 0)
                return e;
        return -1;
    }

    /// <summary>
    ///     Find the non-tree edge with minimum slack that crosses the tree cut (enter edge).
    /// </summary>
    private static int EnterEdge(IndexedGraph g, bool[] treeEdge, int[] treeParent, int leaveEdgeIdx)
    {
        var v = g.EdgeSource[leaveEdgeIdx];
        var w = g.EdgeTarget[leaveEdgeIdx];

        // Determine which side of the cut to search from
        var vLim = g.NodeLim[v];
        var wLim = g.NodeLim[w];
        var flip = vLim > wLim;
        var tailLow = flip ? g.NodeLow[w] : g.NodeLow[v];
        var tailLim = flip ? g.NodeLim[w] : g.NodeLim[v];

        var bestEdge = -1;
        var bestSlack = int.MaxValue;

        for (var e = 0; e < g.EdgeCount; e++)
        {
            if (treeEdge[e]) continue;

            var eLim = g.NodeLim[g.EdgeSource[e]];
            var eDescV = tailLow <= eLim && eLim <= tailLim;
            var eLimW = g.NodeLim[g.EdgeTarget[e]];
            var eDescW = tailLow <= eLimW && eLimW <= tailLim;

            if (flip == eDescV && flip != eDescW)
            {
                var sl = Slack(g, e);
                if (sl < bestSlack)
                {
                    bestSlack = sl;
                    bestEdge = e;
                }
            }
        }

        return bestEdge;
    }

    /// <summary>
    ///     Update graph ranks from the spanning tree structure.
    /// </summary>
    private static void UpdateRanks(IndexedGraph g, bool[] treeEdge, int[] treeParent)
    {
        var nc = g.NodeCount;
        // Find root
        var root = 0;
        for (var n = 0; n < nc; n++)
            if (treeParent[n] == -1)
            {
                root = n;
                break;
            }

        // BFS preorder from root
        var visited = new bool[nc];
        var queue = new Queue<int>();
        queue.Enqueue(root);
        visited[root] = true;

        while (queue.Count > 0)
        {
            var v = queue.Dequeue();
            // Visit all tree neighbors
            for (var e = 0; e < g.EdgeCount; e++)
            {
                if (!treeEdge[e]) continue;
                var s = g.EdgeSource[e];
                var t = g.EdgeTarget[e];
                var neighbor = -1;
                if (s == v && !visited[t]) neighbor = t;
                else if (t == v && !visited[s]) neighbor = s;
                if (neighbor < 0) continue;

                visited[neighbor] = true;
                // Determine edge direction in the original graph
                var flipped = g.EdgeSource[e] != neighbor;
                if (flipped)
                    g.NodeRank[neighbor] = g.NodeRank[v] + g.EdgeMinlen[e];
                else
                    g.NodeRank[neighbor] = g.NodeRank[v] - g.EdgeMinlen[e];

                queue.Enqueue(neighbor);
            }
        }
    }
}