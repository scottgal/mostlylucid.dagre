using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace Dagre
{
    public class BrandesKopf
    {
        public struct VerticalAlignmentResult
        {
            public Dictionary<string, string> Root;
            public Dictionary<string, string> Align;
        }

        /*
               * Try to align nodes into vertical 'blocks' where possible. This algorithm
               * attempts to align a node with one of its median neighbors. If the edge
               * connecting a neighbor is a type-1 conflict then we ignore that possibility.
               * If a previous node has already formed a block with a node after the node
               * we're trying to form a block with, we also ignore that possibility - our
               * blocks would be split in that scenario.
               */
        public static VerticalAlignmentResult verticalAlignment(DagreGraph g, List<string[]> layering, Dictionary<string, Dictionary<string, bool>> conflicts, string neighborFnName)
        {
            var root = new Dictionary<string, string>(StringComparer.Ordinal);
            var align = new Dictionary<string, string>(StringComparer.Ordinal);
            var pos = new Dictionary<string, int>(StringComparer.Ordinal);
            // We cache the position here based on the layering because the graph and
            // layering may be out of sync. The layering matrix is manipulated to
            // generate different extreme alignments.
            foreach (var layer in layering)
            {
                int order = 0;
                foreach (var v in layer)
                {
                    root[v] = v;
                    align[v] = v;
                    pos[v] = order;
                    order++;
                }
            }

            bool usePredecessors = neighborFnName == "predecessors";
            // Reusable buffer to avoid allocating arrays per node
            var wsBuf = new List<string>();

            foreach (var layer in layering)
            {
                var prevIdx = -1;
                foreach (var v in layer)
                {
                    wsBuf.Clear();
                    // Collect neighbors directly from KeyCollection — no array allocation
                    var keys = usePredecessors ? g.PredecessorKeys(v) : g.SuccessorKeys(v);
                    if (keys != null)
                    {
                        foreach (var k in keys)
                        {
                            if (pos.ContainsKey(k))
                                wsBuf.Add(k);
                        }
                    }
                    if (wsBuf.Count != 0)
                    {
                        wsBuf.Sort((a, b) => pos[a] - pos[b]);
                        double mp = (wsBuf.Count - 1) / 2.0;
                        for (int i = (int)Math.Floor(mp), il = (int)Math.Ceiling(mp); i <= il; ++i)
                        {
                            var w = wsBuf[i];
                            if (align[v] == v && prevIdx < pos[w] && !hasConflict(conflicts, v, w))
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


        public static DagreGraph buildBlockGraph(DagreGraph g, List<string[]> layering, Dictionary<string, string> root, bool reverseSep)
        {
            Func<float, float, bool, Func<DagreGraph, string, string, float>> sep = (nodeSep, edgeSep, _reverseSep) =>
            {
                Func<DagreGraph, string, string, float> ret = (DagreGraph _g, string v, string w) =>
                {
                    var vLabel = g.Node(v);
                    var wLabel = g.Node(w);
                    float sum = 0;
                    float delta = 0;
                    sum += vLabel.Width / 2f;
                    if (vLabel.LabelPos != null)
                    {
                        switch (vLabel.LabelPos.ToLower())
                        {
                            case "l": delta = -vLabel.Width / 2f; break;
                            case "r": delta = vLabel.Width / 2f; break;
                        }
                    }
                    if (delta != 0)
                    {
                        sum += reverseSep ? delta : -delta;
                    }
                    delta = 0;
                    sum += (vLabel.Dummy != null ? edgeSep : nodeSep) / 2f;
                    sum += (wLabel.Dummy != null ? edgeSep : nodeSep) / 2f;
                    sum += wLabel.Width / 2f;
                    if (wLabel.LabelPos != null)
                    {
                        switch (wLabel.LabelPos.ToLower())
                        {
                            case "l": delta = wLabel.Width / 2f; break;
                            case "r": delta = -wLabel.Width / 2f; break;
                        }
                    }
                    if (delta != 0)
                    {
                        sum += reverseSep ? delta : -delta;
                    }
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


        public static Dictionary<string, float> horizontalCompaction(DagreGraph g, List<string[]> layering, Dictionary<string, string> root, Dictionary<string, string> align, bool reverseSep)
        {
            // This portion of the algorithm differs from BK due to a number of problems.
            // Instead of their algorithm we construct a new block graph and do two
            // sweeps. The first sweep places blocks with the smallest possible
            // coordinates. The second sweep removes unused space by moving blocks to the
            // greatest coordinates without violating separation.
            var xs = new Dictionary<string, float>(StringComparer.Ordinal);

            DagreGraph blockG = buildBlockGraph(g, layering, root, reverseSep);

            var borderType = reverseSep ? "borderLeft" : "borderRight";
            Action<Action<string>, string> iterate = (setXsFunc, nextNodesFunc) =>
            {
                var stack = new List<string>(blockG.Nodes());
                string elem = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                var visited = new HashSet<string>(StringComparer.Ordinal);
                bool usePreds = nextNodesFunc == "predecessors";
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
                        {
                            foreach (var item in neighborKeys)
                            {
                                stack.Add(item);
                            }
                        }
                    }
                    if (stack.Count == 0) break;
                    elem = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                }
            };
            //// First pass, assign smallest coordinates
            Action<string> pass1 = (elem) =>
            {
                var inEdges = blockG.InEdges(elem);
                float acc = 0;

                for (int i = 0; i < inEdges.Length; i++)
                {
                    var e = inEdges[i];
                    acc = Math.Max(acc, xs[e.v] + (blockG.Edge(e)).Width);
                }
                xs[elem] = acc;
            };
            //// Second pass, assign greatest coordinates
            Action<string> pass2 = (elem) =>
            {
                var outEdges = blockG.OutEdges(elem);

                float acc = float.PositiveInfinity;
                for (int i = 0; i < outEdges.Length; i++)
                {
                    var e = outEdges[i];
                    acc = Math.Min(acc, xs[e.w] - (blockG.Edge(e)).Width);
                }
                float min = acc;
                var node = g.Node(elem);
                string nb = node.BorderType;
                if (min != float.PositiveInfinity && nb != borderType)
                {
                    xs[elem] = Math.Max(xs[elem], min);
                }
            };
            iterate(pass1, "predecessors");
            iterate(pass2, "successors");
            // Assign x coordinates to all nodes
            foreach (var v in align.Values)
            {
                xs[v] = xs[root[v]];
            }
            return xs;
        }


        // Returns the alignment that has the smallest width of the given alignments.
        public static Dictionary<string, float> findSmallestWidthAlignment(DagreGraph g, Dictionary<string, Dictionary<string, float>> xss)
        {
            float minWidth = float.PositiveInfinity;
            Dictionary<string, float> minValue = null;
            foreach (var xs in xss.Values)
            {
                var max = float.NegativeInfinity;
                var min = float.PositiveInfinity;
                foreach (var kvp in xs)
                {
                    var v = kvp.Key;
                    var x = kvp.Value;
                    float halfWidth = (g.Node(v)).Width / 2f;
                    max = Math.Max(x + halfWidth, max);
                    min = Math.Min(x - halfWidth, min);
                }
                float width = max - min;
                if (width < minWidth)
                {
                    minWidth = width;
                    minValue = xs;
                }
            }
            return minValue;
        }

        public static Dictionary<string, float> balance(Dictionary<string, Dictionary<string, float>> xss, string alignDir)
        {
            var result = new Dictionary<string, float>(StringComparer.Ordinal);
            if (alignDir != null)
            {
                var xs = xss[alignDir.ToLower()];
                foreach (var v in xss["ul"].Keys)
                {
                    result[v] = xs[v];
                }
            }
            else
            {
                foreach (var v in xss["ul"].Keys)
                {
                    var vals = new float[] { xss["ul"][v], xss["ur"][v], xss["dl"][v], xss["dr"][v] };
                    Array.Sort(vals);
                    result[v] = (vals[1] + vals[2]) / 2f;
                }
            }
            return result;
        }

        /*
               * Align the coordinates of each of the layout alignments such that
               * left-biased alignments have their minimum coordinate at the same point as
               * the minimum coordinate of the smallest width alignment and right-biased
               * alignments have their maximum coordinate at the same point as the maximum
               * coordinate of the smallest width alignment.
               */
        public static void alignCoordinates(Dictionary<string, Dictionary<string, float>> xss, Dictionary<string, float> alignTo)
        {
            float alignToMin = float.MaxValue;
            float alignToMax = float.MinValue;
            foreach (var val in alignTo.Values)
            {
                if (val < alignToMin) alignToMin = val;
                if (val > alignToMax) alignToMax = val;
            }

            foreach (var vert in new[] { "u", "d" })
            {
                foreach (var horiz in new[] { "l", "r" })
                {
                    var alignment = vert + horiz;
                    var xs = xss[alignment];
                    if (xs == alignTo) continue;

                    float xsMin = float.MaxValue;
                    float xsMax = float.MinValue;
                    foreach (var val in xs.Values)
                    {
                        if (val < xsMin) xsMin = val;
                        if (val > xsMax) xsMax = val;
                    }

                    float delta = horiz == "l" ? alignToMin - xsMin : alignToMax - xsMax;
                    if (delta != 0)
                    {
                        // Update in place to avoid allocating a new dictionary
                        var keys = new string[xs.Count];
                        xs.Keys.CopyTo(keys, 0);
                        foreach (var key in keys)
                        {
                            xs[key] += delta;
                        }
                    }
                }
            }
        }
        /*
        * This module provides coordinate assignment based on Brandes and Köpf, "Fast
        * and Simple Horizontal Coordinate Assignment."
        */

        public static Dictionary<string, float> positionX(DagreGraph g)
        {
            var layering = Util.buildLayerMatrix(g);

            var conflicts1 = findType1Conflicts(g, layering);
            var conflicts2 = findType2Conflicts(g, layering);
            // Merge conflicts
            var conflicts = new Dictionary<string, Dictionary<string, bool>>(StringComparer.Ordinal);
            foreach (var kvp in conflicts1)
            {
                conflicts[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in conflicts2)
            {
                if (conflicts.TryGetValue(kvp.Key, out var existing))
                {
                    foreach (var inner in kvp.Value)
                    {
                        existing[inner.Key] = inner.Value;
                    }
                }
                else
                {
                    conflicts[kvp.Key] = kvp.Value;
                }
            }

            // Run 4 alignment passes in parallel (up-left, up-right, down-left, down-right)
            // Each pass is independent: reads graph but doesn't mutate it
            var upLayering = new List<string[]>(layering);
            var downLayering = new List<string[]>(layering);
            downLayering.Reverse();

            var xss = new Dictionary<string, Dictionary<string, float>>(StringComparer.Ordinal);
            var passes = new (string key, List<string[]> layers, string neighborFn, bool isRight)[]
            {
                ("ul", upLayering, "predecessors", false),
                ("ur", upLayering, "predecessors", true),
                ("dl", downLayering, "successors", false),
                ("dr", downLayering, "successors", true),
            };

            var results = new KeyValuePair<string, Dictionary<string, float>>[4];
            Parallel.ForEachAsync(Enumerable.Range(0, 4), async (i, _) =>
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
                var alignResult = verticalAlignment(g, finalLayering, conflicts, neighborFn);
                var xs = horizontalCompaction(g, finalLayering, alignResult.Root, alignResult.Align, isRight);
                if (isRight)
                {
                    var negated = new Dictionary<string, float>(StringComparer.Ordinal);
                    foreach (var kvp in xs)
                    {
                        negated[kvp.Key] = -kvp.Value;
                    }
                    xs = negated;
                }
                results[i] = new KeyValuePair<string, Dictionary<string, float>>(key, xs);
                await Task.CompletedTask;
            }).GetAwaiter().GetResult();

            foreach (var r in results)
            {
                xss[r.Key] = r.Value;
            }

            var smallestWidth = findSmallestWidthAlignment(g, xss);

            alignCoordinates(xss, smallestWidth);
            return balance(xss, g.Graph().Align);
        }
        public static bool hasConflict(Dictionary<string, Dictionary<string, bool>> conflicts, string v, string w)
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
        public static void addConflict(Dictionary<string, Dictionary<string, bool>> conflicts, string v, string w)
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

        public static string findOtherInnerSegmentNode(DagreGraph g, string v)
        {
            if ((g.NodeRaw(v)).Dummy != null)
            {
                var preds = g.PredecessorKeys(v);
                if (preds != null)
                {
                    foreach (var u in preds)
                        if ((g.NodeRaw(u)).Dummy != null)
                            return u;
                }
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


        public static Dictionary<string, Dictionary<string, bool>> findType1Conflicts(DagreGraph g, List<string[]> layering)
        {
            var conflicts = new Dictionary<string, Dictionary<string, bool>>(StringComparer.Ordinal);

            if (layering.Count > 1)
            {
                var prev = layering[0];
                for (int layerIdx = 1; layerIdx < layering.Count; layerIdx++)
                {
                    var layer = layering[layerIdx];
                    // last visited node in the previous layer that is incident on an inner segment.
                    int k0 = 0;
                    // Tracks the last node in this layer scanned for crossings with a type-1 segment.
                    var scanPos = 0;
                    var prevLayerLength = prev.Length;
                    var lastNode = layer[layer.Length - 1];

                    for (int idx = 0; idx < layer.Length; idx++)
                    {
                        var v = layer[idx];
                        var w = findOtherInnerSegmentNode(g, v);
                        var k1 = w != null ? (g.Node(w)).Order : prevLayerLength;

                        if (w != null || v == lastNode)
                        {
                            var end = Math.Min(idx + 1, layer.Length);
                            for (int si = scanPos; si < end; si++)
                            {
                                var scanNode = layer[si];
                                foreach (var u in g.PredecessorKeys(scanNode) ?? (IEnumerable<string>)Array.Empty<string>())
                                {
                                    var uLabel = g.Node(u);
                                    var uPos = uLabel.Order;
                                    if ((uPos < k0 || k1 < uPos) &&
                                    !(uLabel.Dummy != null && (g.Node(scanNode)).Dummy != null))
                                    {
                                        addConflict(conflicts, u, scanNode);
                                    }
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


        public static Dictionary<string, Dictionary<string, bool>> findType2Conflicts(DagreGraph g, List<string[]> layering)
        {
            var conflicts = new Dictionary<string, Dictionary<string, bool>>(StringComparer.Ordinal);

            if (layering.Count > 1)
            {
                var prev = layering[0];
                for (int layerIdx = 1; layerIdx < layering.Count; layerIdx++)
                {
                    var south = layering[layerIdx];
                    var prevNorthPos = -1;
                    int nextNorthPos = -1;
                    int southPos = 0;
                    int southLookahead = 0;

                    for (int idx = 0; idx < south.Length; idx++)
                    {
                        var v = south[idx];
                        southLookahead = idx;
                        var nd = g.Node(v);
                        if (nd.Dummy == "border")
                        {
                            if (g.PredecessorCount(v) != 0)
                            {
                                nextNorthPos = (g.Node(g.FirstPredecessor(v))).Order;
                                // scan
                                for (var si = southPos; si < southLookahead; si++)
                                {
                                    var sv = south[si];
                                    if ((g.Node(sv)).Dummy != null)
                                    {
                                        foreach (var u in g.PredecessorKeys(sv) ?? (IEnumerable<string>)Array.Empty<string>())
                                        {
                                            var uNode = g.Node(u);
                                            if (uNode.Dummy != null && (uNode.Order < prevNorthPos || uNode.Order > nextNorthPos))
                                            {
                                                addConflict(conflicts, u, sv);
                                            }
                                        }
                                    }
                                }
                                southPos = southLookahead;
                                prevNorthPos = nextNorthPos;
                            }
                        }
                    }
                    // Final scan
                    for (var si = southPos; si < south.Length; si++)
                    {
                        var sv = south[si];
                        if ((g.Node(sv)).Dummy != null)
                        {
                            foreach (var u in g.PredecessorKeys(sv) ?? (IEnumerable<string>)Array.Empty<string>())
                            {
                                var uNode = g.Node(u);
                                if (uNode.Dummy != null && (uNode.Order < prevNorthPos || uNode.Order > prev.Length))
                                {
                                    addConflict(conflicts, u, sv);
                                }
                            }
                        }
                    }

                    prev = south;
                }
            }
            return conflicts;
        }
    }
}
