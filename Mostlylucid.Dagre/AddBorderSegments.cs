namespace Mostlylucid.Dagre;

public class AddBorderSegments
{
    public static void AddBorderNode(DagreGraph g, string prop, string prefix, string sg, NodeLabel sgNode, int rank)
    {
        var label = new NodeLabel();
        label.Width = 0f;
        label.Height = 0f;
        label.Rank = rank;
        label.BorderType = prop;
        label.Dummy = "border";

        string prev = null;
        var borderMap = prop == "borderLeft" ? sgNode.BorderLeft : sgNode.BorderRight;
        if (borderMap != null) borderMap.TryGetValue((rank - 1).ToString(), out prev);
        var curr = Util.UniqueId(prefix);
        g.SetNodeDummy(curr, label);
        borderMap[rank.ToString()] = curr;
        g.SetParent(curr, sg);
        if (prev != null)
        {
            var edgeLabel = new EdgeLabel();
            edgeLabel.Weight = 1;
            g.SetEdgeFast(prev, curr, edgeLabel, null);
        }
    }


    public static void _addBorderSegments(DagreGraph g)
    {
        g.BeginBatch();
        try
        {
            Action<string> dfs = null;
            dfs = v =>
            {
                var children = g.Children(v);
                if (children != null && children.Length > 0)
                    foreach (var item in children)
                        dfs(item);

                var node = g.NodeRaw(v);
                if (node.ContainsKey("minRank"))
                {
                    node.BorderLeft = new Dictionary<string, string>();
                    node.BorderRight = new Dictionary<string, string>();
                    for (int rank = node.MinRank, maxRank = node.MaxRank + 1;
                         rank < maxRank;
                         ++rank)
                    {
                        AddBorderNode(g, "borderLeft", "_bl", v, node, rank);
                        AddBorderNode(g, "borderRight", "_br", v, node, rank);
                    }
                }
            };

            foreach (var item in g.Children()) dfs(item);
        }
        finally
        {
            g.EndBatch();
        }
    }
}