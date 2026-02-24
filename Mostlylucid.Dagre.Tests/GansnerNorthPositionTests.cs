using Mostlylucid.Dagre;

namespace Mostlylucid.Dagre.Tests;

public class GansnerNorthPositionTests
{
    static EdgeLabel DefaultEdge() => new()
    {
        Minlen = 1,
        Weight = 1,
        Width = 0f,
        Height = 0f,
        LabelOffset = 10,
        LabelPos = "r"
    };

    static DagreGraph CreateGraph(string rankDir = "tb")
    {
        var g = new DagreGraph(true) { _isMultigraph = true };
        g.SetGraph(new GraphLabel { RankSep = 50, NodeSep = 50, EdgeSep = 20, RankDir = rankDir });
        return g;
    }

    static void AddNode(DagreGraph g, string id, float width = 80f, float height = 40f, string dummy = null)
    {
        var nl = new NodeLabel { Width = width, Height = height };
        if (dummy != null) nl.Dummy = dummy;
        g.SetNode(id, nl);
    }

    static void RankAndOrder(DagreGraph g)
    {
        // Assign ranks and orders manually for a controlled test
        var layering = Util.BuildLayerMatrix(g);
        for (var r = 0; r < layering.Count; r++)
        {
            var layer = layering[r];
            for (var o = 0; o < layer.Length; o++)
            {
                g.Node(layer[o]).Rank = r;
                g.Node(layer[o]).Order = o;
            }
        }
    }

    [Fact]
    public void LinearChain_ProducesConsistentXCoordinates()
    {
        var g = CreateGraph();
        for (var i = 0; i < 4; i++)
        {
            AddNode(g, i.ToString());
            g.Node(i.ToString()).Rank = i;
            g.Node(i.ToString()).Order = 0;
            if (i > 0) g.SetEdge((i - 1).ToString(), i.ToString(), DefaultEdge());
        }

        var xs = GansnerNorthPosition.PositionX(g);

        Assert.Equal(4, xs.Count);
        // All nodes in a linear chain should have the same X
        var xValues = xs.Values.ToList();
        for (var i = 1; i < xValues.Count; i++)
            Assert.Equal(xValues[0], xValues[i]);
    }

    [Fact]
    public void FanOut_ParentCenteredOverChildren()
    {
        var g = CreateGraph();
        AddNode(g, "A");
        AddNode(g, "B");
        AddNode(g, "C");
        AddNode(g, "D");

        g.Node("A").Rank = 0; g.Node("A").Order = 0;
        g.Node("B").Rank = 1; g.Node("B").Order = 0;
        g.Node("C").Rank = 1; g.Node("C").Order = 1;
        g.Node("D").Rank = 1; g.Node("D").Order = 2;

        g.SetEdge("A", "B", DefaultEdge());
        g.SetEdge("A", "C", DefaultEdge());
        g.SetEdge("A", "D", DefaultEdge());

        var xs = GansnerNorthPosition.PositionX(g);

        // Parent A should be near the center of B, C, D
        var childCenter = (xs["B"] + xs["C"] + xs["D"]) / 3f;
        Assert.True(Math.Abs(xs["A"] - childCenter) < 50,
            $"Parent A ({xs["A"]}) should be near child center ({childCenter})");
    }

    [Fact]
    public void FanIn_ChildCenteredUnderParents()
    {
        var g = CreateGraph();
        AddNode(g, "B");
        AddNode(g, "C");
        AddNode(g, "D");
        AddNode(g, "E");

        g.Node("B").Rank = 0; g.Node("B").Order = 0;
        g.Node("C").Rank = 0; g.Node("C").Order = 1;
        g.Node("D").Rank = 0; g.Node("D").Order = 2;
        g.Node("E").Rank = 1; g.Node("E").Order = 0;

        g.SetEdge("B", "E", DefaultEdge());
        g.SetEdge("C", "E", DefaultEdge());
        g.SetEdge("D", "E", DefaultEdge());

        var xs = GansnerNorthPosition.PositionX(g);

        var parentCenter = (xs["B"] + xs["C"] + xs["D"]) / 3f;
        Assert.True(Math.Abs(xs["E"] - parentCenter) < 50,
            $"Child E ({xs["E"]}) should be near parent center ({parentCenter})");
    }

    [Fact]
    public void Diamond_ProducesSymmetricLayout()
    {
        var g = CreateGraph();
        AddNode(g, "A");
        AddNode(g, "B");
        AddNode(g, "C");
        AddNode(g, "D");

        g.Node("A").Rank = 0; g.Node("A").Order = 0;
        g.Node("B").Rank = 1; g.Node("B").Order = 0;
        g.Node("C").Rank = 1; g.Node("C").Order = 1;
        g.Node("D").Rank = 2; g.Node("D").Order = 0;

        g.SetEdge("A", "B", DefaultEdge());
        g.SetEdge("A", "C", DefaultEdge());
        g.SetEdge("B", "D", DefaultEdge());
        g.SetEdge("C", "D", DefaultEdge());

        var xs = GansnerNorthPosition.PositionX(g);

        // A and D should be between B and C (centered or close to it)
        var minX = Math.Min(xs["B"], xs["C"]);
        var maxX = Math.Max(xs["B"], xs["C"]);
        Assert.True(xs["A"] >= minX && xs["A"] <= maxX,
            $"A ({xs["A"]}) should be between B ({xs["B"]}) and C ({xs["C"]})");
        Assert.True(xs["D"] >= minX && xs["D"] <= maxX,
            $"D ({xs["D"]}) should be between B ({xs["B"]}) and C ({xs["C"]})");
    }

    [Fact]
    public void AdjacentNodes_RespectMinimumSeparation()
    {
        var g = CreateGraph();
        AddNode(g, "A", 80);
        AddNode(g, "B", 80);

        g.Node("A").Rank = 0; g.Node("A").Order = 0;
        g.Node("B").Rank = 0; g.Node("B").Order = 1;

        // No edges — just separation constraint
        var xs = GansnerNorthPosition.PositionX(g);

        var minSep = 80f / 2 + 80f / 2 + 50; // width/2 + width/2 + nodeSep
        Assert.True(xs["B"] - xs["A"] >= minSep - 1,
            $"B ({xs["B"]}) - A ({xs["A"]}) = {xs["B"] - xs["A"]} should be >= {minSep}");
    }

    [Fact]
    public void DummyChain_ProducesStraightPath()
    {
        // Simulate a long edge with dummy nodes between rank 0 and rank 4
        var g = CreateGraph();
        AddNode(g, "A");
        AddNode(g, "d1", dummy: "edge");
        AddNode(g, "d2", dummy: "edge");
        AddNode(g, "d3", dummy: "edge");
        AddNode(g, "B");

        g.Node("A").Rank = 0; g.Node("A").Order = 0;
        g.Node("d1").Rank = 1; g.Node("d1").Order = 0;
        g.Node("d2").Rank = 2; g.Node("d2").Order = 0;
        g.Node("d3").Rank = 3; g.Node("d3").Order = 0;
        g.Node("B").Rank = 4; g.Node("B").Order = 0;

        g.SetEdge("A", "d1", DefaultEdge());
        g.SetEdge("d1", "d2", DefaultEdge());
        g.SetEdge("d2", "d3", DefaultEdge());
        g.SetEdge("d3", "B", DefaultEdge());

        var xs = GansnerNorthPosition.PositionX(g);

        // All nodes in a single chain should have the same X (straight edge)
        var xA = xs["A"];
        Assert.Equal(xA, xs["d1"]);
        Assert.Equal(xA, xs["d2"]);
        Assert.Equal(xA, xs["d3"]);
        Assert.Equal(xA, xs["B"]);
    }

    [Fact]
    public void ReturnsDictionaryWithAllNodes()
    {
        var g = CreateGraph();
        AddNode(g, "X");
        AddNode(g, "Y");
        AddNode(g, "Z");

        g.Node("X").Rank = 0; g.Node("X").Order = 0;
        g.Node("Y").Rank = 1; g.Node("Y").Order = 0;
        g.Node("Z").Rank = 2; g.Node("Z").Order = 0;

        g.SetEdge("X", "Y", DefaultEdge());
        g.SetEdge("Y", "Z", DefaultEdge());

        var xs = GansnerNorthPosition.PositionX(g);

        Assert.Contains("X", xs.Keys);
        Assert.Contains("Y", xs.Keys);
        Assert.Contains("Z", xs.Keys);
    }

    [Fact]
    public void FullLayout_BidirectionalEdges_DoesNotThrow()
    {
        // Reproduces the all-edge-types flowchart that caused NRE
        var g = new DagreGraph(true) { _isMultigraph = true };
        g.SetGraph(new GraphLabel
        {
            RankSep = 50, NodeSep = 50, EdgeSep = 20, RankDir = "lr",
            Acyclicer = "greedy"
        });

        foreach (var id in new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I" })
            g.SetNode(id, new NodeLabel { Width = 80, Height = 40 });

        var edge = new Func<EdgeLabel>(() => new EdgeLabel
        {
            Minlen = 1, Weight = 1, Width = 0, Height = 0,
            LabelOffset = 10, LabelPos = "r"
        });

        g.SetEdge("A", "B", edge());
        g.SetEdge("B", "C", edge());
        g.SetEdge("C", "D", edge());
        g.SetEdge("D", "E", edge());
        g.SetEdge("E", "D", edge()); // Back edge (bidirectional)
        g.SetEdge("E", "F", edge());
        g.SetEdge("F", "G", edge());
        g.SetEdge("G", "H", edge());
        g.SetEdge("H", "I", edge());

        Indexed.IndexedDagreLayout.RunLayout(g);

        // Verify all nodes got coordinates
        foreach (var id in new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I" })
        {
            var n = g.Node(id);
            Assert.True(n.ContainsKey("x"), $"Node {id} should have X");
            Assert.True(n.ContainsKey("y"), $"Node {id} should have Y");
        }
    }
}
