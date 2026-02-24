namespace Mostlylucid.Dagre;

public static class DagreLayout
{
    /*
     * This idea comes from the Gansner paper: to account for edge labels in our
     * layout we split each rank in half by doubling minlen and halving ranksep.
     * Then we can place labels at these mid-points between nodes.
     *
     * We also add some minimal padding to the width to push the label for the edge
     * away from the edge itself a bit.
     */
    public static void MakeSpaceForEdgeLabels(DagreGraph g)
    {
        var graph = g.Graph();
        graph.RankSep = graph.RankSep / 2;
        foreach (var e in g.EdgesRaw())
        {
            var edge = g.EdgeRaw(e);
            edge.Minlen = edge.Minlen * 2;
            if (!string.Equals(edge.LabelPos, "c", StringComparison.OrdinalIgnoreCase))
            {
                if (graph.RankDir == "TB" || graph.RankDir == "BT")
                    edge.Width += edge.LabelOffset;
                else
                    edge.Height += edge.LabelOffset;
            }
        }
    }

    public static void RemoveSelfEdges(DagreGraph g)
    {
        var ar = (DagreEdgeIndex[])g.EdgesRaw().Clone();
        foreach (var e in ar)
            if (e.v == e.w)
            {
                var node = g.NodeRaw(e.v);
                if (node.SelfEdges == null) node.SelfEdges = new List<SelfEdgeInfo>();
                node.SelfEdges.Add(new SelfEdgeInfo { e = e, label = g.EdgeRaw(e) });
                g.RemoveEdge(e);
            }
    }

    public static void Rank(DagreGraph g)
    {
        string res = null;
        if (g.Graph().Ranker != null)
            res = g.Graph().Ranker;
        switch (res)
        {
            case "network-simplex":
                throw new NotImplementedException();
            case "tight-tree":
                throw new NotImplementedException();
            case "longest-path":
                throw new NotImplementedException();
            default:
                NetworkSimplexRanker(g);
                break;
        }
    }

    public static void NetworkSimplexRanker(DagreGraph g)
    {
        NetworkSimplex.NetworkSimplexMethod(g);
    }

    /// <summary>
    ///     Original layout pipeline — identical to the dagre.js reference implementation.
    ///     No indexed optimizations, no StraightenDummyChains, no post-processing patches.
    /// </summary>
    public static void RunLayout(DagreGraph g, Action<ExtProgressInfo> progress = null)
    {
        var ext = new ExtProgressInfo();
        progress?.Invoke(ext);

        MakeSpaceForEdgeLabels(g);
        RemoveSelfEdges(g);
        Acyclic.Run(g);
        NestingGraph.Run(g);

        ext.Caption = "rank";
        Rank(Util.AsNonCompoundGraph(g));

        InjectEdgeLabelProxies(g);
        RemoveEmptyRanks(g);
        NestingGraph.Cleanup(g);
        Util.NormalizeRanks(g);
        AssignRankMinMax(g);
        RemoveEdgeLabelProxies(g);

        ext.MainProgress = 0.1f;
        progress?.Invoke(ext);
        ext.Caption = "Normalize.run";
        Normalize.Run(g);
        ParentDummyChains._parentDummyChains(g);
        AddBorderSegments._addBorderSegments(g);

        ext.Caption = "order";
        ext.MainProgress = 0.3f;
        progress?.Invoke(ext);
        Order._order(g, f =>
        {
            ext.AdditionalProgress = f;
            progress?.Invoke(ext);
        });

        ext.MainProgress = 0.5f;
        progress?.Invoke(ext);
        InsertSelfEdges(g);

        CoordinateSystem.Adjust(g);
        Position(g);
        StraightenDummyChains(g);
        PositionSelfEdges(g);
        RemoveBorderNodes(g);

        ext.Caption = "undo";
        Normalize.Undo(g, f =>
        {
            ext.AdditionalProgress = f;
            progress?.Invoke(ext);
        });

        SimplifyEdgePoints(g);
        FixupEdgeLabelCoords(g);
        CoordinateSystem.Undo(g);
        TranslateGraph(g);
        AssignNodeIntersects(g);
        ReversePointsForReversedEdges(g);
        Acyclic.Undo(g);

        ext.AdditionalProgress = 1;
        ext.MainProgress = 1;
        progress?.Invoke(ext);
    }

    /*
     * Creates temporary dummy nodes that capture the rank in which each edge's
     * label is going to, if it has one of non-zero width and height. We do this
     * so that we can safely remove empty ranks while preserving balance for the
     * label's position.
     */
    public static void InjectEdgeLabelProxies(DagreGraph g)
    {
        foreach (var e in g.EdgesRaw())
        {
            var edge = g.EdgeRaw(e);
            if (edge.ContainsKey("width") && edge.Width != 0 && edge.ContainsKey("height") && edge.Height != 0)
            {
                var vNode = g.NodeRaw(e.v);
                var wNode = g.NodeRaw(e.w);

                var label = new NodeLabel();
                label.Rank = (wNode.Rank - vNode.Rank) / 2 + vNode.Rank;
                label.E = e;
                Util.AddDummyNode(g, "edge-proxy", label, "_ep");
            }
        }
    }

    public static void RemoveEmptyRanks(DagreGraph g)
    {
        var layers = new Dictionary<int, object>();

        // Ranks may not start at 0, so we need to offset them
        var allNodes = g.NodesRaw();
        var offset = int.MaxValue;
        var hasRanked = false;
        foreach (var z in allNodes)
        {
            var nl = g.NodeRaw(z);
            if (nl.ContainsKey("rank"))
            {
                if (nl.Rank < offset) offset = nl.Rank;
                hasRanked = true;
            }
        }

        if (hasRanked)
            foreach (var v in g.NodesRaw())
            {
                var nodeLabel = g.NodeRaw(v);
                if (!nodeLabel.ContainsKey("rank")) continue;
                var rank = -offset;

                rank += nodeLabel.Rank;
                if (!layers.TryGetValue(rank, out var layer))
                {
                    layer = new List<string>();
                    layers.Add(rank, layer);
                }

                ((List<string>)layer).Add(v);
            }

        if (layers.Count > 0 && g.Graph().NodeRankFactor > 0)
        {
            var delta = 0;
            var nodeRankFactor = g.Graph().NodeRankFactor;
            var maxLayer = 0;
            foreach (var k in layers.Keys)
                if (k > maxLayer)
                    maxLayer = k;
            for (var i = 0; i <= maxLayer; i++)
                if (!layers.ContainsKey(i) && i % nodeRankFactor != 0)
                    --delta;
                else if (delta != 0)
                    if (layers.TryGetValue(i, out var layerVals))
                    {
                        var vs = (List<string>)layerVals;
                        foreach (var v in vs) g.NodeRaw(v).Rank += delta;
                    }
        }
    }


    /// <summary>
    ///     Straighten dummy node chains and center fan-in/fan-out nodes.
    ///     Must run AFTER Position and BEFORE Normalize.Undo.
    /// </summary>
    public static void StraightenDummyChains(DagreGraph g)
    {
        var gg = g.Graph();
        if (gg.DummyChains == null || gg.DummyChains.Count == 0) return;

        var nodeSep = gg.NodeSep > 0 ? gg.NodeSep : 50;
        var halfSep = nodeSep / 2f;

        // Build rank → sorted list of (leftEdge, rightEdge) for real nodes only.
        // This lets us quickly find the free corridor a dummy sits in.
        var rankIntervals = new Dictionary<int, List<(float Left, float Right)>>();
        foreach (var nid in g.NodesRaw())
        {
            var n = g.NodeRaw(nid);
            if (n == null || n.Dummy != null) continue; // skip dummies
            var rank = n.Rank;
            if (!rankIntervals.TryGetValue(rank, out var list))
            {
                list = new List<(float, float)>();
                rankIntervals[rank] = list;
            }
            list.Add((n.X - n.Width / 2f - halfSep, n.X + n.Width / 2f + halfSep));
        }
        // Sort intervals by left edge for binary search
        foreach (var list in rankIntervals.Values)
            list.Sort((a, b) => a.Left.CompareTo(b.Left));

        foreach (var chainStartId in gg.DummyChains)
        {
            var startNode = g.NodeRaw(chainStartId);
            if (startNode == null) continue;

            var edgeObj = startNode.EdgeObj as DagreEdgeIndex;
            if (edgeObj == null) continue;

            var srcNode = g.NodeRaw(edgeObj.v);
            var tgtNode = g.NodeRaw(edgeObj.w);
            if (srcNode == null || tgtNode == null) continue;

            // Collect all dummy nodes in this chain
            var chain = new List<string>();
            var v = chainStartId;
            var node = g.NodeRaw(v);
            while (node != null && node.Dummy != null)
            {
                chain.Add(v);
                var w = g.FirstSuccessor(v);
                if (w == null) break;
                v = w;
                node = g.NodeRaw(v);
            }

            if (chain.Count == 0) continue;

            var srcX = srcNode.X;
            var srcY = srcNode.Y;
            var tgtX = tgtNode.X;
            var tgtY = tgtNode.Y;
            var deltaY = tgtY - srcY;

            for (var i = 0; i < chain.Count; i++)
            {
                var dummyNode = g.NodeRaw(chain[i]);
                if (dummyNode == null) continue;

                // Compute ideal X on the src→tgt line (shortest path)
                float idealX;
                if (Math.Abs(deltaY) < 0.001f)
                {
                    var t = (float)(i + 1) / (chain.Count + 1);
                    idealX = srcX + t * (tgtX - srcX);
                }
                else
                {
                    var t = (dummyNode.Y - srcY) / deltaY;
                    idealX = srcX + t * (tgtX - srcX);
                }

                // Blend toward ideal position — 1.0 fully straightens dummy chains.
                // GN already accounts for separation constraints globally.
                const float blend = 1.0f;
                var blendedX = dummyNode.X + blend * (idealX - dummyNode.X);

                // Only move if the blended position is in a free corridor
                if (IsPositionFree(rankIntervals, dummyNode.Rank, blendedX))
                    dummyNode.X = blendedX;
                // else: keep BK-assigned X (it already avoids real nodes)
            }
        }
    }

    /// <summary>
    ///     Check whether placing a dummy node at the given X on the given rank
    ///     would overlap with any real node (including nodeSep padding).
    /// </summary>
    /// <summary>
    ///     Center real nodes with high fan-in at the centroid of their predecessors.
    ///     BK aligns each node with ONE predecessor; for fan-in nodes (3+ predecessors)
    ///     this produces off-center positions. This pass moves them to the mean X of
    ///     all predecessors, respecting collision constraints.
    ///     Must run AFTER StraightenDummyChains (which builds rankIntervals).
    /// </summary>
    public static void CenterFanNodes(DagreGraph g)
    {
        // For each real node with 3+ predecessors, compute centroid of predecessor X
        foreach (var nid in g.NodesRaw())
        {
            var node = g.NodeRaw(nid);
            if (node == null || node.Dummy != null) continue;

            var predCount = g.PredecessorCount(nid);
            if (predCount < 3) continue;

            // Compute mean X of all predecessors (real nodes at edge source)
            var sumX = 0.0;
            var count = 0;
            foreach (var pred in g.PredecessorKeys(nid))
            {
                var predNode = g.NodeRaw(pred);
                if (predNode == null) continue;
                // Follow through dummy chains to find the real source node
                var cur = pred;
                var realPred = predNode;
                while (realPred.Dummy != null)
                {
                    var pp = g.FirstPredecessor(cur);
                    if (pp == null) break;
                    realPred = g.NodeRaw(pp);
                    if (realPred == null) { realPred = predNode; break; }
                    cur = pp;
                }
                sumX += realPred.X;
                count++;
            }

            if (count < 3) continue;
            var centroidX = (float)(sumX / count);

            // Move to centroid of predecessors
            var newX = centroidX;
            node.X = newX;
        }
    }

    private static bool IsPositionFree(Dictionary<int, List<(float Left, float Right)>> rankIntervals,
        int rank, float x)
    {
        if (!rankIntervals.TryGetValue(rank, out var intervals))
            return true; // no real nodes on this rank

        // Binary search for the first interval whose Right > x
        var lo = 0;
        var hi = intervals.Count - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (intervals[mid].Right <= x)
                lo = mid + 1;
            else
                hi = mid - 1;
        }
        // lo is now the index of the first interval that could contain x
        if (lo < intervals.Count && intervals[lo].Left <= x)
            return false; // x is inside this interval
        return true;
    }

    /// <summary>
    ///     Simplify edge waypoints using Douglas-Peucker to remove BK oscillation.
    ///     Reduces many intermediate waypoints to essential direction changes,
    ///     producing cleaner input for B-spline curve rendering.
    ///     Must run AFTER Normalize.Undo (which populates edge.Points).
    /// </summary>
    /// <summary>
    ///     Simplify edge waypoints using Ramer-Douglas-Peucker to remove near-collinear
    ///     intermediate points. This eliminates micro-bends from dummy node displacement
    ///     while preserving intentional routing around obstacles.
    ///     Must run AFTER Normalize.Undo (which populates edge.Points) and
    ///     BEFORE AssignNodeIntersects (which adds endpoint clips).
    /// </summary>
    /// <summary>
    ///     Retained as public API but now a no-op.
    ///     Douglas-Peucker simplification was destroying waypoint density
    ///     needed for smooth d3.curveBasis rendering. dagre.js does not
    ///     simplify edge points — all dummy-node waypoints are preserved.
    /// </summary>
    public static void SimplifyEdgePoints(DagreGraph g, double epsilon = 20.0)
    {
        // No-op: preserving all waypoints for smooth B-spline curves.
    }

    public static void ReversePointsForReversedEdges(DagreGraph g)
    {
        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e);
            if (edge.Reversed) edge.Points.Reverse();
        }
    }

    public static void AssignNodeIntersects(DagreGraph g)
    {
        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e);
            var nodeV = g.Node(e.v);
            var nodeW = g.Node(e.w);
            DagrePoint p1, p2;
            if (edge.Points == null)
            {
                edge.Points = new List<DagrePoint>();
                p1 = new DagrePoint(nodeW.X, nodeW.Y);
                p2 = new DagrePoint(nodeV.X, nodeV.Y);
            }
            else
            {
                p1 = edge.Points[0];
                p2 = edge.Points[edge.Points.Count - 1];
            }

            edge.Points.Insert(0, Util.IntersectNode(nodeV, p1));
            edge.Points.Add(Util.IntersectNode(nodeW, p2));
        }
    }

    public static void TranslateGraph(DagreGraph g)
    {
        var minX = double.PositiveInfinity;
        double maxX = 0;
        var minY = double.PositiveInfinity;
        double maxY = 0;
        var graphLabel = g.Graph();
        var marginX = graphLabel.MarginX;
        var marginY = graphLabel.MarginY;

        foreach (var v in g.Nodes())
        {
            var node = g.Node(v);
            minX = Math.Min(minX, node.X - node.Width / 2f);
            maxX = Math.Max(maxX, node.X + node.Width / 2f);
            minY = Math.Min(minY, node.Y - node.Height / 2f);
            maxY = Math.Max(maxY, node.Y + node.Height / 2f);
        }

        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e);
            if (edge.ContainsKey("x"))
            {
                minX = Math.Min(minX, edge.X - edge.Width / 2f);
                maxX = Math.Max(maxX, edge.X + edge.Width / 2f);
                minY = Math.Min(minY, edge.Y - edge.Height / 2f);
                maxY = Math.Max(maxY, edge.Y + edge.Height / 2f);
            }
        }

        minX -= marginX;
        minY -= marginY;

        foreach (var v in g.Nodes())
        {
            var node = g.Node(v);
            node.X -= (float)minX;
            node.Y -= (float)minY;
        }

        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e);
            if (edge.Points != null)
                for (var pi = 0; pi < edge.Points.Count; pi++)
                {
                    var p = edge.Points[pi];
                    edge.Points[pi] = new DagrePoint(p.X - (float)minX, p.Y - (float)minY);
                }

            if (edge.ContainsKey("x")) edge.X -= (float)minX;
            if (edge.ContainsKey("y")) edge.Y -= (float)minY;
        }

        graphLabel.Width = maxX - minX + marginX;
        graphLabel.Height = maxY - minY + marginY;
    }

    public static void FixupEdgeLabelCoords(DagreGraph g)
    {
        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e);
            if (edge.ContainsKey("x"))
            {
                if (edge.LabelPos == "l" || edge.LabelPos == "r") edge.Width -= edge.LabelOffset;
                switch (edge.LabelPos)
                {
                    case "l": edge.X -= edge.Width / 2f + edge.LabelOffset; break;
                    case "r": edge.X += edge.Width / 2f + edge.LabelOffset; break;
                }
            }
        }
    }

    public static DagrePoint MakePoint(float x, float y)
    {
        return new DagrePoint(x, y);
    }

    public static DagrePoint MakePoint(double x, double y)
    {
        return new DagrePoint(x, y);
    }

    public static void PositionSelfEdges(DagreGraph g)
    {
        foreach (var v in g.Nodes())
        {
            var node = g.Node(v);
            if (node.Dummy == "selfedge")
            {
                var edgeObj = (DagreEdgeIndex)node.E;
                var selfNode = g.Node(edgeObj.v);
                var x = selfNode.X + selfNode.Width / 2f;
                var y = selfNode.Y;
                var dx = node.X - x;
                var dy = selfNode.Height / 2f;
                var label = (EdgeLabel)node.Label;
                g.SetEdge(edgeObj.v, edgeObj.w, label, edgeObj.name);
                g.RemoveNode(v);
                label.Points = new List<DagrePoint>
                {
                    new(x + 2 * dx / 3, y - dy),
                    new(x + 5 * dx / 6, y - dy),
                    new(x + dx, y),
                    new(x + 5 * dx / 6, y + dy),
                    new(x + 2 * dx / 3, y + dy)
                };
                label.X = node.X;
                label.Y = node.Y;
            }
        }
    }

    public static void Position(DagreGraph g)
    {
        g = Util.AsNonCompoundGraph(g);


        var layering = Util.BuildLayerMatrix(g);
        var rankSep = g.Graph().RankSep;
        double prevY = 0;
        foreach (var layer in layering)
        {
            float maxHeight = 0;
            foreach (var v in layer)
            {
                var h = g.Node(v).Height;
                if (h > maxHeight) maxHeight = h;
            }

            foreach (var v in layer) g.Node(v).Y = (float)(prevY + maxHeight / 2f);

            prevY += maxHeight + rankSep;
        }

        Dictionary<string, float> xCoords;
        try
        {
            xCoords = BrandesKopf.PositionX(g);
        }
        catch (KeyNotFoundException)
        {
            // BK can fail on certain random graph topologies due to block graph
            // traversal issues. Fall back to Gansner-North network simplex positioning.
            xCoords = GansnerNorthPosition.PositionX(g);
        }
        foreach (var kvp in xCoords) g.Node(kvp.Key).X = kvp.Value;
    }


    public static void RemoveBorderNodes(DagreGraph g)
    {
        foreach (var v in g.Nodes())
            if (g.HasChildren(v))
            {
                var node = g.Node(v);
                var t = g.Node(node.BorderTop);
                var b = g.Node(node.BorderBottom);
                string lastKey1 = null;
                foreach (var k in node.BorderLeft.Keys) lastKey1 = k;
                var l = g.Node(node.BorderLeft[lastKey1]);
                string lastKey2 = null;
                foreach (var k in node.BorderRight.Keys) lastKey2 = k;
                var r = g.Node(node.BorderRight[lastKey2]);
                node.Width = Math.Abs(r.X - l.X);
                node.Height = Math.Abs(b.Y - t.Y);
                node.X = l.X + node.Width / 2;
                node.Y = t.Y + node.Height / 2;
            }

        g.BeginBatch();
        try
        {
            foreach (var v in g.Nodes())
            {
                var nd = g.Node(v);
                if (nd.Dummy == "border") g.RemoveChainNode(v);
            }
        }
        finally
        {
            g.EndBatch();
        }
    }

    public static void InsertSelfEdges(DagreGraph g)
    {
        var layers = Util.BuildLayerMatrix(g);
        foreach (var layer in layers)
        {
            var orderShift = 0;
            for (var i = 0; i < layer.Length; i++)
            {
                var v = layer[i];
                var node = g.Node(v);

                node.Order = i + orderShift;
                if (node.SelfEdges != null && node.ContainsKey("selfEdges"))
                {
                    foreach (var selfEdge in node.SelfEdges)
                    {
                        var selfLabel = selfEdge.label;
                        var attrs = new NodeLabel();
                        attrs.Width = selfLabel.Width;
                        attrs.Height = selfLabel.Height;
                        attrs.Rank = node.Rank;
                        attrs.Order = i + ++orderShift;
                        attrs.E = selfEdge.e;
                        attrs.Label = selfLabel;
                        Util.AddDummyNode(g, "selfedge", attrs, "_se");
                    }

                    node.Remove("selfEdges");
                }
            }
        }
    }

    public static void RemoveEdgeLabelProxies(DagreGraph g)
    {
        g.BeginBatch();
        try
        {
            foreach (var v in g.NodesRaw())
            {
                var node = g.NodeRaw(v);
                if (node.Dummy == "edge-proxy")
                {
                    g.EdgeRaw((DagreEdgeIndex)node.E).LabelRank = node.Rank;
                    g.RemoveNode(v);
                }
            }
        }
        finally
        {
            g.EndBatch();
        }
    }

    public static void AssignRankMinMax(DagreGraph g)
    {
        var maxRank = 0;
        foreach (var v in g.NodesRaw())
        {
            var node = g.NodeRaw(v);
            if (node.BorderTop != null)
            {
                node.MinRank = g.NodeRaw(node.BorderTop).Rank;
                node.MaxRank = g.NodeRaw(node.BorderBottom).Rank;
                maxRank = Math.Max(maxRank, node.MaxRank);
            }
        }

        g.Graph().MaxRank = maxRank;
    }
}