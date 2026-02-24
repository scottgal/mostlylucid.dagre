using Mostlylucid.Dagre;
using Mostlylucid.Dagre.Indexed;

namespace Mostlylucid.Dagre.Tests;

public class GraphScenarioTests
{
    [Fact]
    public void WorkflowGraph_ProducesValidLayout()
    {
        var graph = new DagreInputGraph();

        var start = graph.AddNode(tag: "Start", width: 80, height: 40);
        var validate = graph.AddNode(tag: "Validate Input", width: 120, height: 50);
        var process = graph.AddNode(tag: "Process Data", width: 120, height: 50);
        var transform = graph.AddNode(tag: "Transform", width: 100, height: 50);
        var output = graph.AddNode(tag: "Output", width: 80, height: 40);

        graph.AddEdge(start, validate);
        graph.AddEdge(validate, process);
        graph.AddEdge(process, transform);
        graph.AddEdge(transform, output);

        graph.Layout();

        // Chain should produce increasing Y positions
        Assert.True(start.Y < validate.Y);
        Assert.True(validate.Y < process.Y);
        Assert.True(process.Y < transform.Y);
        Assert.True(transform.Y < output.Y);
    }

    [Fact]
    public void StateMachine_WithCycles_ProducesValidLayout()
    {
        var g = new DagreGraph(true) { _isMultigraph = true };
        g.SetGraph(new GraphLabel { RankSep = 50, NodeSep = 50, EdgeSep = 20, RankDir = "tb", Acyclicer = "greedy" });

        g.SetNode("idle", new NodeLabel { Width = 80, Height = 40 });
        g.SetNode("loading", new NodeLabel { Width = 80, Height = 40 });
        g.SetNode("active", new NodeLabel { Width = 80, Height = 40 });
        g.SetNode("paused", new NodeLabel { Width = 80, Height = 40 });

        g.SetEdge("idle", "loading", new EdgeLabel { Weight = 1, Minlen = 1 });
        g.SetEdge("loading", "active", new EdgeLabel { Weight = 1, Minlen = 1 });
        g.SetEdge("active", "paused", new EdgeLabel { Weight = 1, Minlen = 1 });
        g.SetEdge("paused", "active", new EdgeLabel { Weight = 1, Minlen = 1 });
        g.SetEdge("active", "idle", new EdgeLabel { Weight = 1, Minlen = 1 });

        IndexedDagreLayout.RunLayout(g);

        foreach (var v in g.Nodes())
        {
            var node = g.Node(v);
            Assert.True(node.X >= 0 && node.Y >= 0, $"Node {v} has invalid position ({node.X}, {node.Y})");
        }
    }

    /// <summary>
    /// Regression: GreedyFAS must reverse weight-0 (dotted) edges preferentially over
    /// weight-1 (solid) edges. This tests the exact scenario from the llm-field-mapping
    /// diagram where DB-.->LEARN_BOOST (weight=0) creates a cycle.
    /// </summary>
    [Fact]
    public void GreedyFAS_PrefersReversingWeightZeroEdges()
    {
        // Build a cycle: A → B → C → D (weight=1 each), D → A (weight=0)
        // GreedyFAS should reverse D→A (weight=0), not any weight=1 edge
        var g = new DagreGraph(false) { _isMultigraph = true };
        g.SetEdge("A", "B", new EdgeLabel { Weight = 1 });
        g.SetEdge("B", "C", new EdgeLabel { Weight = 1 });
        g.SetEdge("C", "D", new EdgeLabel { Weight = 1 });
        g.SetEdge("D", "A", new EdgeLabel { Weight = 0 }); // dotted back-edge

        var fas = Acyclic.GreedyFAS(g, Acyclic.WeightFn(g));

        // FAS should contain exactly the weight-0 edge D→A
        Assert.Single(fas);
        Assert.Equal("D", fas[0].v);
        Assert.Equal("A", fas[0].w);
    }

    /// <summary>
    /// Regression: weight-0 edges must not create phantom sinks/sources.
    /// Node with only weight-0 outgoing edges has structural edges and
    /// should NOT be classified as a sink.
    /// </summary>
    [Fact]
    public void GreedyFAS_WeightZeroEdgesDoNotCreatePhantomSinksSources()
    {
        // Cycle: PROF → LEARN → MOV → GATE → ACCEPT → API → DB -.-> LEARN
        // DB→LEARN is weight=0 (dotted). Without the structural edge fix,
        // DB would be classified as a sink and LEARN as a source, breaking the cycle
        // silently and producing no FAS edges — leaving the cycle in place.
        var g = new DagreGraph(false) { _isMultigraph = true };
        g.SetEdge("PROF", "LEARN", new EdgeLabel { Weight = 1 });
        g.SetEdge("LEARN", "MOV", new EdgeLabel { Weight = 1 });
        g.SetEdge("MOV", "GATE", new EdgeLabel { Weight = 1 });
        g.SetEdge("GATE", "ACCEPT", new EdgeLabel { Weight = 1 });
        g.SetEdge("ACCEPT", "API", new EdgeLabel { Weight = 1 });
        g.SetEdge("API", "DB", new EdgeLabel { Weight = 1 });
        g.SetEdge("DB", "LEARN", new EdgeLabel { Weight = 0 }); // dotted

        var fas = Acyclic.GreedyFAS(g, Acyclic.WeightFn(g));

        // FAS must not be empty — the cycle must be broken
        Assert.NotEmpty(fas);
        // The reversed edge should be the weight-0 one (DB→LEARN)
        Assert.Contains(fas, e => e.v == "DB" && e.w == "LEARN");
    }

    /// <summary>
    /// Regression: when multiple nodes have the same delta in GreedyFAS,
    /// the tiebreaker should prefer the node with lower in-weight so that
    /// cheaper edges are reversed.
    /// </summary>
    [Fact]
    public void GreedyFAS_TiebreaksOnInWeight()
    {
        // Two parallel cycles sharing a common path:
        // X → A → B → X (weight=1 back-edge, total FAS weight=1)
        // Y → A → B → Y (weight=0 back-edge, total FAS weight=0)
        // Both X and Y have delta = out-in = 1-0 = 1 after sources are drained.
        // The tiebreaker should pick Y (in=0) over X (in=1) so the weight-0 edge
        // B→Y is reversed instead of B→X.
        var g = new DagreGraph(false) { _isMultigraph = true };
        g.SetEdge("A", "B", new EdgeLabel { Weight = 1 });
        g.SetEdge("B", "X", new EdgeLabel { Weight = 1 });
        g.SetEdge("X", "A", new EdgeLabel { Weight = 1 }); // back-edge, weight=1
        g.SetEdge("B", "Y", new EdgeLabel { Weight = 0 });
        g.SetEdge("Y", "A", new EdgeLabel { Weight = 0 }); // back-edge, weight=0

        var fas = Acyclic.GreedyFAS(g, Acyclic.WeightFn(g));

        // Should reverse the weight-0 cycle, not the weight-1 cycle
        var totalFasWeight = fas.Sum(e => g.Edge(e)?.Weight ?? 0);
        Assert.True(totalFasWeight <= 1, $"FAS weight should be <=1, got {totalFasWeight}");
    }

    /// <summary>
    /// Test that GreedyFAS correctly handles the full llm-field-mapping cycle.
    /// The cycle DB-.->LEARN→MOV→...→DB should be broken by reversing the
    /// weight-0 dotted edge DB→LEARN, not any solid edge.
    /// </summary>
    [Fact]
    public void GreedyFAS_LlmFieldMapping_ReversesCorrectEdge()
    {
        var g = new DagreGraph(false) { _isMultigraph = true };

        // Full llm-field-mapping edge structure
        g.SetEdge("FILE", "DUCK", new EdgeLabel { Weight = 1 });
        g.SetEdge("DUCK", "PROF", new EdgeLabel { Weight = 1 });
        g.SetEdge("PROF", "TOK", new EdgeLabel { Weight = 1 });
        g.SetEdge("PROF", "TYPE", new EdgeLabel { Weight = 1 });
        g.SetEdge("PROF", "PAT", new EdgeLabel { Weight = 1 });
        g.SetEdge("PROF", "FEAT", new EdgeLabel { Weight = 1 });
        g.SetEdge("PROF", "FIT", new EdgeLabel { Weight = 1 });
        g.SetEdge("DB", "LEARN", new EdgeLabel { Weight = 0 }); // dotted
        g.SetEdge("TOK", "MOV", new EdgeLabel { Weight = 1 });
        g.SetEdge("TYPE", "MOV", new EdgeLabel { Weight = 1 });
        g.SetEdge("PAT", "MOV", new EdgeLabel { Weight = 1 });
        g.SetEdge("FEAT", "MOV", new EdgeLabel { Weight = 1 });
        g.SetEdge("FIT", "MOV", new EdgeLabel { Weight = 1 });
        g.SetEdge("LEARN", "MOV", new EdgeLabel { Weight = 1 });
        g.SetEdge("MOV", "GATE", new EdgeLabel { Weight = 1 });
        g.SetEdge("GATE", "READY", new EdgeLabel { Weight = 1 });
        g.SetEdge("GATE", "OLLAMA", new EdgeLabel { Weight = 1 });
        g.SetEdge("OLLAMA", "CONFIRM", new EdgeLabel { Weight = 1 });
        g.SetEdge("CONFIRM", "REVIEW", new EdgeLabel { Weight = 1 });
        g.SetEdge("REVIEW", "ACCEPT", new EdgeLabel { Weight = 1 });
        g.SetEdge("READY", "ACCEPT", new EdgeLabel { Weight = 1 });
        g.SetEdge("ACCEPT", "API", new EdgeLabel { Weight = 1 });
        g.SetEdge("API", "DB", new EdgeLabel { Weight = 1 });

        var fas = Acyclic.GreedyFAS(g, Acyclic.WeightFn(g));

        // FAS should reverse ONLY the dotted edge DB→LEARN (weight=0)
        Assert.Single(fas);
        Assert.Equal("DB", fas[0].v);
        Assert.Equal("LEARN", fas[0].w);

        // No solid (weight=1) edge should be reversed
        foreach (var e in fas)
            Assert.Equal(0, g.Edge(e).Weight);
    }

    [Fact]
    public void BinaryTree_ProducesValidLayout()
    {
        var graph = new DagreInputGraph();

        var root = graph.AddNode(tag: "Root", width: 60, height: 40);
        var left1 = graph.AddNode(tag: "L1", width: 60, height: 40);
        var right1 = graph.AddNode(tag: "R1", width: 60, height: 40);
        var left2 = graph.AddNode(tag: "L2", width: 60, height: 40);
        var right2 = graph.AddNode(tag: "R2", width: 60, height: 40);
        var left3 = graph.AddNode(tag: "L3", width: 60, height: 40);
        var right3 = graph.AddNode(tag: "R3", width: 60, height: 40);

        graph.AddEdge(root, left1);
        graph.AddEdge(root, right1);
        graph.AddEdge(left1, left2);
        graph.AddEdge(left1, right2);
        graph.AddEdge(right1, left3);
        graph.AddEdge(right1, right3);

        graph.Layout();

        // Root at top, children below
        Assert.True(root.Y < left1.Y);
        Assert.True(root.Y < right1.Y);
        // L1 and R1 on same rank
        Assert.Equal(left1.Y, right1.Y, 1.0);
        // Leaves on same rank
        Assert.Equal(left2.Y, right2.Y, 1.0);
        Assert.Equal(left2.Y, left3.Y, 1.0);
    }

    [Fact]
    public void FanOutFanIn_ProducesValidLayout()
    {
        var graph = new DagreInputGraph();

        var input = graph.AddNode(tag: "Input", width: 80, height: 40);
        var a = graph.AddNode(tag: "A", width: 60, height: 40);
        var b = graph.AddNode(tag: "B", width: 60, height: 40);
        var c = graph.AddNode(tag: "C", width: 60, height: 40);
        var d = graph.AddNode(tag: "D", width: 60, height: 40);
        var output = graph.AddNode(tag: "Output", width: 80, height: 40);

        graph.AddEdge(input, a);
        graph.AddEdge(input, b);
        graph.AddEdge(input, c);
        graph.AddEdge(input, d);
        graph.AddEdge(a, output);
        graph.AddEdge(b, output);
        graph.AddEdge(c, output);
        graph.AddEdge(d, output);

        graph.Layout();

        Assert.Equal(6, graph.Nodes().Length);
        Assert.True(input.Y < a.Y);
        Assert.True(a.Y < output.Y);
    }

    [Fact]
    public void DependencyGraph_ProducesValidLayout()
    {
        var layout = new GraphLayout(new GraphLayoutOptions
        {
            Direction = LayoutDirection.LeftToRight,
            RankSeparation = 60,
            NodeSeparation = 40
        });

        layout.AddNode("core", 100, 40);
        layout.AddNode("utils", 100, 40);
        layout.AddNode("logging", 100, 40);
        layout.AddNode("database", 100, 40);
        layout.AddNode("api", 100, 40);
        layout.AddNode("web", 100, 40);
        layout.AddNode("app", 100, 40);

        layout.AddEdge("core", "utils");
        layout.AddEdge("core", "logging");
        layout.AddEdge("utils", "database");
        layout.AddEdge("utils", "api");
        layout.AddEdge("logging", "api");
        layout.AddEdge("database", "web");
        layout.AddEdge("api", "web");
        layout.AddEdge("web", "app");

        var result = layout.Run();

        Assert.Equal(7, result.Nodes.Count);
        Assert.Equal(8, result.Edges.Count);
        // LR: core should be left of app
        Assert.True(result.Nodes["core"].X < result.Nodes["app"].X);
    }

    [Fact]
    public void ParallelPaths_ProducesValidLayout()
    {
        var graph = new DagreInputGraph();

        var start = graph.AddNode(tag: "Start", width: 80, height: 40);
        var path1a = graph.AddNode(tag: "P1-A", width: 70, height: 40);
        var path1b = graph.AddNode(tag: "P1-B", width: 70, height: 40);
        var path2a = graph.AddNode(tag: "P2-A", width: 70, height: 40);
        var path2b = graph.AddNode(tag: "P2-B", width: 70, height: 40);
        var path3a = graph.AddNode(tag: "P3-A", width: 70, height: 40);
        var path3b = graph.AddNode(tag: "P3-B", width: 70, height: 40);
        var end = graph.AddNode(tag: "End", width: 80, height: 40);

        graph.AddEdge(start, path1a);
        graph.AddEdge(start, path2a);
        graph.AddEdge(start, path3a);
        graph.AddEdge(path1a, path1b);
        graph.AddEdge(path2a, path2b);
        graph.AddEdge(path3a, path3b);
        graph.AddEdge(path1b, end);
        graph.AddEdge(path2b, end);
        graph.AddEdge(path3b, end);

        graph.Layout();

        Assert.Equal(8, graph.Nodes().Length);
        Assert.True(start.Y < path1a.Y);
        Assert.True(path1a.Y < path1b.Y);
        Assert.True(path1b.Y < end.Y);
    }

    [Fact]
    public void LargeGraph_PerformanceTest()
    {
        var layout = new GraphLayout(new GraphLayoutOptions
        {
            RankSeparation = 50,
            NodeSeparation = 50,
            EdgeSeparation = 20
        });

        for (var i = 0; i < 50; i++)
            layout.AddNode($"N{i}", 80, 40);

        var random = new Random(42);
        var added = new HashSet<(int, int)>();
        for (var i = 0; i < 80; i++)
        {
            var from = random.Next(0, 49);
            var to = random.Next(from + 1, 50);
            if (to < 50 && added.Add((from, to)))
                layout.AddEdge($"N{from}", $"N{to}");
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = layout.Run();
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 5000, $"Layout took {sw.ElapsedMilliseconds}ms, expected < 5000ms");
        Assert.Equal(50, result.Nodes.Count);

        foreach (var (id, node) in result.Nodes)
        {
            Assert.False(float.IsNaN(node.X), $"Node {id} has NaN X");
            Assert.False(float.IsNaN(node.Y), $"Node {id} has NaN Y");
        }
    }

    [Fact]
    public void LongChain_MinLenTest()
    {
        var graph = new DagreInputGraph();

        var a = graph.AddNode(tag: "A", width: 60, height: 40);
        var b = graph.AddNode(tag: "B", width: 60, height: 40);

        graph.AddEdge(a, b, minLen: 1);

        graph.Layout();

        Assert.True(b.Y > a.Y, "B should be below A in TB layout");
        Assert.True(b.X >= 0 && b.Y >= 0, "B should have valid coordinates");
    }
}
