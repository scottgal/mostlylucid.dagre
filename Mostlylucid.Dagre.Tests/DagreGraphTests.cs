using Mostlylucid.Dagre;

namespace Mostlylucid.Dagre.Tests;

public class DagreGraphTests
{
    [Fact]
    public void Constructor_CreatesEmptyGraph()
    {
        var graph = new DagreGraph(true);

        Assert.Equal(0, graph.NodeCount());
        Assert.Empty(graph.Nodes());
        Assert.Empty(graph.Edges());
    }

    [Fact]
    public void SetNode_CreatesNode()
    {
        var graph = new DagreGraph(true);

        graph.SetNode("a");

        Assert.True(graph.HasNode("a"));
        Assert.Equal(1, graph.NodeCount());
    }

    [Fact]
    public void SetNode_WithLabel_SetsLabel()
    {
        var graph = new DagreGraph(true);
        var label = new NodeLabel { Width = 100, Height = 50 };

        graph.SetNode("a", label);

        var node = graph.Node("a");
        Assert.Equal(100, node.Width);
        Assert.Equal(50, node.Height);
    }

    [Fact]
    public void SetNode_ExistingNode_UpdatesLabel()
    {
        var graph = new DagreGraph(true);
        graph.SetNode("a", new NodeLabel { Width = 50 });

        graph.SetNode("a", new NodeLabel { Width = 100 });

        Assert.Equal(100, graph.Node("a").Width);
        Assert.Equal(1, graph.NodeCount());
    }

    [Fact]
    public void RemoveNode_RemovesNodeAndEdges()
    {
        var graph = new DagreGraph(true);
        graph.SetNode("a");
        graph.SetNode("b");
        graph.SetEdge("a", "b");

        graph.RemoveNode("a");

        Assert.False(graph.HasNode("a"));
        Assert.Equal(1, graph.NodeCount());
        Assert.Empty(graph.Edges());
    }

    [Fact]
    public void SetEdge_CreatesNodesAndEdge()
    {
        var graph = new DagreGraph(true);

        graph.SetEdge("a", "b");

        Assert.True(graph.HasNode("a"));
        Assert.True(graph.HasNode("b"));
        Assert.Single(graph.Edges());
    }

    [Fact]
    public void SetEdge_WithLabel_SetsLabel()
    {
        var graph = new DagreGraph(true);
        var label = new EdgeLabel { Weight = 5, Minlen = 2 };

        graph.SetEdge("a", "b", label);

        var edge = graph.Edge("a", "b");
        Assert.Equal(5, edge.Weight);
        Assert.Equal(2, edge.Minlen);
    }

    [Fact]
    public void SetEdge_Duplicate_ReplacesLabel()
    {
        var graph = new DagreGraph(true);
        graph.SetEdge("a", "b", new EdgeLabel { Weight = 1 });

        graph.SetEdge("a", "b", new EdgeLabel { Weight = 10 });

        Assert.Equal(10, graph.Edge("a", "b").Weight);
    }

    [Fact]
    public void SetEdge_NamedEdge_WithMultigraph_Throws()
    {
        var graph = new DagreGraph(true) { _isMultigraph = false };

        Assert.Throws<DagreException>(() => graph.SetEdge("a", "b", new EdgeLabel(), "name"));
    }

    [Fact]
    public void Predecessors_ReturnsCorrectNodes()
    {
        var graph = new DagreGraph(true);
        graph.SetEdge("a", "b");
        graph.SetEdge("c", "b");

        var preds = graph.Predecessors("b");

        Assert.Equal(2, preds.Length);
        Assert.Contains("a", preds);
        Assert.Contains("c", preds);
    }

    [Fact]
    public void Successors_ReturnsCorrectNodes()
    {
        var graph = new DagreGraph(true);
        graph.SetEdge("a", "b");
        graph.SetEdge("a", "c");

        var sucs = graph.Successors("a");

        Assert.Equal(2, sucs.Length);
        Assert.Contains("b", sucs);
        Assert.Contains("c", sucs);
    }

    [Fact]
    public void Graph_ReturnsGraphLabel()
    {
        var graph = new DagreGraph(true);
        var label = new GraphLabel { RankSep = 100, NodeSep = 50 };

        graph.SetGraph(label);

        Assert.Equal(100, graph.Graph().RankSep);
        Assert.Equal(50, graph.Graph().NodeSep);
    }

    [Fact]
    public void BeginBatch_EndBatch_SuppressesCacheInvalidation()
    {
        var graph = new DagreGraph(true);

        graph.BeginBatch();
        graph.SetNode("a");
        graph.SetNode("b");
        graph.EndBatch();

        Assert.Equal(2, graph.Nodes().Length);
    }

    [Fact]
    public void EdgeArgsToId_ProducesConsistentId()
    {
        var id1 = DagreGraph.EdgeArgsToId(true, "a", "b", null);
        var id2 = DagreGraph.EdgeArgsToId(true, "a", "b", null);

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void EdgeArgsToId_DifferentNames_ProduceDifferentIds()
    {
        var id1 = DagreGraph.EdgeArgsToId(true, "a", "b", "name1");
        var id2 = DagreGraph.EdgeArgsToId(true, "a", "b", "name2");

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Edge_ByEdgeIndex_ReturnsLabel()
    {
        var graph = new DagreGraph(true);
        graph.SetEdge("a", "b", new EdgeLabel { Weight = 7 });

        var edges = graph.Edges();
        var label = graph.Edge(edges[0]);

        Assert.Equal(7, label.Weight);
    }
}
