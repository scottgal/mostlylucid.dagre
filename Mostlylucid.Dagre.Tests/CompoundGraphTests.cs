using Mostlylucid.Dagre;
using Mostlylucid.Dagre.Indexed;

namespace Mostlylucid.Dagre.Tests;

public class CompoundGraphTests
{
    [Fact]
    public void SetParent_SetsParentRelationship()
    {
        var graph = new DagreGraph(true);
        graph.SetNode("parent");
        graph.SetNode("child");

        graph.SetParent("child", "parent");

        Assert.Equal("parent", graph.Parent("child"));
    }

    [Fact]
    public void SetParent_ToRoot_ClearsParent()
    {
        var graph = new DagreGraph(true);
        graph.SetNode("parent");
        graph.SetNode("child");
        graph.SetParent("child", "parent");

        graph.SetParent("child");

        Assert.Null(graph.Parent("child"));
    }

    [Fact]
    public void SetParent_Cycle_Throws()
    {
        var graph = new DagreGraph(true);
        graph.SetNode("a");
        graph.SetNode("b");
        graph.SetParent("b", "a");

        Assert.Throws<DagreException>(() => graph.SetParent("a", "b"));
    }

    [Fact]
    public void SetParent_NonCompound_Throws()
    {
        var graph = new DagreGraph(false);
        graph.SetNode("a");
        graph.SetNode("b");

        Assert.Throws<DagreException>(() => graph.SetParent("b", "a"));
    }

    [Fact]
    public void Children_ReturnsChildNodes()
    {
        var graph = new DagreGraph(true);
        graph.SetNode("parent");
        graph.SetNode("child1");
        graph.SetNode("child2");
        graph.SetParent("child1", "parent");
        graph.SetParent("child2", "parent");

        var children = graph.Children("parent");

        Assert.Equal(2, children.Length);
        Assert.Contains("child1", children);
        Assert.Contains("child2", children);
    }

    [Fact]
    public void Children_RootNode_ReturnsTopLevelNodes()
    {
        var graph = new DagreGraph(true);
        graph.SetNode("a");
        graph.SetNode("b");
        graph.SetNode("parent");
        graph.SetParent("a", "parent");

        var rootChildren = graph.Children();

        // Root children should include "b" and "parent" (not "a", which is under "parent")
        Assert.Contains("b", rootChildren);
        Assert.Contains("parent", rootChildren);
        Assert.DoesNotContain("a", rootChildren);
    }

    [Fact]
    public void HasChildren_ReturnsCorrectValue()
    {
        var graph = new DagreGraph(true);
        graph.SetNode("parent");
        graph.SetNode("child");

        Assert.False(graph.HasChildren("parent"));

        graph.SetParent("child", "parent");

        Assert.True(graph.HasChildren("parent"));
    }

    [Fact]
    public void RemoveNode_RemovesFromParentChildren()
    {
        var graph = new DagreGraph(true);
        graph.SetNode("parent");
        graph.SetNode("child");
        graph.SetParent("child", "parent");

        graph.RemoveNode("child");

        Assert.False(graph.HasChildren("parent"));
    }

    [Fact]
    public void RemoveNode_WithChildren_ReparentsToRoot()
    {
        var graph = new DagreGraph(true);
        graph.SetNode("parent");
        graph.SetNode("child");
        graph.SetParent("child", "parent");

        graph.RemoveNode("parent");

        Assert.Null(graph.Parent("child"));
    }

    [Fact]
    public void Layout_CompoundGraph_ProducesValidLayout()
    {
        var g = new DagreGraph(true) { _isCompound = true, _isMultigraph = true };
        g.SetGraph(new GraphLabel { RankDir = "tb", RankSep = 50, NodeSep = 50, EdgeSep = 20 });

        g.SetNode("group", new NodeLabel());
        g.SetNode("a", new NodeLabel { ["width"] = 80f, ["height"] = 40f });
        g.SetNode("b", new NodeLabel { ["width"] = 80f, ["height"] = 40f });

        g.SetParent("a", "group");
        g.SetParent("b", "group");

        g.SetEdge("a", "b", new EdgeLabel
        {
            ["minlen"] = 1, ["weight"] = 1, ["width"] = 0f,
            ["height"] = 0f, ["labeloffset"] = 10, ["labelpos"] = "r"
        });

        IndexedDagreLayout.RunLayout(g);

        // Group should contain both nodes
        var group = g.Node("group");
        var a = g.Node("a");
        var b = g.Node("b");
        Assert.True(group.Width > 0, "Group should have positive width");
        Assert.True(group.Height > 0, "Group should have positive height");
        Assert.True(a.Y < b.Y, "A should be above B in TB layout");
    }

    [Fact]
    public void Layout_NestedGroups_ProducesValidLayout()
    {
        var g = new DagreGraph(true) { _isCompound = true, _isMultigraph = true };
        g.SetGraph(new GraphLabel { RankDir = "tb", RankSep = 50, NodeSep = 50, EdgeSep = 20 });

        g.SetNode("outer", new NodeLabel());
        g.SetNode("inner", new NodeLabel());
        g.SetNode("a", new NodeLabel { ["width"] = 50f, ["height"] = 30f });

        g.SetParent("inner", "outer");
        g.SetParent("a", "inner");

        IndexedDagreLayout.RunLayout(g);

        var outer = g.Node("outer");
        var inner = g.Node("inner");
        var a = g.Node("a");

        Assert.True(outer.Width > 0);
        Assert.True(inner.Width > 0);
        Assert.False(float.IsNaN(a.X));
        Assert.False(float.IsNaN(a.Y));
    }

    [Fact]
    public void Layout_EdgeBetweenGroups_ProducesValidLayout()
    {
        var g = new DagreGraph(true) { _isCompound = true, _isMultigraph = true };
        g.SetGraph(new GraphLabel { RankDir = "tb", RankSep = 30, NodeSep = 30, EdgeSep = 20 });

        g.SetNode("g1", new NodeLabel());
        g.SetNode("g2", new NodeLabel());
        g.SetNode("a", new NodeLabel { ["width"] = 60f, ["height"] = 30f });
        g.SetNode("b", new NodeLabel { ["width"] = 60f, ["height"] = 30f });

        g.SetParent("a", "g1");
        g.SetParent("b", "g2");
        g.SetEdge("a", "b", new EdgeLabel
        {
            ["minlen"] = 1, ["weight"] = 1, ["width"] = 0f,
            ["height"] = 0f, ["labeloffset"] = 10, ["labelpos"] = "r"
        });

        IndexedDagreLayout.RunLayout(g);

        var aNode = g.Node("a");
        var bNode = g.Node("b");

        Assert.True(aNode.Y < bNode.Y, "a should be above b in TB layout");
    }
}
