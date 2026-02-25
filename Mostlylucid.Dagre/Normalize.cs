namespace Mostlylucid.Dagre;

public class Normalize
{
    /*
     * Breaks any long edges in the graph into short segments that span 1 layer
     * each. This operation is undoable with the denormalize function.
     *
     * Pre-conditions:
     *
     *    1. The input graph is a DAG.
     *    2. Each node in the graph has a "rank" property.
     *
     * Post-condition:
     *
     *    1. All edges in the graph have a length of 1.
     *    2. Dummy nodes are added where edges have been split into segments.
     *    3. The graph is augmented with a "dummyChains" attribute which contains
     *       the first dummy in each chain of dummy nodes produced.
     */
    public static void Run(DagreGraph g)
    {
        g.Graph().DummyChains = new List<string>();
        g.BeginBatch();
        try
        {
            foreach (var edge in g.EdgesRaw()) NormalizeEdge(g, edge);
        }
        finally
        {
            g.EndBatch();
        }
    }

    /*denormalize
     */
    public static void Undo(DagreGraph g, Action<float> progress = null)
    {
        var gg = g.Graph();
        if (gg.DummyChains != null)
        {
            g.BeginBatch();
            try
            {
                var list = gg.DummyChains;
                var i = 0;
                var count = list.Count;
                foreach (var chainStart in list)
                {
                    var perc = (float)i / count;
                    progress?.Invoke(perc);
                    i++;
                    var v = chainStart;
                    var node = g.Node(v);
                    var origLabel = node.EdgeLabel;
                    string w = null;
                    var edgeObj = node.EdgeObj;
                    g.SetEdge(edgeObj.v, edgeObj.w, origLabel, edgeObj.name);
                    while (node.Dummy != null)
                    {
                        w = g.FirstSuccessor(v);
                        g.RemoveChainNode(v);
                        if (origLabel.Points == null) origLabel.Points = new List<DagrePoint>();
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
            finally
            {
                g.EndBatch();
            }
        }
    }

    public static void NormalizeEdge(DagreGraph g, DagreEdgeIndex e)
    {
        var v = e.v;
        var vRank = g.NodeRaw(e.v).Rank;
        var w = e.w;
        var wRank = g.NodeRaw(w).Rank;
        var name = e.name;
        var edgeLabel = g.EdgeRaw(e);
        var labelRank = edgeLabel.LabelRank;
        if (wRank != vRank + 1)
        {
            g.RemoveEdge(e);
            string dummy = null;
            ++vRank;
            var edgeWeight = edgeLabel.Weight;
            for (var i = 0; vRank < wRank; ++i, ++vRank)
            {
                var attrs = new NodeLabel();
                attrs.Width = 0f;
                attrs.Height = 0f;
                attrs.EdgeLabel = edgeLabel;
                attrs.EdgeObj = e;
                attrs.Rank = vRank;

                // Use fast dummy node creation (skips existence check, uses small-capacity dicts)
                dummy = Util.UniqueId("_d");
                attrs.Dummy = "edge";
                g.SetNodeDummy(dummy, attrs);

                if (labelRank != 0 && vRank == labelRank)
                {
                    attrs.Width = edgeLabel.Width;
                    attrs.Height = edgeLabel.Height;
                    attrs.Dummy = "edge-label";
                    attrs.LabelPos = edgeLabel.LabelPos;
                }

                var jo1 = new EdgeLabel();
                jo1.Weight = edgeWeight;

                g.SetEdgeFast(v, dummy, jo1, name);
                if (i == 0) g.Graph().DummyChains.Add(dummy);
                v = dummy;
            }

            var jo2 = new EdgeLabel();
            jo2.Weight = edgeWeight;

            g.SetEdgeFast(v, w, jo2, name);
        }
    }
}