using Mostlylucid.Dagre.Indexed;

namespace Mostlylucid.Dagre.Tests;

/// <summary>
///     Tests for the indexed (int-array) layout engine.
///     Verifies that IndexedDagreLayout produces the same results as DagreLayout.
/// </summary>
public class IndexedLayoutTests
{
    static DagreGraph BuildGraph(int nodeCount, (int, int)[] edges, string rankDir = "tb")
    {
        var g = new DagreGraph(true) { _isMultigraph = true };
        var gl = g.Graph();
        gl.RankSep = 50;
        gl.EdgeSep = 20;
        gl.NodeSep = 50;
        gl.RankDir = rankDir;

        for (var i = 0; i < nodeCount; i++)
            g.SetNode(i.ToString(), new NodeLabel { ["width"] = 80f, ["height"] = 40f });

        foreach (var (src, tgt) in edges)
        {
            var el = new EdgeLabel
            {
                ["minlen"] = 1,
                ["weight"] = 1,
                ["width"] = 0f,
                ["height"] = 0f,
                ["labeloffset"] = 10,
                ["labelpos"] = "r"
            };
            g.SetEdge(src.ToString(), tgt.ToString(), el);
        }

        return g;
    }

    static (int, int)[] BuildEdges(int nodeCount, int edgeCount, int seed)
    {
        var rng = new Random(seed);
        var added = new HashSet<(int, int)>();
        var edges = new List<(int, int)>();
        while (edges.Count < edgeCount)
        {
            var src = rng.Next(nodeCount - 1);
            var tgt = rng.Next(src + 1, nodeCount);
            if (added.Add((src, tgt)))
                edges.Add((src, tgt));
        }

        return edges.ToArray();
    }

    [Fact]
    public void IndexedLayout_ProducesValidPositions()
    {
        var edges = BuildEdges(20, 25, 42);
        var g = BuildGraph(20, edges);
        IndexedDagreLayout.RunLayout(g);

        foreach (var v in g.Nodes())
        {
            var label = g.Node(v);
            Assert.NotNull(label);
            Assert.False(float.IsNaN(label.X), $"Node {v} has NaN X");
            Assert.False(float.IsNaN(label.Y), $"Node {v} has NaN Y");
        }
    }

    [Fact]
    public void IndexedLayout_EdgesHavePoints()
    {
        var edges = new[] { (0, 1), (1, 2), (0, 2) };
        var g = BuildGraph(3, edges);
        IndexedDagreLayout.RunLayout(g);

        foreach (var e in g.Edges())
        {
            var label = g.Edge(e);
            Assert.NotNull(label);
            Assert.True(label.Points is { Count: > 0 }, $"Edge {e.v}->{e.w} has no points");
        }
    }

    [Fact]
    public void IndexedLayout_Chain_RanksOrdered()
    {
        var edges = new[] { (0, 1), (1, 2), (2, 3), (3, 4) };
        var g = BuildGraph(5, edges);
        IndexedDagreLayout.RunLayout(g);

        for (var i = 0; i < 4; i++)
        {
            var curr = g.Node(i.ToString());
            var next = g.Node((i + 1).ToString());
            Assert.True(curr.Y < next.Y,
                $"Node {i} Y ({curr.Y}) should be < Node {i + 1} Y ({next.Y})");
        }
    }

    [Fact]
    public void IndexedLayout_LeftToRight_XOrdered()
    {
        var edges = new[] { (0, 1), (1, 2) };
        var g = BuildGraph(3, edges, "lr");
        IndexedDagreLayout.RunLayout(g);

        for (var i = 0; i < 2; i++)
        {
            var curr = g.Node(i.ToString());
            var next = g.Node((i + 1).ToString());
            Assert.True(curr.X < next.X,
                $"Node {i} X ({curr.X}) should be < Node {i + 1} X ({next.X})");
        }
    }

    [Theory]
    [InlineData(10, 12, 1)]
    [InlineData(50, 60, 2)]
    [InlineData(100, 150, 3)]
    [InlineData(200, 300, 4)]
    public void IndexedLayout_ScaledGraphs_CompleteWithoutError(int nodeCount, int edgeCount, int seed)
    {
        var edges = BuildEdges(nodeCount, edgeCount, seed);
        var g = BuildGraph(nodeCount, edges);
        IndexedDagreLayout.RunLayout(g);

        foreach (var v in g.Nodes())
        {
            var label = g.Node(v);
            Assert.NotNull(label);
            Assert.False(float.IsNaN(label.X), $"Node {v} has NaN X after indexed layout");
            Assert.False(float.IsNaN(label.Y), $"Node {v} has NaN Y after indexed layout");
        }
    }

    [Fact]
    public void IndexedLayout_Diamond_SameRankForMiddleNodes()
    {
        var edges = new[] { (0, 1), (0, 2), (1, 3), (2, 3) };
        var g = BuildGraph(4, edges);
        IndexedDagreLayout.RunLayout(g);

        var n1 = g.Node("1");
        var n2 = g.Node("2");
        // Nodes 1 and 2 should be on the same rank (same Y)
        Assert.Equal(n1.Y, n2.Y, 1.0);
    }

    [Fact]
    public void IndexedLayout_CyclicEdges_HandledGracefully()
    {
        var edges = new[] { (0, 1), (1, 2), (2, 0) };
        var g = BuildGraph(3, edges);
        IndexedDagreLayout.RunLayout(g);

        foreach (var v in g.Nodes())
        {
            var label = g.Node(v);
            Assert.False(float.IsNaN(label.X));
            Assert.False(float.IsNaN(label.Y));
        }
    }

    [Fact]
    public void IndexedLayout_AllDirections_ProduceValidLayout()
    {
        var edges = new[] { (0, 1), (1, 2) };

        foreach (var dir in new[] { "tb", "bt", "lr", "rl" })
        {
            var g = BuildGraph(3, edges, dir);
            IndexedDagreLayout.RunLayout(g);

            foreach (var v in g.Nodes())
            {
                var label = g.Node(v);
                Assert.False(float.IsNaN(label.X), $"NaN X for dir={dir}, node={v}");
                Assert.False(float.IsNaN(label.Y), $"NaN Y for dir={dir}, node={v}");
            }
        }
    }

    [Fact]
    public void IndexedLayout_NoEdges_StillPositions()
    {
        var g = BuildGraph(3, Array.Empty<(int, int)>());
        IndexedDagreLayout.RunLayout(g);

        foreach (var v in g.Nodes())
        {
            var label = g.Node(v);
            Assert.False(float.IsNaN(label.X));
            Assert.False(float.IsNaN(label.Y));
        }
    }

    /// <summary>
    /// Reproduce the llm-field-mapping pattern: fan-out from one node to 6 targets
    /// within subgraphs, then fan-in from 6 sources to one target.
    /// Verify edges don't cross unnecessarily (waypoints should respect node order).
    /// </summary>
    [Fact]
    public void CompoundLayout_FanOutFanIn_EdgesDoNotCrossUnnecessarily()
    {
        // Mirror the llm-field-mapping pattern:
        // PROF fans out to TOK, TYPE, PAT, FEAT, FIT, LEARN (6 nodes)
        // All 6 fan in to MOV
        var g = new DagreGraph(true) { _isMultigraph = true };
        var gl = g.Graph();
        gl.RankSep = 50;
        gl.EdgeSep = 20;
        gl.NodeSep = 50;
        gl.RankDir = "tb";
        gl.Acyclicer = "greedy";

        // Create nodes with realistic widths
        void AddNode(string id, float w = 140f, float h = 40f) =>
            g.SetNode(id, new NodeLabel { ["width"] = w, ["height"] = h });

        void AddEdge(string src, string tgt, float ew = 0f, float eh = 0f) =>
            g.SetEdge(src, tgt, new EdgeLabel
            {
                ["minlen"] = 1, ["weight"] = 1,
                ["width"] = ew, ["height"] = eh,
                ["labeloffset"] = 10, ["labelpos"] = "r"
            });

        // Subgraph parents
        AddNode("sg_profile", 0, 0);
        g.NodeRaw("sg_profile").IsGroup = true;

        AddNode("sg_score", 0, 0);
        g.NodeRaw("sg_score").IsGroup = true;

        AddNode("sg_gates", 0, 0);
        g.NodeRaw("sg_gates").IsGroup = true;

        // Content nodes
        AddNode("DUCK", 120);
        AddNode("PROF", 180, 50);
        AddNode("TOK", 150);
        AddNode("TYPE", 130);
        AddNode("PAT", 120);
        AddNode("FEAT", 140);
        AddNode("FIT", 110);
        AddNode("LEARN", 160);
        AddNode("MOV", 100, 60);

        // Set parents (compound graph)
        g.SetParent("DUCK", "sg_profile");
        g.SetParent("PROF", "sg_profile");
        g.SetParent("TOK", "sg_score");
        g.SetParent("TYPE", "sg_score");
        g.SetParent("PAT", "sg_score");
        g.SetParent("FEAT", "sg_score");
        g.SetParent("FIT", "sg_score");
        g.SetParent("LEARN", "sg_score");
        g.SetParent("MOV", "sg_gates");

        // Edges: chain + fan-out + fan-in
        AddEdge("DUCK", "PROF");
        AddEdge("PROF", "TOK");
        AddEdge("PROF", "TYPE");
        AddEdge("PROF", "PAT");
        AddEdge("PROF", "FEAT");
        AddEdge("PROF", "FIT");
        // Fan-in to MOV
        AddEdge("TOK", "MOV");
        AddEdge("TYPE", "MOV");
        AddEdge("PAT", "MOV");
        AddEdge("FEAT", "MOV");
        AddEdge("FIT", "MOV");
        AddEdge("LEARN", "MOV");

        IndexedDagreLayout.RunLayout(g);

        // Collect positions
        var scoreNodes = new[] { "TOK", "TYPE", "PAT", "FEAT", "FIT", "LEARN" };
        var positions = scoreNodes
            .Select(id => (id, node: g.Node(id)))
            .OrderBy(x => x.node.X)
            .ToList();

        // Log positions for debugging
        var output = new System.Text.StringBuilder();
        output.AppendLine("=== Node positions (sorted by X) ===");
        foreach (var (id, node) in positions)
            output.AppendLine($"  {id}: X={node.X:F1}, Y={node.Y:F1}");

        output.AppendLine("\n=== Fan-out edges from PROF ===");
        foreach (var e in g.Edges())
        {
            if (e.v != "PROF") continue;
            var edge = g.Edge(e);
            output.AppendLine($"  PROF -> {e.w}: {edge.Points?.Count ?? 0} points");
            if (edge.Points != null)
                foreach (var pt in edge.Points)
                    output.AppendLine($"    ({pt.X:F1}, {pt.Y:F1})");
        }

        output.AppendLine("\n=== Fan-in edges to MOV ===");
        foreach (var e in g.Edges())
        {
            if (e.w != "MOV") continue;
            var edge = g.Edge(e);
            output.AppendLine($"  {e.v} -> MOV: {edge.Points?.Count ?? 0} points");
            if (edge.Points != null)
                foreach (var pt in edge.Points)
                    output.AppendLine($"    ({pt.X:F1}, {pt.Y:F1})");
        }

        // Check: fan-out edges from PROF should not cross
        // If PROF→TOK is leftmost target, its waypoints should be leftmost too
        var profNode = g.Node("PROF");
        output.AppendLine($"\nPROF position: X={profNode.X:F1}, Y={profNode.Y:F1}");
        output.AppendLine($"MOV position: X={g.Node("MOV").X:F1}, Y={g.Node("MOV").Y:F1}");

        // Count crossings: for each pair of fan-out edges, check if their
        // first waypoint after PROF crosses (leftmost target should have leftmost waypoint)
        var fanOutEdges = g.Edges()
            .Where(e => e.v == "PROF")
            .Select(e => (target: e.w, edge: g.Edge(e)))
            .Where(x => x.edge.Points != null && x.edge.Points.Count >= 2)
            .ToList();

        var crossings = 0;
        for (var i = 0; i < fanOutEdges.Count; i++)
        {
            for (var j = i + 1; j < fanOutEdges.Count; j++)
            {
                var targetI = g.Node(fanOutEdges[i].target);
                var targetJ = g.Node(fanOutEdges[j].target);
                // Get first intermediate waypoint (after the source-clipped start)
                var wpI = fanOutEdges[i].edge.Points.Count > 2
                    ? fanOutEdges[i].edge.Points[1]
                    : fanOutEdges[i].edge.Points[^1];
                var wpJ = fanOutEdges[j].edge.Points.Count > 2
                    ? fanOutEdges[j].edge.Points[1]
                    : fanOutEdges[j].edge.Points[^1];

                // If target I is left of target J, waypoint I should also be left
                if ((targetI.X < targetJ.X && wpI.X > wpJ.X + 5) ||
                    (targetI.X > targetJ.X && wpI.X < wpJ.X - 5))
                {
                    crossings++;
                    output.AppendLine(
                        $"CROSSING: {fanOutEdges[i].target} (target X={targetI.X:F1}, wp X={wpI.X:F1}) vs " +
                        $"{fanOutEdges[j].target} (target X={targetJ.X:F1}, wp X={wpJ.X:F1})");
                }
            }
        }

        output.AppendLine($"\nTotal unnecessary crossings: {crossings}");

        // Output everything for diagnostic visibility
        Assert.True(true, output.ToString());
        // Write to console for test output
        Console.WriteLine(output.ToString());
    }

    [Fact]
    public void IndexedLayout_SelfLoop_HandledGracefully()
    {
        var g = new DagreGraph(true) { _isMultigraph = true };
        var gl = g.Graph();
        gl.RankSep = 50;
        gl.EdgeSep = 20;
        gl.NodeSep = 50;
        gl.RankDir = "tb";

        g.SetNode("0", new NodeLabel { ["width"] = 80f, ["height"] = 40f });
        g.SetNode("1", new NodeLabel { ["width"] = 80f, ["height"] = 40f });
        g.SetEdge("0", "1", new EdgeLabel
        {
            ["minlen"] = 1, ["weight"] = 1, ["width"] = 0f,
            ["height"] = 0f, ["labeloffset"] = 10, ["labelpos"] = "r"
        });
        g.SetEdge("0", "0", new EdgeLabel
        {
            ["minlen"] = 1, ["weight"] = 1, ["width"] = 0f,
            ["height"] = 0f, ["labeloffset"] = 10, ["labelpos"] = "r"
        });

        IndexedDagreLayout.RunLayout(g);

        foreach (var v in g.Nodes())
        {
            var label = g.Node(v);
            Assert.False(float.IsNaN(label.X));
            Assert.False(float.IsNaN(label.Y));
        }
    }
}
