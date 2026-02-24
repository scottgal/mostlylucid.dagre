using System.Numerics;

namespace Mostlylucid.Dagre.Indexed;

/// <summary>
///     Brandes-Kopf coordinate assignment on IndexedGraph.
///     Uses float[] arrays instead of Dictionary&lt;string,float&gt; for all alignment data.
/// </summary>
internal static class IndexedPosition
{
    /// <summary>
    ///     Assign X and Y coordinates to all nodes.
    /// </summary>
    public static void Run(IndexedGraph g)
    {
        AssignY(g);
        AssignX(g);
    }

    /// <summary>
    ///     Assign Y coordinates based on layer heights and rank separation.
    /// </summary>
    private static void AssignY(IndexedGraph g)
    {
        var layering = g.BuildLayerMatrix();
        var rankSep = g.RankSep;
        double prevY = 0;

        for (var r = 0; r < layering.Length; r++)
        {
            var layer = layering[r];
            float maxHeight = 0;
            for (var i = 0; i < layer.Length; i++)
            {
                var h = g.NodeHeight[layer[i]];
                if (h > maxHeight) maxHeight = h;
            }

            for (var i = 0; i < layer.Length; i++) g.NodeY[layer[i]] = (float)(prevY + maxHeight / 2f);
            prevY += maxHeight + rankSep;
        }
    }

    /// <summary>
    ///     Assign X coordinates using Brandes-Kopf 4-pass algorithm.
    /// </summary>
    private static void AssignX(IndexedGraph g)
    {
        var layering = g.BuildLayerMatrix();
        var nc = g.NodeCount;

        // Find conflicts
        var conflicts = FindType1Conflicts(g, layering);
        MergeConflicts(conflicts, FindType2Conflicts(g, layering));

        // Build up/down layer orderings
        var upLayering = layering;
        var downLayering = new int[layering.Length][];
        for (var i = 0; i < layering.Length; i++)
            downLayering[i] = layering[layering.Length - 1 - i];

        // 4 alignment passes (sequential for browser/WASM compatibility)
        var results = new float[4][];
        var passes = new (int[][] layers, bool usePred, bool isRight)[]
        {
            (upLayering, true, false), // ul
            (upLayering, true, true), // ur
            (downLayering, false, false), // dl
            (downLayering, false, true) // dr
        };

        for (var i = 0; i < 4; i++)
        {
            var (layers, usePred, isRight) = passes[i];
            int[][] finalLayers;
            if (isRight)
            {
                finalLayers = new int[layers.Length][];
                for (var r = 0; r < layers.Length; r++)
                {
                    finalLayers[r] = (int[])layers[r].Clone();
                    Array.Reverse(finalLayers[r]);
                }
            }
            else
            {
                finalLayers = layers;
            }

            var (root, align) = VerticalAlignment(g, finalLayers, conflicts, usePred);
            var xs = HorizontalCompaction(g, finalLayers, root, align, isRight);

            if (isRight)
                for (var n = 0; n < nc; n++)
                    xs[n] = -xs[n];
            results[i] = xs;
        }

        // Find smallest width alignment (SIMD min/max of x±hw)
        var minWidth = float.PositiveInfinity;
        var minIdx = 0;
        // Pre-compute half-widths once for reuse across 4 passes
        var halfWidths = new float[nc];
        {
            var vecSize = Vector<float>.Count;
            var vHalf = new Vector<float>(0.5f);
            var n = 0;
            for (; n + vecSize <= nc; n += vecSize)
            {
                var w = new Vector<float>(g.NodeWidth, n);
                (w * vHalf).CopyTo(halfWidths, n);
            }

            for (; n < nc; n++)
                halfWidths[n] = g.NodeWidth[n] / 2f;
        }
        for (var i = 0; i < 4; i++)
        {
            var xs = results[i];
            var (aMin, aMax) = VectorMinMaxWithOffset(xs, halfWidths, nc);
            var w = aMax - aMin;
            if (w < minWidth)
            {
                minWidth = w;
                minIdx = i;
            }
        }

        // Align all to smallest width
        AlignCoordinates(g, results, minIdx);

        // Balance: average of middle two values from 4 alignments
        for (var n = 0; n < nc; n++)
        {
            var a = results[0][n];
            var b = results[1][n];
            var c = results[2][n];
            var d = results[3][n];
            // 5-comparison sorting network
            if (a > b) (a, b) = (b, a);
            if (c > d) (c, d) = (d, c);
            if (a > c) (a, c) = (c, a);
            if (b > d) (b, d) = (d, b);
            if (b > c) (b, c) = (c, b);
            g.NodeX[n] = (b + c) / 2f;
        }
    }

    /// <summary>
    ///     Vertical alignment: group nodes into blocks along median neighbors.
    ///     Returns (root[], align[]) arrays indexed by node.
    /// </summary>
    private static (int[] root, int[] align) VerticalAlignment(IndexedGraph g, int[][] layering,
        HashSet<long> conflicts, bool usePredecessors)
    {
        var nc = g.NodeCount;
        var root = new int[nc];
        var align = new int[nc];
        var pos = new int[nc]; // position within layer

        // Initialize: each node is its own root and align
        for (var n = 0; n < nc; n++)
        {
            root[n] = n;
            align[n] = n;
        }

        // Build position map from layering
        for (var r = 0; r < layering.Length; r++)
        {
            var layer = layering[r];
            for (var i = 0; i < layer.Length; i++)
                pos[layer[i]] = i;
        }

        // Reusable buffer for neighbor collection
        var wsBuf = new List<(int node, int position)>();

        for (var r = 0; r < layering.Length; r++)
        {
            var layer = layering[r];
            var prevIdx = -1;

            for (var li = 0; li < layer.Length; li++)
            {
                var v = layer[li];
                wsBuf.Clear();

                // Collect neighbors
                if (usePredecessors)
                {
                    var inEdges = g.InEdges(v);
                    for (var e = 0; e < inEdges.Length; e++)
                    {
                        var neighbor = g.EdgeSource[inEdges[e]];
                        wsBuf.Add((neighbor, pos[neighbor]));
                    }
                }
                else
                {
                    var outEdges = g.OutEdges(v);
                    for (var e = 0; e < outEdges.Length; e++)
                    {
                        var neighbor = g.EdgeTarget[outEdges[e]];
                        wsBuf.Add((neighbor, pos[neighbor]));
                    }
                }

                if (wsBuf.Count == 0) continue;

                // Sort by position
                wsBuf.Sort((a, b) => a.position - b.position);

                // Find median neighbors
                var mp = (wsBuf.Count - 1) / 2.0;
                for (int i = (int)Math.Floor(mp), il = (int)Math.Ceiling(mp); i <= il; ++i)
                {
                    var w = wsBuf[i].node;
                    if (align[v] == v && prevIdx < pos[w] && !HasConflict(conflicts, v, w))
                    {
                        align[w] = v;
                        align[v] = root[w];
                        root[v] = root[w];
                        prevIdx = pos[w];
                    }
                }
            }
        }

        return (root, align);
    }

    /// <summary>
    ///     Horizontal compaction: assign x coordinates to aligned blocks.
    /// </summary>
    private static float[] HorizontalCompaction(IndexedGraph g, int[][] layering,
        int[] root, int[] align, bool reverseSep)
    {
        var nc = g.NodeCount;
        var xs = new float[nc];

        // Build block graph: for each pair of adjacent nodes in a layer,
        // compute the minimum separation between their root blocks.
        var blockSucc = new Dictionary<int, List<(int target, float sep)>>();
        var blockPred = new Dictionary<int, List<(int target, float sep)>>();

        for (var r = 0; r < layering.Length; r++)
        {
            var layer = layering[r];
            for (var i = 1; i < layer.Length; i++)
            {
                var u = layer[i - 1];
                var v = layer[i];
                var uRoot = root[u];
                var vRoot = root[v];

                var sep = ComputeSep(g, u, v, reverseSep);

                // Block graph edge: uRoot -> vRoot with separation
                if (!blockSucc.TryGetValue(uRoot, out var succList))
                {
                    succList = new List<(int, float)>();
                    blockSucc[uRoot] = succList;
                }

                // Update max sep for this pair
                var found = false;
                for (var j = 0; j < succList.Count; j++)
                    if (succList[j].target == vRoot)
                    {
                        if (sep > succList[j].sep)
                            succList[j] = (vRoot, sep);
                        found = true;
                        break;
                    }

                if (!found) succList.Add((vRoot, sep));

                if (!blockPred.TryGetValue(vRoot, out var predList))
                {
                    predList = new List<(int, float)>();
                    blockPred[vRoot] = predList;
                }

                found = false;
                for (var j = 0; j < predList.Count; j++)
                    if (predList[j].target == uRoot)
                    {
                        if (sep > predList[j].sep)
                            predList[j] = (uRoot, sep);
                        found = true;
                        break;
                    }

                if (!found) predList.Add((uRoot, sep));
            }
        }

        // Collect unique root nodes
        var rootNodes = new HashSet<int>();
        for (var n = 0; n < nc; n++)
            rootNodes.Add(root[n]);

        // Pass 1: assign smallest coordinates (traverse predecessors first)
        var visited = new bool[nc];
        var stack = new List<int>(rootNodes);

        void Pass1(int elem)
        {
            float acc = 0;
            if (blockPred.TryGetValue(elem, out var preds))
                for (var i = 0; i < preds.Count; i++)
                {
                    var (pred, sep) = preds[i];
                    var val = xs[pred] + sep;
                    if (val > acc) acc = val;
                }

            xs[elem] = acc;
        }

        // DFS-based iteration (predecessors first)
        IterateBlocks(stack, visited, blockPred, Pass1);

        // Pass 2: assign greatest coordinates (traverse successors)
        Array.Clear(visited, 0, nc);
        stack.Clear();
        stack.AddRange(rootNodes);

        var borderType = reverseSep ? "borderLeft" : "borderRight";

        void Pass2(int elem)
        {
            var acc = float.PositiveInfinity;
            if (blockSucc.TryGetValue(elem, out var succs))
                for (var i = 0; i < succs.Count; i++)
                {
                    var (succ, sep) = succs[i];
                    var val = xs[succ] - sep;
                    if (val < acc) acc = val;
                }

            if (acc != float.PositiveInfinity && g.NodeBorderType[elem] != borderType)
                xs[elem] = Math.Max(xs[elem], acc);
        }

        IterateBlocks(stack, visited, blockSucc, Pass2);

        // Assign x coordinates to all nodes based on their root
        for (var n = 0; n < nc; n++) xs[n] = xs[root[n]];

        return xs;
    }

    /// <summary>
    ///     DFS-based block graph iteration (used for both passes).
    /// </summary>
    private static void IterateBlocks(List<int> initialNodes, bool[] visited,
        Dictionary<int, List<(int target, float sep)>> adjacency, Action<int> processFunc)
    {
        var stack = new List<int>(initialNodes);
        while (stack.Count > 0)
        {
            var elem = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);

            if (!visited[elem])
            {
                visited[elem] = true;
                stack.Add(elem); // push back for processing after children
                if (adjacency.TryGetValue(elem, out var neighbors))
                    for (var i = 0; i < neighbors.Count; i++)
                        stack.Add(neighbors[i].target);
            }
            else
            {
                processFunc(elem);
            }
        }
    }

    /// <summary>
    ///     Compute minimum separation between two adjacent nodes in a layer.
    /// </summary>
    private static float ComputeSep(IndexedGraph g, int u, int v, bool reverseSep)
    {
        float sum = 0;
        float delta = 0;

        sum += g.NodeWidth[u] / 2f;
        var uLabelPos = g.NodeLabelPos[u];
        if (uLabelPos != null)
            switch (uLabelPos.ToLower())
            {
                case "l": delta = -g.NodeWidth[u] / 2f; break;
                case "r": delta = g.NodeWidth[u] / 2f; break;
            }

        if (delta != 0)
            sum += reverseSep ? delta : -delta;
        delta = 0;

        sum += (g.NodeDummy[u] != null ? g.EdgeSep : g.NodeSep) / 2f;
        sum += (g.NodeDummy[v] != null ? g.EdgeSep : g.NodeSep) / 2f;

        sum += g.NodeWidth[v] / 2f;
        var vLabelPos = g.NodeLabelPos[v];
        if (vLabelPos != null)
            switch (vLabelPos.ToLower())
            {
                case "l": delta = g.NodeWidth[v] / 2f; break;
                case "r": delta = -g.NodeWidth[v] / 2f; break;
            }

        if (delta != 0)
            sum += reverseSep ? delta : -delta;

        return sum;
    }

    /// <summary>
    ///     Align all 4 coordinate arrays to the smallest-width alignment.
    /// </summary>
    private static void AlignCoordinates(IndexedGraph g, float[][] xss, int alignToIdx)
    {
        var nc = g.NodeCount;
        var alignTo = xss[alignToIdx];
        var (alignToMin, alignToMax) = VectorMinMax(alignTo, nc);

        // UL=0, UR=1, DL=2, DR=3
        // "l" = 0,2; "r" = 1,3
        for (var i = 0; i < 4; i++)
        {
            if (i == alignToIdx) continue;
            var xs = xss[i];
            var (xsMin, xsMax) = VectorMinMax(xs, nc);

            var isLeft = i % 2 == 0;
            var delta = isLeft ? alignToMin - xsMin : alignToMax - xsMax;
            if (delta != 0)
                VectorAdd(xs, delta, nc);
        }
    }

    /// <summary>
    ///     SIMD min of (x-hw) and max of (x+hw) over two float arrays.
    /// </summary>
    private static (float min, float max) VectorMinMaxWithOffset(float[] xs, float[] hw, int count)
    {
        if (count == 0) return (0, 0);

        var vecSize = Vector<float>.Count;
        var n = 0;

        if (count >= vecSize)
        {
            var vx = new Vector<float>(xs, 0);
            var vh = new Vector<float>(hw, 0);
            var vMin = vx - vh;
            var vMax = vx + vh;
            n = vecSize;

            for (; n + vecSize <= count; n += vecSize)
            {
                vx = new Vector<float>(xs, n);
                vh = new Vector<float>(hw, n);
                vMin = Vector.Min(vMin, vx - vh);
                vMax = Vector.Max(vMax, vx + vh);
            }

            float rMin = float.MaxValue, rMax = float.MinValue;
            for (var i = 0; i < vecSize; i++)
            {
                if (vMin[i] < rMin) rMin = vMin[i];
                if (vMax[i] > rMax) rMax = vMax[i];
            }

            for (; n < count; n++)
            {
                var lo = xs[n] - hw[n];
                var hi = xs[n] + hw[n];
                if (lo < rMin) rMin = lo;
                if (hi > rMax) rMax = hi;
            }

            return (rMin, rMax);
        }

        float min = xs[0] - hw[0], max = xs[0] + hw[0];
        for (var i = 1; i < count; i++)
        {
            var lo = xs[i] - hw[i];
            var hi = xs[i] + hw[i];
            if (lo < min) min = lo;
            if (hi > max) max = hi;
        }

        return (min, max);
    }

    /// <summary>
    ///     SIMD min/max scan over a float array.
    /// </summary>
    private static (float min, float max) VectorMinMax(float[] arr, int count)
    {
        if (count == 0) return (0, 0);

        var vecSize = Vector<float>.Count;
        var n = 0;

        if (count >= vecSize)
        {
            var vMin = new Vector<float>(arr, 0);
            var vMax = vMin;
            n = vecSize;

            for (; n + vecSize <= count; n += vecSize)
            {
                var v = new Vector<float>(arr, n);
                vMin = Vector.Min(vMin, v);
                vMax = Vector.Max(vMax, v);
            }

            // Horizontal reduce
            float rMin = float.MaxValue, rMax = float.MinValue;
            for (var i = 0; i < vecSize; i++)
            {
                if (vMin[i] < rMin) rMin = vMin[i];
                if (vMax[i] > rMax) rMax = vMax[i];
            }

            // Scalar tail
            for (; n < count; n++)
            {
                if (arr[n] < rMin) rMin = arr[n];
                if (arr[n] > rMax) rMax = arr[n];
            }

            return (rMin, rMax);
        }

        float min = arr[0], max = arr[0];
        for (var i = 1; i < count; i++)
        {
            if (arr[i] < min) min = arr[i];
            if (arr[i] > max) max = arr[i];
        }

        return (min, max);
    }

    /// <summary>
    ///     SIMD add scalar to float array.
    /// </summary>
    private static void VectorAdd(float[] arr, float value, int count)
    {
        var vecSize = Vector<float>.Count;
        var n = 0;

        if (count >= vecSize)
        {
            var vDelta = new Vector<float>(value);
            for (; n + vecSize <= count; n += vecSize)
            {
                var v = new Vector<float>(arr, n);
                (v + vDelta).CopyTo(arr, n);
            }
        }

        for (; n < count; n++)
            arr[n] += value;
    }

    // --- Conflict detection ---

    private static HashSet<long> FindType1Conflicts(IndexedGraph g, int[][] layering)
    {
        var conflicts = new HashSet<long>();
        if (layering.Length <= 1) return conflicts;

        for (var layerIdx = 1; layerIdx < layering.Length; layerIdx++)
        {
            var layer = layering[layerIdx];
            var prev = layering[layerIdx - 1];
            var k0 = 0;
            var scanPos = 0;
            var prevLayerLength = prev.Length;

            for (var idx = 0; idx < layer.Length; idx++)
            {
                var v = layer[idx];
                var w = FindOtherInnerSegmentNode(g, v);
                var k1 = w >= 0 ? g.NodeOrder[w] : prevLayerLength;

                if (w >= 0 || idx == layer.Length - 1)
                {
                    var end = Math.Min(idx + 1, layer.Length);
                    for (var si = scanPos; si < end; si++)
                    {
                        var scanNode = layer[si];
                        var preds = g.Predecessors(scanNode);
                        for (var p = 0; p < preds.Count; p++)
                        {
                            var u = preds[p];
                            var uPos = g.NodeOrder[u];
                            if ((uPos < k0 || k1 < uPos) &&
                                !(g.NodeDummy[u] != null && g.NodeDummy[scanNode] != null))
                                AddConflict(conflicts, u, scanNode);
                        }
                    }

                    scanPos = idx + 1;
                    k0 = k1;
                }
            }
        }

        return conflicts;
    }

    private static HashSet<long> FindType2Conflicts(IndexedGraph g, int[][] layering)
    {
        var conflicts = new HashSet<long>();
        if (layering.Length <= 1) return conflicts;

        for (var layerIdx = 1; layerIdx < layering.Length; layerIdx++)
        {
            var south = layering[layerIdx];
            var prev = layering[layerIdx - 1];
            var prevNorthPos = -1;
            var nextNorthPos = -1;
            var southPos = 0;

            for (var idx = 0; idx < south.Length; idx++)
            {
                var v = south[idx];
                if (g.NodeDummy[v] == "border")
                {
                    var preds = g.Predecessors(v);
                    if (preds.Count > 0)
                    {
                        nextNorthPos = g.NodeOrder[preds[0]];
                        for (var si = southPos; si < idx; si++)
                        {
                            var sv = south[si];
                            if (g.NodeDummy[sv] != null)
                            {
                                var spreds = g.Predecessors(sv);
                                for (var p = 0; p < spreds.Count; p++)
                                {
                                    var u = spreds[p];
                                    if (g.NodeDummy[u] != null &&
                                        (g.NodeOrder[u] < prevNorthPos || g.NodeOrder[u] > nextNorthPos))
                                        AddConflict(conflicts, u, sv);
                                }
                            }
                        }

                        southPos = idx;
                        prevNorthPos = nextNorthPos;
                    }
                }

                // Trailing scan — runs on EVERY iteration (matches dagre.js)
                for (var si = southPos; si < south.Length; si++)
                {
                    var sv = south[si];
                    if (g.NodeDummy[sv] != null)
                    {
                        var spreds = g.Predecessors(sv);
                        for (var p = 0; p < spreds.Count; p++)
                        {
                            var u = spreds[p];
                            if (g.NodeDummy[u] != null &&
                                (g.NodeOrder[u] < prevNorthPos || g.NodeOrder[u] > prev.Length))
                                AddConflict(conflicts, u, sv);
                        }
                    }
                }
            }
        }

        return conflicts;
    }

    private static int FindOtherInnerSegmentNode(IndexedGraph g, int v)
    {
        if (g.NodeDummy[v] != null)
        {
            var preds = g.Predecessors(v);
            for (var i = 0; i < preds.Count; i++)
                if (g.NodeDummy[preds[i]] != null)
                    return preds[i];
        }

        return -1;
    }

    private static void AddConflict(HashSet<long> conflicts, int v, int w)
    {
        if (v > w) (v, w) = (w, v);
        conflicts.Add(((long)v << 32) | (uint)w);
    }

    private static bool HasConflict(HashSet<long> conflicts, int v, int w)
    {
        if (v > w) (v, w) = (w, v);
        return conflicts.Contains(((long)v << 32) | (uint)w);
    }

    private static void MergeConflicts(HashSet<long> target, HashSet<long> source)
    {
        foreach (var c in source)
            target.Add(c);
    }
}