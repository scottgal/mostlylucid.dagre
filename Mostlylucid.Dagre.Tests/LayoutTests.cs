using Mostlylucid.Dagre;
using Mostlylucid.Dagre.Indexed;

namespace Mostlylucid.Dagre.Tests;

public class LayoutTests
{
    static EdgeLabel DefaultEdge() => new()
    {
        ["minlen"] = 1,
        ["weight"] = 1,
        ["width"] = 0f,
        ["height"] = 0f,
        ["labeloffset"] = 10,
        ["labelpos"] = "r"
    };

    static DagreGraph CreateSimpleChain(int count, string rankDir = "tb")
    {
        var g = new DagreGraph(true) { _isMultigraph = true };
        g.SetGraph(new GraphLabel { RankSep = 50, NodeSep = 50, EdgeSep = 20, RankDir = rankDir });

        for (var i = 0; i < count; i++)
        {
            g.SetNode(i.ToString(), new NodeLabel { ["width"] = 80f, ["height"] = 40f });
            if (i > 0)
                g.SetEdge((i - 1).ToString(), i.ToString(), DefaultEdge());
        }

        return g;
    }

    [Fact]
    public void RunLayout_SimpleChain_ProducesCorrectOrder()
    {
        var g = CreateSimpleChain(4);
        IndexedDagreLayout.RunLayout(g);

        var ys = Enumerable.Range(0, 4).Select(i => g.Node(i.ToString()).Y).ToList();
        for (var i = 0; i < 3; i++)
            Assert.True(ys[i] < ys[i + 1], $"Node {i} Y ({ys[i]}) should be < Node {i + 1} Y ({ys[i + 1]})");
    }

    [Fact]
    public void RunLayout_AssignsValidCoordinates()
    {
        var g = CreateSimpleChain(3);
        IndexedDagreLayout.RunLayout(g);

        foreach (var v in g.Nodes())
        {
            var node = g.Node(v);
            Assert.True(node.X > 0, $"Node {v} should have positive X");
            Assert.True(node.Y > 0, $"Node {v} should have positive Y");
        }
    }

    [Fact]
    public void RunLayout_EdgesHavePoints()
    {
        var g = CreateSimpleChain(2);
        IndexedDagreLayout.RunLayout(g);

        var edge = g.Edge("0", "1");
        Assert.NotNull(edge.Points);
        Assert.True(edge.Points.Count >= 2);
    }

    [Fact]
    public void RunLayout_PreservesNodeDimensions()
    {
        var g = new DagreGraph(true) { _isMultigraph = true };
        g.SetGraph(new GraphLabel { RankSep = 50, NodeSep = 50, EdgeSep = 20, RankDir = "tb" });
        g.SetNode("a", new NodeLabel { ["width"] = 150f, ["height"] = 75f });
        g.SetNode("b", new NodeLabel { ["width"] = 200f, ["height"] = 100f });
        g.SetEdge("a", "b", DefaultEdge());

        IndexedDagreLayout.RunLayout(g);

        Assert.Equal(150, g.Node("a").Width);
        Assert.Equal(75, g.Node("a").Height);
        Assert.Equal(200, g.Node("b").Width);
        Assert.Equal(100, g.Node("b").Height);
    }

    [Fact]
    public void RunLayout_TranslatesToPositiveCoordinates()
    {
        var g = new DagreGraph(true) { _isMultigraph = true };
        g.SetGraph(new GraphLabel { RankSep = 50, NodeSep = 50, EdgeSep = 20, RankDir = "tb", MarginX = 10, MarginY = 10 });
        g.SetNode("a", new NodeLabel { ["width"] = 100f, ["height"] = 40f });

        IndexedDagreLayout.RunLayout(g);

        Assert.True(g.Node("a").X >= 0);
        Assert.True(g.Node("a").Y >= 0);
    }

    [Fact]
    public void RunLayout_SetsGraphDimensions()
    {
        var g = CreateSimpleChain(2);
        IndexedDagreLayout.RunLayout(g);

        var graph = g.Graph();
        Assert.True(graph.Width > 0);
        Assert.True(graph.Height > 0);
    }

    [Fact]
    public void RunLayout_Diamond_ProducesCorrectLayout()
    {
        var g = new DagreGraph(true) { _isMultigraph = true };
        g.SetGraph(new GraphLabel { RankSep = 50, NodeSep = 50, EdgeSep = 20, RankDir = "tb" });
        g.SetNode("a", new NodeLabel { ["width"] = 80f, ["height"] = 40f });
        g.SetNode("b", new NodeLabel { ["width"] = 80f, ["height"] = 40f });
        g.SetNode("c", new NodeLabel { ["width"] = 80f, ["height"] = 40f });
        g.SetNode("d", new NodeLabel { ["width"] = 80f, ["height"] = 40f });
        g.SetEdge("a", "b", DefaultEdge());
        g.SetEdge("a", "c", DefaultEdge());
        g.SetEdge("b", "d", DefaultEdge());
        g.SetEdge("c", "d", DefaultEdge());

        IndexedDagreLayout.RunLayout(g);

        // A at top, D at bottom
        Assert.True(g.Node("a").Y < g.Node("d").Y);
        // B and C on same rank
        Assert.Equal(g.Node("b").Y, g.Node("c").Y, 1.0);
    }

    [Fact]
    public void RunLayout_SelfEdge_HandlesCorrectly()
    {
        var g = new DagreGraph(true) { _isMultigraph = true };
        g.SetGraph(new GraphLabel { RankSep = 50, NodeSep = 50, EdgeSep = 20, RankDir = "tb" });
        g.SetNode("a", new NodeLabel { ["width"] = 80f, ["height"] = 40f });
        g.SetNode("b", new NodeLabel { ["width"] = 80f, ["height"] = 40f });
        g.SetEdge("a", "b", DefaultEdge());
        g.SetEdge("a", "a", DefaultEdge());

        IndexedDagreLayout.RunLayout(g);

        Assert.True(g.Node("a").X > 0);
        Assert.True(g.Node("a").Y > 0);
    }

    [Fact]
    public void RunLayout_Cycle_BreaksCycle()
    {
        var g = new DagreGraph(true) { _isMultigraph = true };
        g.SetGraph(new GraphLabel { RankSep = 50, NodeSep = 50, EdgeSep = 20, RankDir = "tb" });
        g.SetNode("a", new NodeLabel { ["width"] = 80f, ["height"] = 40f });
        g.SetNode("b", new NodeLabel { ["width"] = 80f, ["height"] = 40f });
        g.SetNode("c", new NodeLabel { ["width"] = 80f, ["height"] = 40f });
        g.SetEdge("a", "b", DefaultEdge());
        g.SetEdge("b", "c", DefaultEdge());
        g.SetEdge("c", "a", DefaultEdge());

        IndexedDagreLayout.RunLayout(g);

        Assert.True(g.Node("a").Y >= 0);
        Assert.True(g.Node("b").Y >= 0);
        Assert.True(g.Node("c").Y >= 0);
    }

    [Fact]
    public void RunLayout_LeftToRight_ChangesOrientation()
    {
        var g = CreateSimpleChain(2, "lr");
        IndexedDagreLayout.RunLayout(g);

        // In LR layout, node 0 should be to the left of node 1
        Assert.True(g.Node("0").X < g.Node("1").X,
            $"LR: Node 0 X ({g.Node("0").X}) should be < Node 1 X ({g.Node("1").X})");
    }

    [Fact]
    public void MakeSpaceForEdgeLabels_HalvesRankSep()
    {
        var g = new DagreGraph(true) { _isMultigraph = true };
        g.SetGraph(new GraphLabel { RankSep = 100, RankDir = "tb" });
        g.SetNode("a", new NodeLabel { ["width"] = 80f, ["height"] = 40f });
        g.SetEdge("a", "b", DefaultEdge());

        var originalRankSep = g.Graph().RankSep;
        DagreLayout.MakeSpaceForEdgeLabels(g);

        Assert.Equal(originalRankSep / 2, g.Graph().RankSep);
    }

    [Fact]
    public void RemoveSelfEdges_MovesSelfEdgesToNode()
    {
        var g = new DagreGraph(true) { _isMultigraph = true };
        g.SetGraph(new GraphLabel { RankDir = "tb" });
        g.SetNode("a", new NodeLabel { ["width"] = 80f, ["height"] = 40f });
        g.SetEdge("a", "a", DefaultEdge());

        DagreLayout.RemoveSelfEdges(g);

        Assert.Empty(g.Edges());
        Assert.NotNull(g.Node("a").SelfEdges);
        Assert.Single(g.Node("a").SelfEdges);
    }

}
