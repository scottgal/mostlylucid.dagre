namespace Mostlylucid.Dagre.Indexed;

/// <summary>
///     Indexed Normalize.Undo: walks dummy chains on IndexedGraph arrays
///     instead of DagreGraph dictionaries for point collection.
///     Then bulk-removes dummy nodes from DagreGraph.
/// </summary>
internal static class IndexedNormalize
{
    /// <summary>
    ///     Denormalize using IndexedGraph for fast point collection,
    ///     then remove dummies from DagreGraph.
    /// </summary>
    public static void Undo(IndexedGraph ig, DagreGraph g, Action<float> progress = null)
    {
        var gg = g.Graph();
        if (gg.DummyChains == null || gg.DummyChains.Count == 0) return;

        var dummyChains = gg.DummyChains;
        var count = dummyChains.Count;

        // Phase 1: Collect points from IndexedGraph (pure array ops, no dict lookups)
        for (var i = 0; i < count; i++)
        {
            progress?.Invoke((float)i / count);

            var chainStartId = dummyChains[i];

            // Get chain start from DagreGraph (need EdgeLabel/EdgeObj references)
            var startNode = g.NodeRaw(chainStartId);
            if (startNode == null) continue;

            var origLabel = (EdgeLabel)startNode.EdgeLabel;
            var edgeObj = (DagreEdgeIndex)startNode.EdgeObj;
            if (origLabel == null || edgeObj == null) continue;

            // Restore original edge in DagreGraph
            g.SetEdge(edgeObj.v, edgeObj.w, origLabel, edgeObj.name);

            // Walk chain using IndexedGraph for fast traversal + coordinate reads
            var nodeIdx = ig.TryGetNodeIndex(chainStartId);
            if (nodeIdx < 0)
            {
                // Fallback to DagreGraph walking
                FallbackUndoChain(g, chainStartId, startNode, origLabel);
                continue;
            }

            // Walk chain on IndexedGraph arrays
            var v = nodeIdx;
            while (v >= 0 && ig.NodeDummy[v] != null)
            {
                if (origLabel.Points == null)
                    origLabel.Points = new List<DagrePoint>();
                origLabel.Points.Add(new DagrePoint(ig.NodeX[v], ig.NodeY[v]));

                if (ig.NodeDummy[v] == "edge-label")
                {
                    origLabel.X = ig.NodeX[v];
                    origLabel.Y = ig.NodeY[v];
                    origLabel.Width = ig.NodeWidth[v];
                    origLabel.Height = ig.NodeHeight[v];
                }

                v = ig.FirstSuccessor(v);
            }
        }

        // Phase 2: Bulk remove all dummy nodes from DagreGraph
        g.BulkRemoveDummyChains(dummyChains);
    }

    /// <summary>
    ///     Fallback for chains not found in IndexedGraph (shouldn't happen normally).
    /// </summary>
    private static void FallbackUndoChain(DagreGraph g, string v, NodeLabel node, EdgeLabel origLabel)
    {
        while (node.Dummy != null)
        {
            var w = g.FirstSuccessor(v);
            if (origLabel.Points == null)
                origLabel.Points = new List<DagrePoint>();
            origLabel.Points.Add(new DagrePoint(node.X, node.Y));
            if (node.Dummy == "edge-label")
            {
                origLabel.X = node.X;
                origLabel.Y = node.Y;
                origLabel.Width = node.Width;
                origLabel.Height = node.Height;
            }

            v = w;
            node = g.Node(v);
        }
    }
}