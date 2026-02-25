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

    /// <summary>
    /// Greedy heuristic for finding a feedback arc set (FAS) - a set of edges whose removal
    /// makes the graph acyclic. Based on the algorithm from:
    /// P. Eades, X. Lin, and W.F. Smyth, "A fast and effective heuristic for the feedback arc set problem."
    /// Information Processing Letters, 47(6):319–323, 1993.
    ///
    /// The algorithm repeatedly removes sinks (nodes with no outgoing edges) and sources
    /// (nodes with no incoming edges), appending/prepending them to a sequence. Then it removes
    /// the node with the largest (outWeight - inWeight) delta. Edges that go "backwards" in
    /// the resulting sequence form the feedback arc set.
    /// </summary>
    public static DagreEdgeIndex[] GreedyFAS(DagreGraph g, Func<DagreEdgeIndex, int> wf)
    {
        if (g.NodeCount() <= 0)
            return Array.Empty<DagreEdgeIndex>();

        // Build a simple adjacency representation with weights
        var nodes = new Dictionary<string, FasEntry>(StringComparer.Ordinal);

        foreach (var v in g.NodesRaw())
        {
            nodes[v] = new FasEntry { V = v };
        }

        foreach (var e in g.Edges())
        {
            var weight = wf(e);
            var entry = nodes[e.v];
            entry.Out += weight;
            var wEntry = nodes[e.w];
            wEntry.In += weight;
        }

        // S_l: sequence built from left (sources), S_r: sequence built from right (sinks)
        var sL = new List<string>();
        var sR = new List<string>();
        var remaining = new HashSet<string>(nodes.Keys, StringComparer.Ordinal);

        // Iteratively remove sources and sinks, then the max-delta node
        while (remaining.Count > 0)
        {
            // Remove all sinks
            bool changed;
            do
            {
                changed = false;
                var toRemove = new List<string>();
                foreach (var v in remaining)
                {
                    var entry = nodes[v];
                    if (entry.Out == 0)
                    {
                        sR.Add(v);
                        toRemove.Add(v);
                        changed = true;
                    }
                }
                foreach (var v in toRemove)
                {
                    remaining.Remove(v);
                    // Update weights: removing a sink reduces outWeight of predecessors
                    foreach (var e in g.InEdges(v))
                    {
                        if (remaining.Contains(e.v))
                            nodes[e.v].Out -= wf(e);
                    }
                }
            } while (changed);

            // Remove all sources
            do
            {
                changed = false;
                var toRemove = new List<string>();
                foreach (var v in remaining)
                {
                    var entry = nodes[v];
                    if (entry.In == 0)
                    {
                        sL.Add(v);
                        toRemove.Add(v);
                        changed = true;
                    }
                }
                foreach (var v in toRemove)
                {
                    remaining.Remove(v);
                    // Update weights: removing a source reduces inWeight of successors
                    foreach (var e in g.OutEdges(v))
                    {
                        if (remaining.Contains(e.w))
                            nodes[e.w].In -= wf(e);
                    }
                }
            } while (changed);

            if (remaining.Count == 0)
                break;

            // Pick the node with maximum (out - in) delta
            string bestV = null;
            var bestDelta = int.MinValue;
            foreach (var v in remaining)
            {
                var entry = nodes[v];
                var delta = entry.Out - entry.In;
                if (delta > bestDelta)
                {
                    bestDelta = delta;
                    bestV = v;
                }
            }

            sL.Add(bestV);
            remaining.Remove(bestV);

            // Update neighbors
            foreach (var e in g.OutEdges(bestV))
            {
                if (remaining.Contains(e.w))
                    nodes[e.w].In -= wf(e);
            }
            foreach (var e in g.InEdges(bestV))
            {
                if (remaining.Contains(e.v))
                    nodes[e.v].Out -= wf(e);
            }
        }

        // Build final sequence: sL + reversed(sR)
        sR.Reverse();
        sL.AddRange(sR);

        // Assign positions in the sequence
        var position = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < sL.Count; i++)
            position[sL[i]] = i;

        // Edges that go backwards in the sequence form the FAS
        var fas = new List<DagreEdgeIndex>();
        foreach (var e in g.Edges())
        {
            if (position.TryGetValue(e.v, out var pv) &&
                position.TryGetValue(e.w, out var pw) &&
                pv >= pw)
            {
                fas.Add(e);
            }
        }

        return fas.ToArray();
    }

    private class FasEntry
    {
        public string V;
        public int In;
        public int Out;
    }

    public static DagreEdgeIndex[] DfsFAS(DagreGraph g)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var fas = new List<DagreEdgeIndex>();
        // HashSet for O(1) cycle detection - need Contains + Remove semantics
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
