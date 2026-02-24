namespace Mostlylucid.Dagre;

/// <summary>
/// Assign X-coordinates using the Gansner-North network simplex method.
/// Constructs an auxiliary graph with separation constraints and edge
/// straightness weights, then solves via network simplex to find globally
/// optimal positions that minimize total weighted edge displacement.
///
/// This replaces Brandes-Kopf which uses local heuristics that cause
/// zigzag oscillation on dummy node chains.
///
/// Reference: Gansner, Koutsofios, North, Vo — "A Technique for Drawing
/// Directed Graphs" (IEEE TSE, 1993), Section 4.2.
/// </summary>
public static class GansnerNorthPosition
{
    public static Dictionary<string, float> PositionX(DagreGraph g)
    {
        var layering = Util.BuildLayerMatrix(g);
        var graphLabel = g.Graph();
        var nodeSep = graphLabel.NodeSep;

        // Build auxiliary graph for the NS solver
        var aux = new DagreGraph(false) { _isMultigraph = false };
        aux.SetGraph(new GraphLabel());

        // Add all original nodes to aux graph
        foreach (var layer in layering)
            foreach (var v in layer)
            {
                aux.SetNode(v, new NodeLabel());
            }

        // Add separation constraints between adjacent nodes in each layer
        foreach (var layer in layering)
        {
            for (var i = 0; i < layer.Length - 1; i++)
            {
                var left = layer[i];
                var right = layer[i + 1];
                var leftNode = g.Node(left);
                var rightNode = g.Node(right);
                var minSep = (int)Math.Ceiling(leftNode.Width / 2f + rightNode.Width / 2f + nodeSep);

                aux.SetEdge(left, right, new EdgeLabel
                {
                    Minlen = minSep,
                    Weight = 0
                });
            }
        }

        // Add edge-straightness constraints via virtual nodes.
        // We create virtual nodes for ALL edges (even weight-0 ones) to keep the
        // auxiliary graph connected — NS requires a connected graph for FeasibleTree.
        var vNodeCount = 0;
        foreach (var e in g.EdgesRaw())
        {
            var edgeLabel = g.EdgeRaw(e);
            if (edgeLabel == null) continue;

            var weight = GetEdgeWeight(g, e) * Math.Max(edgeLabel.Weight, 1);

            var vNode = "_gn_v" + vNodeCount++;
            aux.SetNode(vNode, new NodeLabel());

            aux.SetEdge(vNode, e.v, new EdgeLabel
            {
                Minlen = 0,
                Weight = weight
            });
            aux.SetEdge(vNode, e.w, new EdgeLabel
            {
                Minlen = 0,
                Weight = weight
            });
        }

        // Solve with network simplex — ranks become X-coordinates
        NetworkSimplex.NetworkSimplexMethod(aux);

        // Read back X-coordinates
        var result = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var layer in layering)
            foreach (var v in layer)
            {
                var auxNode = aux.Node(v);
                result[v] = auxNode.Rank;
            }

        return result;
    }

    /// <summary>
    /// Weight multiplier based on dummy node status of the edge endpoints.
    /// Both-dummy edges (long edge chains) get highest weight to keep them straight.
    /// </summary>
    private static int GetEdgeWeight(DagreGraph g, DagreEdgeIndex e)
    {
        var srcDummy = g.Node(e.v).Dummy;
        var tgtDummy = g.Node(e.w).Dummy;

        if (srcDummy != null && tgtDummy != null)
            return 32; // Both dummy — highest priority for straightness
        if (srcDummy != null || tgtDummy != null)
            return 8; // One dummy — medium priority
        return 1;     // Both real — base priority
    }
}
