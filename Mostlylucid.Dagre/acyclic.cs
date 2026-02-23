namespace Mostlylucid.Dagre;

public class Acyclic
{
    public static void Undo(DagreGraph g)
    {
        foreach (var e in g.Edges())
        {
            var label = g.Edge(e);
            if (label.Reversed)
            {
                g.RemoveEdge(e);

                var forwardName = label.ForwardName;
                label.Reversed = false;
                label.ForwardName = null;

                g.SetEdge(e.w, e.v, label, forwardName);
            }
        }
    }

    public static Func<DagreEdgeIndex, int> WeightFn(DagreGraph g)
    {
        return e => g.Edge(e).Weight;
    }

    public static void Run(DagreGraph g)
    {
        var cyclicer = g.Graph().Acyclicer ?? "";
        var fas = cyclicer == "greedy"
            ? GreedyFAS(g, WeightFn(g))
            : DfsFAS(g);
        foreach (var e in fas)
        {
            var label = g.Edge(e);
            g.RemoveEdge(e);
            label.ForwardName = e.name;
            label.Reversed = true;

            g.SetEdge(e.w, e.v, label, Util.UniqueId("rev"));
        }
    }

    public static DagreEdgeIndex[] GreedyFAS(DagreGraph g, Func<DagreEdgeIndex, int> wf)
    {
        throw new NotImplementedException();
    }

    public static DagreEdgeIndex[] DfsFAS(DagreGraph g)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var fas = new List<DagreEdgeIndex>();
        // HashSet for O(1) cycle detection — need Contains + Remove semantics
        var stack = new HashSet<string>(StringComparer.Ordinal);

        void Dfs(string v)
        {
            if (!visited.Add(v))
                return;

            stack.Add(v);
            foreach (var e in g.OutEdges(v))
                if (stack.Contains(e.w))
                    fas.Add(e);
                else
                    Dfs(e.w);
            stack.Remove(v);
        }

        foreach (var item in g.NodesRaw()) Dfs(item);
        return fas.ToArray();
    }
}