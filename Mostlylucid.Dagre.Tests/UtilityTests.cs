using Mostlylucid.Dagre;

namespace Mostlylucid.Dagre.Tests;

public class UtilityTests
{
    [Fact]
    public void UniqueId_ReturnsDifferentValues()
    {
        Util.UniqueCounter = 0;
        
        var id1 = Util.UniqueId("node");
        var id2 = Util.UniqueId("node");
        
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void UniqueId_ContainsPrefix()
    {
        var id = Util.UniqueId("test");
        
        Assert.StartsWith("test", id);
    }

    [Fact]
    public void Range_ReturnsCorrectSequence()
    {
        var range = Util.Range(1, 5);
        
        Assert.Equal(new[] { 1, 2, 3, 4 }, range);
    }

    [Fact]
    public void Range_EmptyRange_ReturnsEmptyArray()
    {
        var range = Util.Range(5, 5);
        
        Assert.Empty(range);
    }

    [Fact]
    public void Range_NegativeRange_ReturnsEmptyArray()
    {
        var range = Util.Range(5, 3);
        
        Assert.Empty(range);
    }

    [Fact]
    public void Range_WithStep_ReturnsCorrectSequence()
    {
        var range = Util.Range(0, 10, 2);
        
        Assert.Equal(new[] { 0, 2, 4, 6, 8 }, range);
    }

    [Fact]
    public void MaxRank_ReturnsHighestRank()
    {
        var g = new DagreGraph(true);
        g.SetNode("a", new NodeLabel { Rank = 0 });
        g.SetNode("b", new NodeLabel { Rank = 2 });
        g.SetNode("c", new NodeLabel { Rank = 1 });
        
        var maxRank = Util.MaxRank(g);
        
        Assert.Equal(2, maxRank);
    }

    [Fact]
    public void NormalizeRanks_ShiftsToZeroBased()
    {
        var g = new DagreGraph(true);
        g.SetNode("a", new NodeLabel { Rank = 5 });
        g.SetNode("b", new NodeLabel { Rank = 7 });
        g.SetNode("c", new NodeLabel { Rank = 6 });
        
        Util.NormalizeRanks(g);
        
        Assert.Equal(0, g.Node("a").Rank);
        Assert.Equal(2, g.Node("b").Rank);
        Assert.Equal(1, g.Node("c").Rank);
    }

    [Fact]
    public void BuildLayerMatrix_GroupsByRank()
    {
        var g = new DagreGraph(true);
        g.SetNode("a", new NodeLabel { Rank = 0, Order = 0 });
        g.SetNode("b", new NodeLabel { Rank = 0, Order = 1 });
        g.SetNode("c", new NodeLabel { Rank = 1, Order = 0 });
        
        var layers = Util.BuildLayerMatrix(g);
        
        Assert.Equal(2, layers.Count);
        Assert.Equal(2, layers[0].Length);
        Assert.Single(layers[1]);
    }

    [Fact]
    public void BuildLayerMatrix_SortsByOrder()
    {
        var g = new DagreGraph(true);
        g.SetNode("a", new NodeLabel { Rank = 0, Order = 2 });
        g.SetNode("b", new NodeLabel { Rank = 0, Order = 0 });
        g.SetNode("c", new NodeLabel { Rank = 0, Order = 1 });
        
        var layers = Util.BuildLayerMatrix(g);
        
        Assert.Equal(new[] { "b", "c", "a" }, layers[0]);
    }

    [Fact]
    public void IntersectRect_TopIntersection_ReturnsCorrectPoint()
    {
        var rect = new NodeLabel { X = 50, Y = 50, Width = 40, Height = 40 };
        var point = new DagrePoint(50, 0);
        
        var intersection = Util.IntersectRect(rect, point);
        
        Assert.True(Math.Abs(intersection.Y - 30) < 0.01);
    }

    [Fact]
    public void IntersectRect_RightIntersection_ReturnsCorrectPoint()
    {
        var rect = new NodeLabel { X = 50, Y = 50, Width = 40, Height = 40 };
        var point = new DagrePoint(100, 50);
        
        var intersection = Util.IntersectRect(rect, point);
        
        Assert.True(Math.Abs(intersection.X - 70) < 0.01);
    }

    [Fact]
    public void IntersectRect_InsideRect_Throws()
    {
        var rect = new NodeLabel { X = 50, Y = 50, Width = 40, Height = 40 };
        var point = new DagrePoint(50, 50);
        
        Assert.Throws<DagreException>(() => Util.IntersectRect(rect, point));
    }

    [Fact]
    public void Simplify_MergesMultiEdges()
    {
        var g = new DagreGraph(true) { _isMultigraph = true };
        g.SetNode("a", new NodeLabel());
        g.SetNode("b", new NodeLabel());
        g.SetEdge("a", "b", new EdgeLabel { Weight = 2 }, "e1");
        g.SetEdge("a", "b", new EdgeLabel { Weight = 3 }, "e2");
        
        var simplified = Util.Simplify(g);
        
        Assert.Single(simplified.Edges());
        Assert.Equal(5, simplified.Edge("a", "b").Weight);
    }

    [Fact]
    public void Simplify_TakesMaxMinlen()
    {
        var g = new DagreGraph(true) { _isMultigraph = true };
        g.SetNode("a", new NodeLabel());
        g.SetNode("b", new NodeLabel());
        g.SetEdge("a", "b", new EdgeLabel { Weight = 1, Minlen = 1 }, "e1");
        g.SetEdge("a", "b", new EdgeLabel { Weight = 1, Minlen = 3 }, "e2");
        
        var simplified = Util.Simplify(g);
        
        Assert.Equal(3, simplified.Edge("a", "b").Minlen);
    }

    [Fact]
    public void AsNonCompoundGraph_RemovesCompoundNodes()
    {
        var g = new DagreGraph(true) { _isCompound = true };
        g.SetNode("a", new NodeLabel { Width = 50 });
        g.SetNode("b", new NodeLabel { Width = 50 });
        g.SetNode("group", new NodeLabel());
        g.SetParent("a", "group");
        g.SetEdge("a", "b", new EdgeLabel());
        
        var nonCompound = Util.AsNonCompoundGraph(g);
        
        Assert.False(nonCompound._isCompound);
        Assert.Equal(2, nonCompound.NodeCount());
    }

    [Fact]
    public void AddDummyNode_CreatesNodeWithDummyType()
    {
        var g = new DagreGraph(true);
        g.SetNode("existing", new NodeLabel());
        
        var dummyId = Util.AddDummyNode(g, "edge", new NodeLabel { Rank = 5 }, "_d");
        
        Assert.True(g.HasNode(dummyId));
        Assert.StartsWith("_d", dummyId);
        Assert.Equal("edge", g.Node(dummyId).Dummy);
        Assert.Equal(5, g.Node(dummyId).Rank);
    }
}
