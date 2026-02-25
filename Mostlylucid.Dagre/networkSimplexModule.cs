namespace Mostlylucid.Dagre;

public class NetworkSimplex
{
    public static void InitLowLimValues(DagreGraph tree, string root = null)
    {
        if (root == null) root = tree.NodesRaw()[0];

        DfsAssignLowLim(tree, new HashSet<string>(), 1, root);
    }

    public static int DfsAssignLowLim(DagreGraph tree, HashSet<string> visited, int nextLim, string v,
        string parent = null)
    {
        var low = nextLim;
        var label = tree.NodeRaw(v);

        visited.Add(v);
        foreach (var w in tree.Neighbors(v))
            if (!visited.Contains(w))
                nextLim = DfsAssignLowLim(tree, visited, nextLim, w, v);

        label.Low = low;
        label.Lim = nextLim++;
        if (parent != null)
            label.Parent = parent;
        else
            label.Parent = null;

        return nextLim;
    }

    public static string[] Postorder(DagreGraph t, string[] g)
    {
        return GraphLib.Dfs(t, g, "post");
    }

    public static string[] Preorder(DagreGraph t, string[] g)
    {
        return GraphLib.Dfs(t, g, "pre");
    }

    public static void InitCutValues(DagreGraph t, DagreGraph g)
    {
        var vs = Postorder(t, t.NodesRaw());
        // Skip the last element (root) - iterate up to Length-1
        for (var i = 0; i < vs.Length - 1; i++) AssignCutValue(t, g, vs[i]);
    }

    public static void AssignCutValue(DagreGraph t, DagreGraph g, string child)
    {
        var childLab = t.NodeRaw(child);
        var parent = childLab.Parent;
        var edge = t.EdgeRaw(child, parent);
        if (edge != null)
        {
            var res = CalcCutValue(t, g, child);
            edge.Cutvalue = res;
        }
    }

    public static int CalcCutValue(DagreGraph t, DagreGraph g, string child)
    {
        var childLab = t.NodeRaw(child);
        var parent = childLab.Parent;
        var childIsTail = true;
        var graphEdge = g.EdgeRaw(child, parent);
        var cutValue = 0;

        if (graphEdge == null)
        {
            childIsTail = false;
            graphEdge = g.EdgeRaw(parent, child);
        }

        cutValue = graphEdge.Weight;

        foreach (var e in g.NodeEdges(child))
        {
            var isOutEdge = e.v == child;
            var other = isOutEdge ? e.w : e.v;
            if (other != parent)
            {
                var pointsToHead = isOutEdge == childIsTail;
                var otherWeight = g.EdgeRaw(e).Weight;

                cutValue += pointsToHead ? otherWeight : -otherWeight;
                if (IsTreeEdge(t, child, other))
                {
                    var otherCutValue = t.EdgeRaw(child, other).Cutvalue;
                    cutValue += pointsToHead ? -otherCutValue : otherCutValue;
                }
            }
        }

        return cutValue;
    }

    public static bool IsTreeEdge(DagreGraph tree, string u, string v)
    {
        return tree.EdgeRaw(u, v) != null;
    }

    public static DagreEdgeIndex EnterEdge(DagreGraph t, DagreGraph g, DagreEdgeIndex edge)
    {
        var v = edge.v;
        var w = edge.w;

        if (g.EdgeRaw(v, w) == null)
        {
            v = edge.w;
            w = edge.v;
        }

        var vLabel = t.NodeRaw(v);
        var wLabel = t.NodeRaw(w);
        var tailLabel = vLabel;
        var flip = false;

        if (vLabel.Lim > wLabel.Lim)
        {
            tailLabel = wLabel;
            flip = true;
        }

        // Single-pass min-slack scan instead of Where+OrderBy+First
        DagreEdgeIndex best = null;
        int? bestSlack = null;
        foreach (var ee in g.EdgesRaw())
            if (flip == IsDescendant(t, t.NodeRaw(ee.v), tailLabel) &&
                flip != IsDescendant(t, t.NodeRaw(ee.w), tailLabel))
            {
                var s = Slack(g, ee);
                if (best == null || (s != null && (bestSlack == null || s < bestSlack)))
                {
                    best = ee;
                    bestSlack = s;
                }
            }

        return best;
    }

    public static bool IsDescendant(DagreGraph tree, NodeLabel vLabel, NodeLabel rootLabel)
    {
        return rootLabel.Low <= vLabel.Lim && vLabel.Lim <= rootLabel.Lim;
    }

    public static void NetworkSimplexMethod(DagreGraph g)
    {
        g = Util.Simplify(g);

        LongestPath(g);

        var tree = FeasibleTree(g);

        InitLowLimValues(tree);

        InitCutValues(tree, g);

        DagreEdgeIndex e = null, f = null;
        var step = 0;
        while ((e = LeaveEdge(tree)) != null)
        {
            f = EnterEdge(tree, g, e);
            ExchangeEdges(tree, g, e, f, step);
            step++;
        }
    }

    public static void ExchangeEdges(DagreGraph t, DagreGraph g, DagreEdgeIndex e, DagreEdgeIndex f, int step)
    {
        t.RemoveEdge(e.v, e.w);
        t.SetEdge(f.v, f.w, new EdgeLabel());

        InitLowLimValues(t);
        InitCutValues(t, g);
        UpdateRanks(t, g);
    }

    public static DagreEdgeIndex LeaveEdge(DagreGraph tree)
    {
        foreach (var e in tree.EdgesRaw())
        {
            var edge = tree.EdgeRaw(e);
            if (edge != null && edge.Cutvalue < 0)
                return e;
        }

        return null;
    }

    public static void UpdateRanks(DagreGraph t, DagreGraph g)
    {
        string root = null;
        foreach (var v in t.Nodes())
        {
            var nl = g.Node(v);
            if (nl.Parent == null)
            {
                root = v;
                break;
            }
        }

        var vs = Preorder(t, new[] { root });
        for (var i = 1; i < vs.Length; i++)
        {
            var v = vs[i];
            var tNodeLabel = t.Node(v);
            var parent = tNodeLabel.Parent;

            var edge = g.EdgeRaw(v, parent);
            var flipped = false;

            if (edge == null)
            {
                edge = g.EdgeRaw(parent, v);
                flipped = true;
            }

            var gNode = g.Node(v);
            var gParent = g.Node(parent);
            gNode.Rank = gParent.Rank + (flipped ? edge.Minlen : -edge.Minlen);
        }
    }

    public static void LongestPath(DagreGraph g)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);

        int Dfs(string v)
        {
            var label = g.NodeRaw(v);
            if (!visited.Add(v))
                return label.Rank;

            var rank = int.MaxValue;
            foreach (var e in g.OutEdges(v))
            {
                var edgeLabel = g.EdgeRaw(e);
                var x = Dfs(e.w) - edgeLabel.Minlen;
                if (x < rank)
                    rank = x;
            }

            if (rank == int.MaxValue)
                rank = 0;

            label.Rank = rank;
            return label.Rank;
        }

        foreach (var item in g.Sources()) Dfs(item);
    }

    public static int? Slack(DagreGraph g, DagreEdgeIndex e)
    {
        var node1 = g.NodeRaw(e.w);
        var node2 = g.NodeRaw(e.v);
        var edge = g.EdgeRaw(e);
        if (node1 == null || node2 == null || edge == null) return null;
        return node1.Rank - node2.Rank - edge.Minlen;
    }

    public static DagreGraph FeasibleTree(DagreGraph g)
    {
        var t = new DagreGraph(false) { _isDirected = false };

        var start = g.NodesRaw()[0];
        var size = g.NodeCount();
        t.SetNode(start, new NodeLabel());

        DagreEdgeIndex edge;
        int delta;
        while (TightTree(t, g) < size)
        {
            edge = FindMinSlackEdge(t, g);
            delta = t.HasNode(edge.v) ? (int)Slack(g, edge) : -(int)Slack(g, edge);
            ShiftRanks(t, g, delta);
        }

        return t;
    }

    public static int TightTree(DagreGraph t, DagreGraph g)
    {
        var nodes = t.NodesRaw();
        var stack = new List<string>(nodes.Length);
        for (var i = nodes.Length - 1; i >= 0; i--)
            stack.Add(nodes[i]);
        while (stack.Count > 0)
        {
            var v = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            foreach (var e in g.NodeEdges(v))
            {
                var edgeV = e.v;
                var w = v == edgeV ? e.w : edgeV;
                var _slack = Slack(g, e);
                if (!t.HasNode(w) && (_slack == null || _slack == 0))
                {
                    t.SetNode(w, new NodeLabel());
                    t.SetEdge(v, w, new EdgeLabel());
                    stack.Add(w);
                }
            }
        }

        return t.NodeCount();
    }

    public static DagreEdgeIndex FindMinSlackEdge(DagreGraph t, DagreGraph g)
    {
        DagreEdgeIndex best = null;
        int? bestSlack = null;
        foreach (var e in g.Edges())
            if (t.HasNode(e.v) != t.HasNode(e.w))
            {
                var s = Slack(g, e);
                if (best == null || (s != null && (bestSlack == null || s < bestSlack)))
                {
                    best = e;
                    bestSlack = s;
                }
            }

        return best;
    }

    public static void ShiftRanks(DagreGraph t, DagreGraph g, int delta)
    {
        foreach (var v in t.Nodes()) g.Node(v).Rank += delta;
    }
}

public class GraphLib
{
    public static string[] Dfs(DagreGraph g, string[] vs, string order)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var acc = new List<string>();
        var postorder = order == "post";

        foreach (var v in vs)
        {
            if (!g.HasNode(v))
                throw new DagreException("graph does not have node: " + v);
            DoDfs(g, v, postorder, visited, acc);
        }

        return acc.ToArray();
    }

    public static void DoDfs(DagreGraph g, string v, bool postorder, HashSet<string> visited, List<string> acc)
    {
        if (!visited.Add(v)) return;

        if (!postorder) acc.Add(v);

        // Get neighbors without per-call array allocation via KeyCollection iteration
        if (g._isDirected)
        {
            var keys = g.SuccessorKeys(v);
            if (keys != null)
            {
                // Sort is needed for deterministic ordering — collect to temp list
                var sorted = new List<string>(keys);
                sorted.Sort(StringComparer.Ordinal);
                foreach (var w in sorted)
                    DoDfs(g, w, postorder, visited, acc);
            }
        }
        else
        {
            // Undirected — use preds + sucs merged
            var neighbors = g.Neighbors(v);
            if (neighbors != null)
            {
                Array.Sort(neighbors, StringComparer.Ordinal);
                foreach (var w in neighbors)
                    DoDfs(g, w, postorder, visited, acc);
            }
        }

        if (postorder) acc.Add(v);
    }
}
