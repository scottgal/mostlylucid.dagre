namespace Mostlylucid.Dagre;

public class BrandesKopf
{
    /*
     * Try to align nodes into vertical 'blocks' where possible. This algorithm
     * attempts to align a node with one of its median neighbors. If the edge
     * connecting a neighbor is a type-1 conflict then we ignore that possibility.
     * If a previous node has already formed a block with a node after the node
     * we're trying to form a block with, we also ignore that possibility - our
     * blocks would be split in that scenario.
     */
    public static VerticalAlignmentResult VerticalAlignment(DagreGraph g, List<string[]> layering,
        Dictionary<string, Dictionary<string, bool>> conflicts, string neighborFnName)
    {
        var root = new Dictionary<string, string>(StringComparer.Ordinal);
        var align = new Dictionary<string, string>(StringComparer.Ordinal);
        var pos = new Dictionary<string, int>(StringComparer.Ordinal);
        // We cache the position here based on the layering because the graph and
        // layering may be out of sync. The layering matrix is manipulated to
        // generate different extreme alignments.
        foreach (var layer in layering)
        {
            var order = 0;
            foreach (var v in layer)
            {
                root[v] = v;
                align[v] = v;
                pos[v] = order;
                order++;
            }
        }

        var usePredecessors = neighborFnName == "predecessors";
        // Reusable buffer to avoid allocating arrays per node
        var wsBuf = new List<string>();

        foreach (var layer in layering)
        {
            var prevIdx = -1;
            foreach (var v in layer)
            {
                wsBuf.Clear();
                // Collect neighbors directly from KeyCollection - no array allocation
                var keys = usePredecessors ? g.PredecessorKeys(v) : g.SuccessorKeys(v);
                if (keys != null)
                    foreach (var k in keys)
                        if (pos.ContainsKey(k))
                            wsBuf.Add(k);

                if (wsBuf.Count != 0)
                {
                    wsBuf.Sort((a, b) => pos[a] - pos[b]);
                    var mp = (wsBuf.Count - 1) / 2.0;
                    for (int i = (int)Math.Floor(mp), il = (int)Math.Ceiling(mp); i <= il; ++i)
                    {
                        var w = wsBuf[i];
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
        }

        return new VerticalAlignmentResult { Root = root, Align = align };
    }


    public static DagreGraph BuildBlockGraph(DagreGraph g, List<string[]> layering, Dictionary<string, string> root,
        bool reverseSep)
    {
        Func<float, float, bool, Func<DagreGraph, string, string, float>> sep = (nodeSep, edgeSep, _reverseSep) =>
        {
            var ret = (DagreGraph _g, string v, string w) =>
            {
                var vLabel = g.Node(v);
                var wLabel = g.Node(w);
                float sum = 0;
                float delta = 0;
                sum += vLabel.Width / 2f;
                if (vLabel.LabelPos != null)
                    switch (vLabel.LabelPos.ToLower())
                    {
                        case "l": delta = -vLabel.Width / 2f; break;
                        case "r": delta = vLabel.Width / 2f; break;
                    }

                if (delta != 0) sum += reverseSep ? delta : -delta;
                delta = 0;
                sum += (vLabel.Dummy != null ? edgeSep : nodeSep) / 2f;
                sum += (wLabel.Dummy != null ? edgeSep : nodeSep) / 2f;
                sum += wLabel.Width / 2f;
                if (wLabel.LabelPos != null)
                    switch (wLabel.LabelPos.ToLower())
                    {
                        case "l": delta = wLabel.Width / 2f; break;
                        case "r": delta = -wLabel.Width / 2f; break;
                    }

                if (delta != 0) sum += reverseSep ? delta : -delta;
                delta = 0;
                return sum;
            };
            return ret;
        };
        var blockGraph = new DagreGraph(false);
        var graphLabel = g.Graph();
        var sepFn = sep(graphLabel.NodeSep, graphLabel.EdgeSep, reverseSep);

        foreach (var layer in layering)
        {
            string u = null;
            foreach (var v in layer)
            {
                var vRoot = root[v];
                blockGraph.SetNode(vRoot);
                if (u != null)
                {
                    var uRoot = root[u];
                    var prevMax = blockGraph.EdgeRaw(uRoot, vRoot);
                    var prevMaxVal = prevMax?.Width ?? 0f;
                    var el = new EdgeLabel();
                    el.Width = Math.Max(sepFn(g, v, u), prevMaxVal);
                    blockGraph.SetEdge(uRoot, vRoot, el);
                }

                u = v;
            }
        }

        return blockGraph;
    }


    public static Dictionary<string, float> HorizontalCompaction(DagreGraph g, List<string[]> layering,
        Dictionary<string, string> root, Dictionary<string, string> align, bool reverseSep)
    {
        // This portion of the algorithm differs from BK due to a number of problems.
        // Instead of their algorithm we construct a new block graph and do two
        // sweeps. The first sweep places blocks with the smallest possible
        // coordinates. The second sweep removes unused space by moving blocks to the
        // greatest coordinates without violating separation.
        var xs = new Dictionary<string, float>(StringComparer.Ordinal);

        var blockG = BuildBlockGraph(g, layering, root, reverseSep);

        var borderType = reverseSep ? "borderLeft" : "borderRight";
        Action<Action<string>, string> iterate = (setXsFunc, nextNodesFunc) =>
        {
            var stack = new List<string>(blockG.Nodes());
            var elem = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var usePreds = nextNodesFunc == "predecessors";
            while (elem != null)
            {
                if (!visited.Add(elem))
                {
                    setXsFunc(elem);
                }
                else
                {
                    stack.Add(elem);
                    var neighborKeys = usePreds ? blockG.PredecessorKeys(elem) : blockG.SuccessorKeys(elem);
                    if (neighborKeys != null)
                        foreach (var item in neighborKeys)
                            stack.Add(item);
                }

                if (stack.Count == 0) break;
                elem = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
            }
        };
        //// First pass, assign smallest coordinates
        Action<string> pass1 = elem =>
        {
            var inEdges = blockG.InEdges(elem);
            float acc = 0;

            for (var i = 0; i < inEdges.Length; i++)
            {
                var e = inEdges[i];
                acc = Math.Max(acc, xs[e.v] + blockG.Edge(e).Width);
            }

            xs[elem] = acc;
        };
        //// Second pass, assign greatest coordinates
        Action<string> pass2 = elem =>
        {
            var outEdges = blockG.OutEdges(elem);

            var acc = float.PositiveInfinity;
            for (var i = 0; i < outEdges.Length; i++)
            {
                var e = outEdges[i];
                acc = Math.Min(acc, xs[e.w] - blockG.Edge(e).Width);
            }

            var min = acc;
            var node = g.Node(elem);
            var nb = node.BorderType;
            if (min != float.PositiveInfinity && nb != borderType) xs[elem] = Math.Max(xs[elem], min);
        };
        iterate(pass1, "predecessors");
        iterate(pass2, "successors");
        // Assign x coordinates to all nodes
        foreach (var v in align.Values) xs[v] = xs[root[v]];
        return xs;
    }


    // Returns the alignment that has the smallest width of the given alignments.
    public static Dictionary<string, float> FindSmallestWidthAlignment(DagreGraph g,
        Dictionary<string, Dictionary<string, float>> xss)
    {
        var minWidth = float.PositiveInfinity;
        Dictionary<string, float> minValue = null;
        foreach (var xs in xss.Values)
        {
            var max = float.NegativeInfinity;
            var min = float.PositiveInfinity;
            foreach (var kvp in xs)
            {
                var v = kvp.Key;
                var x = kvp.Value;
                var halfWidth = g.Node(v).Width / 2f;
                max = Math.Max(x + halfWidth, max);
                min = Math.Min(x - halfWidth, min);
            }

            var width = max - min;
            if (width < minWidth)
            {
                minWidth = width;
                minValue = xs;
            }
        }

        return minValue;
    }

    public static Dictionary<string, float> Balance(Dictionary<string, Dictionary<string, float>> xss, string alignDir,
        DagreGraph g = null, float alpha = 0f)
    {
        var result = new Dictionary<string, float>(StringComparer.Ordinal);
        if (alignDir != null)
        {
            var xs = xss[alignDir.ToLower()];
            foreach (var v in xss["ul"].Keys) result[v] = xs[v];
        }
        else
        {
            foreach (var v in xss["ul"].Keys)
            {
                // Sort 4 values inline without allocation (sorting network)
                var a = xss["ul"][v];
                var b = xss["ur"][v];
                var c = xss["dl"][v];
                var d = xss["dr"][v];
                // 5-comparison sorting network for 4 elements
                if (a > b) (a, b) = (b, a);
                if (c > d) (c, d) = (d, c);
                if (a > c) (a, c) = (c, a);
                if (b > d) (b, d) = (d, b);
                if (b > c) (b, c) = (c, b);
                result[v] = (b + c) / 2f;
            }
        }

        // Fan-out-aware correction: bias dummy node positions toward the ideal
        // straight line between their original edge endpoints.
        if (alpha > 0 && g != null)
            ApplyFanOutCorrection(result, g, alpha);

        return result;
    }

    /// <summary>
    /// After standard BK balance, dummy nodes from fan-out edges cluster near the source X.
    /// This biases each dummy toward the straight-line ideal position between its original
    /// edge's source and target, then enforces minimum layer separation.
    /// See: docs/papers/bk-fan-out-aware-coordinate-assignment.md
    /// </summary>
    static void ApplyFanOutCorrection(Dictionary<string, float> result, DagreGraph g, float alpha)
    {
        // Phase 1: Compute ideal positions and blend
        foreach (var v in g.Nodes())
        {
            var label = g.Node(v);
            if (label.Dummy != "edge" && label.Dummy != "edge-label") continue;

            var edgeObj = label.EdgeObj;
            if (edgeObj.v == edgeObj.w) continue; // skip self-edges

            if (!result.TryGetValue(edgeObj.v, out var srcX)) continue;
            if (!result.TryGetValue(edgeObj.w, out var tgtX)) continue;

            var srcY = g.Node(edgeObj.v).Y;
            var tgtY = g.Node(edgeObj.w).Y;
            var deltaY = tgtY - srcY;
            if (Math.Abs(deltaY) < 0.001f) continue;

            var t = (label.Y - srcY) / deltaY;
            var idealX = srcX + t * (tgtX - srcX);
            result[v] = (1 - alpha) * result[v] + alpha * idealX;
        }

        // Phase 2: Enforce minimum separation per layer
        var layering = Util.BuildLayerMatrix(g);
        var graphLabel = g.Graph();
        var edgeSep = graphLabel.EdgeSep;
        var nodeSep = graphLabel.NodeSep;

        foreach (var layer in layering)
        {
            if (layer.Length <= 1) continue;

            // Sort nodes in this layer by their X position
            Array.Sort(layer, (a, b) => result[a].CompareTo(result[b]));

            for (var i = 1; i < layer.Length; i++)
            {
                var prev = layer[i - 1];
                var curr = layer[i];
                var prevLabel = g.Node(prev);
                var currLabel = g.Node(curr);

                var sep = (prevLabel.Dummy != null || currLabel.Dummy != null) ? edgeSep : nodeSep;
                var minGap = prevLabel.Width / 2f + sep + currLabel.Width / 2f;

                if (result[curr] - result[prev] < minGap)
                    result[curr] = result[prev] + minGap;
            }
        }
    }

    /*
     * Align the coordinates of each of the layout alignments such that
     * left-biased alignments have their minimum coordinate at the same point as
     * the minimum coordinate of the smallest width alignment and right-biased
     * alignments have their maximum coordinate at the same point as the maximum
     * coordinate of the smallest width alignment.
     */
    public static void AlignCoordinates(Dictionary<string, Dictionary<string, float>> xss,
        Dictionary<string, float> alignTo)
    {
        var alignToMin = float.MaxValue;
        var alignToMax = float.MinValue;
        foreach (var val in alignTo.Values)
        {
            if (val < alignToMin) alignToMin = val;
            if (val > alignToMax) alignToMax = val;
        }

        foreach (var vert in new[] { "u", "d" })
        foreach (var horiz in new[] { "l", "r" })
        {
            var alignment = vert + horiz;
            var xs = xss[alignment];
            if (xs == alignTo) continue;

            var xsMin = float.MaxValue;
            var xsMax = float.MinValue;
            foreach (var val in xs.Values)
            {
                if (val < xsMin) xsMin = val;
                if (val > xsMax) xsMax = val;
            }

            var delta = horiz == "l" ? alignToMin - xsMin : alignToMax - xsMax;
            if (delta != 0)
            {
                // Build list of keys to avoid modifying collection during iteration
                var keyList = new List<string>(xs.Count);
                foreach (var key in xs.Keys)
                    keyList.Add(key);
                foreach (var key in keyList)
                    xs[key] += delta;
            }
        }
    }
    /*
     * This module provides coordinate assignment based on Brandes and Köpf, "Fast
     * and Simple Horizontal Coordinate Assignment."
     */

    public static Dictionary<string, float> PositionX(DagreGraph g)
    {
        var layering = Util.BuildLayerMatrix(g);

        var conflicts1 = FindType1Conflicts(g, layering);
        var conflicts2 = FindType2Conflicts(g, layering);
        // Merge conflicts
        var conflicts = new Dictionary<string, Dictionary<string, bool>>(StringComparer.Ordinal);
        foreach (var kvp in conflicts1) conflicts[kvp.Key] = kvp.Value;
        foreach (var kvp in conflicts2)
            if (conflicts.TryGetValue(kvp.Key, out var existing))
                foreach (var inner in kvp.Value)
                    existing[inner.Key] = inner.Value;
            else
                conflicts[kvp.Key] = kvp.Value;

        // Run 4 alignment passes (up-left, up-right, down-left, down-right).
        // Passes are independent but executed sequentially for browser/WASM compatibility.
        var upLayering = new List<string[]>(layering);
        var downLayering = new List<string[]>(layering);
        downLayering.Reverse();

        var xss = new Dictionary<string, Dictionary<string, float>>(StringComparer.Ordinal);
        var passes = new (string key, List<string[]> layers, string neighborFn, bool isRight)[]
        {
            ("ul", upLayering, "predecessors", false),
            ("ur", upLayering, "predecessors", true),
            ("dl", downLayering, "successors", false),
            ("dr", downLayering, "successors", true)
        };

        var results = new KeyValuePair<string, Dictionary<string, float>>[4];
        for (var i = 0; i < 4; i++)
        {
            var (key, layers, neighborFn, isRight) = passes[i];
            List<string[]> finalLayering;
            if (isRight)
            {
                finalLayering = new List<string[]>(layers.Count);
                foreach (var layer in layers)
                {
                    var reversed = (string[])layer.Clone();
                    Array.Reverse(reversed);
                    finalLayering.Add(reversed);
                }
            }
            else
            {
                finalLayering = layers;
            }

            var alignResult = VerticalAlignment(g, finalLayering, conflicts, neighborFn);
            var xs = HorizontalCompaction(g, finalLayering, alignResult.Root, alignResult.Align, isRight);
            if (isRight)
            {
                var negated = new Dictionary<string, float>(StringComparer.Ordinal);
                foreach (var kvp in xs) negated[kvp.Key] = -kvp.Value;
                xs = negated;
            }

            results[i] = new KeyValuePair<string, Dictionary<string, float>>(key, xs);
        }

        foreach (var r in results) xss[r.Key] = r.Value;

        var smallestWidth = FindSmallestWidthAlignment(g, xss);

        AlignCoordinates(xss, smallestWidth);
        var gl = g.Graph();
        return Balance(xss, gl.Align, g, gl.EdgeStraighteningStrength);
    }

    public static bool HasConflict(Dictionary<string, Dictionary<string, bool>> conflicts, string v, string w)
    {
        if (string.CompareOrdinal(v, w) > 0)
        {
            var tmp = v;
            v = w;
            w = tmp;
        }

        Dictionary<string, bool> inner;
        return conflicts.TryGetValue(v, out inner) && inner.ContainsKey(w);
    }

    public static void AddConflict(Dictionary<string, Dictionary<string, bool>> conflicts, string v, string w)
    {
        if (string.CompareOrdinal(v, w) == 1)
        {
            var tmp = v;
            v = w;
            w = tmp;
        }

        Dictionary<string, bool> conflictsV;
        if (!conflicts.TryGetValue(v, out conflictsV) || conflictsV == null)
        {
            conflictsV = new Dictionary<string, bool>(StringComparer.Ordinal);
            conflicts[v] = conflictsV;
        }

        conflictsV[w] = true;
    }

    public static string FindOtherInnerSegmentNode(DagreGraph g, string v)
    {
        if (g.NodeRaw(v).Dummy != null)
        {
            var preds = g.PredecessorKeys(v);
            if (preds != null)
                foreach (var u in preds)
                    if (g.NodeRaw(u).Dummy != null)
                        return u;
            return null;
        }

        return null;
    }
    /*
     * Marks all edges in the graph with a type-1 conflict with the "type1Conflict"
     * property. A type-1 conflict is one where a non-inner segment crosses an
     * inner segment. An inner segment is an edge with both incident nodes marked
     * with the "dummy" property.
     *
     * This algorithm scans layer by layer, starting with the second, for type-1
     * conflicts between the current layer and the previous layer. For each layer
     * it scans the nodes from left to right until it reaches one that is incident
     * on an inner segment. It then scans predecessors to determine if they have
     * edges that cross that inner segment. At the end a final scan is done for all
     * nodes on the current rank to see if they cross the last visited inner
     * segment.
     *
     * This algorithm (safely) assumes that a dummy node will only be incident on a
     * single node in the layers being scanned.
     */


    public static Dictionary<string, Dictionary<string, bool>> FindType1Conflicts(DagreGraph g, List<string[]> layering)
    {
        var conflicts = new Dictionary<string, Dictionary<string, bool>>(StringComparer.Ordinal);

        if (layering.Count > 1)
        {
            var prev = layering[0];
            for (var layerIdx = 1; layerIdx < layering.Count; layerIdx++)
            {
                var layer = layering[layerIdx];
                // last visited node in the previous layer that is incident on an inner segment.
                var k0 = 0;
                // Tracks the last node in this layer scanned for crossings with a type-1 segment.
                var scanPos = 0;
                var prevLayerLength = prev.Length;
                var lastNode = layer[layer.Length - 1];

                for (var idx = 0; idx < layer.Length; idx++)
                {
                    var v = layer[idx];
                    var w = FindOtherInnerSegmentNode(g, v);
                    var k1 = w != null ? g.Node(w).Order : prevLayerLength;

                    if (w != null || v == lastNode)
                    {
                        var end = Math.Min(idx + 1, layer.Length);
                        for (var si = scanPos; si < end; si++)
                        {
                            var scanNode = layer[si];
                            foreach (var u in g.PredecessorKeys(scanNode) ?? (IEnumerable<string>)Array.Empty<string>())
                            {
                                var uLabel = g.Node(u);
                                var uPos = uLabel.Order;
                                if ((uPos < k0 || k1 < uPos) &&
                                    !(uLabel.Dummy != null && g.Node(scanNode).Dummy != null))
                                    AddConflict(conflicts, u, scanNode);
                            }
                        }

                        scanPos = idx + 1;
                        k0 = k1;
                    }
                }

                prev = layer;
            }
        }

        return conflicts;
    }


    public static Dictionary<string, Dictionary<string, bool>> FindType2Conflicts(DagreGraph g, List<string[]> layering)
    {
        var conflicts = new Dictionary<string, Dictionary<string, bool>>(StringComparer.Ordinal);

        if (layering.Count > 1)
        {
            var prev = layering[0];
            for (var layerIdx = 1; layerIdx < layering.Count; layerIdx++)
            {
                var south = layering[layerIdx];
                var prevNorthPos = -1;
                var nextNorthPos = -1;
                var southPos = 0;
                var southLookahead = 0;

                for (var idx = 0; idx < south.Length; idx++)
                {
                    var v = south[idx];
                    southLookahead = idx;
                    var nd = g.Node(v);
                    if (nd.Dummy == "border")
                        if (g.PredecessorCount(v) != 0)
                        {
                            nextNorthPos = g.Node(g.FirstPredecessor(v)).Order;
                            // scan
                            for (var si = southPos; si < southLookahead; si++)
                            {
                                var sv = south[si];
                                if (g.Node(sv).Dummy != null)
                                    foreach (var u in g.PredecessorKeys(sv) ??
                                                      (IEnumerable<string>)Array.Empty<string>())
                                    {
                                        var uNode = g.Node(u);
                                        if (uNode.Dummy != null &&
                                            (uNode.Order < prevNorthPos || uNode.Order > nextNorthPos))
                                            AddConflict(conflicts, u, sv);
                                    }
                            }

                            southPos = southLookahead;
                            prevNorthPos = nextNorthPos;
                        }
                }

                // Final scan
                for (var si = southPos; si < south.Length; si++)
                {
                    var sv = south[si];
                    if (g.Node(sv).Dummy != null)
                        foreach (var u in g.PredecessorKeys(sv) ?? (IEnumerable<string>)Array.Empty<string>())
                        {
                            var uNode = g.Node(u);
                            if (uNode.Dummy != null && (uNode.Order < prevNorthPos || uNode.Order > prev.Length))
                                AddConflict(conflicts, u, sv);
                        }
                }

                prev = south;
            }
        }

        return conflicts;
    }

    public struct VerticalAlignmentResult
    {
        public Dictionary<string, string> Root;
        public Dictionary<string, string> Align;
    }
}
