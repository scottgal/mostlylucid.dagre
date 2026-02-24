using Mostlylucid.Dagre;

namespace Mostlylucid.Dagre.Tests;

public class DagreInputGraphTests
{
    [Fact]
    public void AddNode_WithTag_ReturnsNodeWithTag()
    {
        var graph = new DagreInputGraph();
        var node = graph.AddNode(tag: "TestNode", width: 100, height: 50);

        Assert.Equal("TestNode", node.Tag);
        Assert.Equal(100, node.Width);
        Assert.Equal(50, node.Height);
    }

    [Fact]
    public void AddNode_WithoutDimensions_UsesDefaults()
    {
        var graph = new DagreInputGraph();
        var node = graph.AddNode(tag: "Default");

        Assert.Equal(300, node.Width);
        Assert.Equal(100, node.Height);
    }

    [Fact]
    public void AddNode_Duplicate_ThrowsException()
    {
        var graph = new DagreInputGraph();
        var node = graph.AddNode(tag: "A");

        Assert.Throws<DagreException>(() => graph.AddNode(node));
    }

    [Fact]
    public void AddEdge_CreatesParentChildRelationship()
    {
        var graph = new DagreInputGraph();
        var a = graph.AddNode(tag: "A");
        var b = graph.AddNode(tag: "B");

        graph.AddEdge(a, b);

        Assert.Contains(a, b.Parents);
        Assert.Contains(b, a.Childs);
    }

    [Fact]
    public void AddEdge_DuplicateEdge_HandlesBasedOnFlag()
    {
        var graph = new DagreInputGraph();
        var a = graph.AddNode(tag: "A");
        var b = graph.AddNode(tag: "B");

        graph.AddEdge(a, b);

        DagreInputGraph.ExceptionOnDuplicateEdge = true;
        try
        {
            Assert.Throws<DagreException>(() => graph.AddEdge(a, b));
        }
        finally
        {
            DagreInputGraph.ExceptionOnDuplicateEdge = false;
        }
    }

    [Fact]
    public void AddEdge_ReverseDuplicate_ThrowsByDefault()
    {
        var graph = new DagreInputGraph();
        var a = graph.AddNode(tag: "A");
        var b = graph.AddNode(tag: "B");

        graph.AddEdge(a, b);

        Assert.Throws<DagreException>(() => graph.AddEdge(b, a));
    }

    [Fact]
    public void GetNode_ReturnsCorrectNode()
    {
        var graph = new DagreInputGraph();
        var a = graph.AddNode(tag: "A");
        graph.AddNode(tag: "B");

        var found = graph.GetNode("A");

        Assert.Same(a, found);
    }

    [Fact]
    public void GetEdge_ReturnsCorrectEdge()
    {
        var graph = new DagreInputGraph();
        var a = graph.AddNode(tag: "A");
        var b = graph.AddNode(tag: "B");
        var edge = graph.AddEdge(a, b);

        var found = graph.GetEdge(a, b);

        Assert.Same(edge, found);
    }

    [Fact]
    public void Nodes_ReturnsAllNodes()
    {
        var graph = new DagreInputGraph();
        graph.AddNode(tag: "A");
        graph.AddNode(tag: "B");
        graph.AddNode(tag: "C");

        var nodes = graph.Nodes();

        Assert.Equal(3, nodes.Length);
    }

    [Fact]
    public void Edges_ReturnsAllEdges()
    {
        var graph = new DagreInputGraph();
        var a = graph.AddNode(tag: "A");
        var b = graph.AddNode(tag: "B");
        var c = graph.AddNode(tag: "C");

        graph.AddEdge(a, b);
        graph.AddEdge(b, c);

        var edges = graph.Edges();

        Assert.Equal(2, edges.Length);
    }

    [Fact]
    public void AddGroup_CreatesGroupNode()
    {
        var graph = new DagreInputGraph();
        var group = graph.AddGroup(tag: "Group1");

        Assert.IsType<DagreInputGroup>(group);
        Assert.Equal("Group1", group.Tag);
    }

    [Fact]
    public void SetGroup_AssignsNodeToGroup()
    {
        var graph = new DagreInputGraph();
        var group = graph.AddGroup(tag: "G");
        var node = graph.AddNode(tag: "N");

        graph.SetGroup(node, group);

        Assert.Same(group, node.Group);
    }

    [Fact]
    public void Layout_SimpleGraph_AssignsCoordinates()
    {
        var graph = new DagreInputGraph();
        var a = graph.AddNode(tag: "A", width: 100, height: 40);
        var b = graph.AddNode(tag: "B", width: 100, height: 40);
        var c = graph.AddNode(tag: "C", width: 100, height: 40);

        graph.AddEdge(a, b);
        graph.AddEdge(b, c);

        graph.Layout();

        // Chain: A -> B -> C, so Y should increase
        Assert.True(a.Y < b.Y, $"A.Y ({a.Y}) should be < B.Y ({b.Y})");
        Assert.True(b.Y < c.Y, $"B.Y ({b.Y}) should be < C.Y ({c.Y})");
    }

    [Fact]
    public void Layout_DiamondGraph_AssignsCoordinates()
    {
        var graph = new DagreInputGraph();
        var a = graph.AddNode(tag: "A", width: 80, height: 40);
        var b = graph.AddNode(tag: "B", width: 80, height: 40);
        var c = graph.AddNode(tag: "C", width: 80, height: 40);
        var d = graph.AddNode(tag: "D", width: 80, height: 40);

        graph.AddEdge(a, b);
        graph.AddEdge(a, c);
        graph.AddEdge(b, d);
        graph.AddEdge(c, d);

        graph.Layout();

        // A at top, D at bottom
        Assert.True(a.Y < d.Y);
        // B and C on same rank
        Assert.Equal(b.Y, c.Y, 1.0);
    }

    [Fact]
    public void Layout_VerticalVsHorizontal_RespectsDirection()
    {
        var graphV = new DagreInputGraph { VerticalLayout = true };
        var a = graphV.AddNode(tag: "A", width: 100, height: 40);
        var b = graphV.AddNode(tag: "B", width: 100, height: 40);
        graphV.AddEdge(a, b);
        graphV.Layout();

        var graphH = new DagreInputGraph { VerticalLayout = false };
        var a2 = graphH.AddNode(tag: "A", width: 100, height: 40);
        var b2 = graphH.AddNode(tag: "B", width: 100, height: 40);
        graphH.AddEdge(a2, b2);
        graphH.Layout();

        // Vertical: A above B (Y increases)
        Assert.True(a.Y < b.Y, "Vertical: A should be above B");
        // Horizontal: A left of B (X increases)
        Assert.True(a2.X < b2.X, "Horizontal: A should be left of B");
    }

    [Fact]
    public void Layout_WithProgress_ReceivesProgressUpdates()
    {
        var graph = new DagreInputGraph();
        var a = graph.AddNode(tag: "A");
        var b = graph.AddNode(tag: "B");
        graph.AddEdge(a, b);

        var progressCount = 0;
        float lastProgress = 0;

        graph.Layout(p =>
        {
            progressCount++;
            lastProgress = p.MainProgress;
        });

        Assert.True(progressCount > 0);
        Assert.Equal(1, lastProgress);
    }

    [Fact]
    public void Layout_SetsEdgePoints()
    {
        var graph = new DagreInputGraph();
        var a = graph.AddNode(tag: "A", width: 100, height: 40);
        var b = graph.AddNode(tag: "B", width: 100, height: 40);
        var edge = graph.AddEdge(a, b);

        graph.Layout();

        Assert.NotNull(edge.Points);
        Assert.True(edge.Points.Length >= 2);
    }
}
