using Mostlylucid.Dagre;

namespace Mostlylucid.Dagre.Tests;

public class GraphLayoutApiTests
{
    [Fact]
    public void AddNode_ReturnsThisForChaining()
    {
        var layout = new GraphLayout();
        var result = layout.AddNode("a", 100, 50);
        Assert.Same(layout, result);
    }

    [Fact]
    public void AddEdge_ReturnsThisForChaining()
    {
        var layout = new GraphLayout();
        layout.AddNode("a", 100, 50);
        layout.AddNode("b", 100, 50);
        var result = layout.AddEdge("a", "b");
        Assert.Same(layout, result);
    }

    [Fact]
    public void Run_ReturnsLayoutResultData()
    {
        var layout = new GraphLayout();
        layout.AddNode("a", 100, 50);
        layout.AddNode("b", 100, 50);
        layout.AddEdge("a", "b");

        var result = layout.Run();

        Assert.NotNull(result);
        Assert.NotNull(result.Nodes);
        Assert.NotNull(result.Edges);
    }

    [Fact]
    public void Run_SetsNodeCoordinates()
    {
        var layout = new GraphLayout();
        layout.AddNode("a", 100, 50);
        layout.AddNode("b", 100, 50);
        layout.AddEdge("a", "b");

        var result = layout.Run();

        Assert.True(result.Nodes["a"].X > 0);
        Assert.True(result.Nodes["a"].Y > 0);
        Assert.True(result.Nodes["b"].X > 0);
        Assert.True(result.Nodes["b"].Y > 0);
    }

    [Fact]
    public void Run_SetsEdgePoints()
    {
        var layout = new GraphLayout();
        layout.AddNode("a", 100, 50);
        layout.AddNode("b", 100, 50);
        layout.AddEdge("a", "b");

        var result = layout.Run();

        Assert.Single(result.Edges);
        Assert.NotNull(result.Edges[0].Points);
        Assert.True(result.Edges[0].Points.Count >= 2);
    }

    [Fact]
    public void Run_TopToBottom_ProducesVerticalLayout()
    {
        var layout = new GraphLayout(new GraphLayoutOptions
        {
            Direction = LayoutDirection.TopToBottom,
            RankSeparation = 50,
            NodeSeparation = 50
        });

        layout.AddNode("a", 80, 40);
        layout.AddNode("b", 80, 40);
        layout.AddEdge("a", "b");

        var result = layout.Run();

        Assert.True(result.Nodes["a"].Y < result.Nodes["b"].Y);
    }

    [Fact]
    public void Run_LeftToRight_ProducesHorizontalLayout()
    {
        var layout = new GraphLayout(new GraphLayoutOptions
        {
            Direction = LayoutDirection.LeftToRight,
            RankSeparation = 50,
            NodeSeparation = 50
        });

        layout.AddNode("a", 80, 40);
        layout.AddNode("b", 80, 40);
        layout.AddEdge("a", "b");

        var result = layout.Run();

        Assert.True(result.Nodes["a"].X < result.Nodes["b"].X);
    }

    [Fact]
    public void Run_BottomToTop_ProducesReversedVerticalLayout()
    {
        var layout = new GraphLayout(new GraphLayoutOptions
        {
            Direction = LayoutDirection.BottomToTop,
            RankSeparation = 50,
            NodeSeparation = 50
        });

        layout.AddNode("a", 80, 40);
        layout.AddNode("b", 80, 40);
        layout.AddEdge("a", "b");

        var result = layout.Run();

        Assert.True(result.Nodes["a"].Y > result.Nodes["b"].Y);
    }

    [Fact]
    public void Run_RightToLeft_ProducesReversedHorizontalLayout()
    {
        var layout = new GraphLayout(new GraphLayoutOptions
        {
            Direction = LayoutDirection.RightToLeft,
            RankSeparation = 50,
            NodeSeparation = 50
        });

        layout.AddNode("a", 80, 40);
        layout.AddNode("b", 80, 40);
        layout.AddEdge("a", "b");

        var result = layout.Run();

        Assert.True(result.Nodes["a"].X > result.Nodes["b"].X);
    }

    [Fact]
    public void Run_WithUserData_PreservesUserData()
    {
        var layout = new GraphLayout();
        var userData = new { Name = "TestNode", Id = 42 };

        layout.AddNode("a", 100, 50, userData);
        layout.AddNode("b", 100, 50);
        layout.AddEdge("a", "b", userData: new { Label = "connection" });

        var result = layout.Run();

        Assert.NotNull(result.Nodes["a"].UserData);
        Assert.Equal("TestNode", ((dynamic)result.Nodes["a"].UserData).Name);
        Assert.Equal(42, ((dynamic)result.Nodes["a"].UserData).Id);
        Assert.Equal("connection", ((dynamic)result.Edges[0].UserData).Label);
    }

    [Fact]
    public void Run_WithEdgeOptions_AppliesOptions()
    {
        var layout = new GraphLayout();
        layout.AddNode("a", 100, 50);
        layout.AddNode("b", 100, 50);
        layout.AddEdge("a", "b", new EdgeOptions
        {
            MinLength = 2,
            Weight = 5,
            LabelWidth = 30,
            LabelHeight = 15,
            LabelOffset = 12,
            LabelPosition = LabelPosition.Center
        });

        var result = layout.Run();

        Assert.Single(result.Edges);
    }

    [Fact]
    public void Run_ComplexGraph_ProducesValidLayout()
    {
        var layout = new GraphLayout(new GraphLayoutOptions
        {
            Direction = LayoutDirection.TopToBottom,
            RankSeparation = 40,
            NodeSeparation = 40,
            EdgeSeparation = 15
        });

        layout.AddNode("start", 80, 40);
        layout.AddNode("proc1", 100, 50);
        layout.AddNode("proc2", 100, 50);
        layout.AddNode("decision", 80, 80);
        layout.AddNode("end", 80, 40);

        layout.AddEdge("start", "proc1");
        layout.AddEdge("proc1", "proc2");
        layout.AddEdge("proc2", "decision");
        layout.AddEdge("decision", "end");
        layout.AddEdge("decision", "proc1", new EdgeOptions { MinLength = 1 });

        var result = layout.Run();

        Assert.Equal(5, result.Nodes.Count);
        Assert.Equal(5, result.Edges.Count);

        foreach (var node in result.Nodes.Values)
        {
            Assert.True(node.X >= 0);
            Assert.True(node.Y >= 0);
        }

        foreach (var edge in result.Edges)
            Assert.NotNull(edge.Points);
    }

    [Fact]
    public void Run_SingleNode_ProducesValidLayout()
    {
        var layout = new GraphLayout();
        layout.AddNode("alone", 100, 50);

        var result = layout.Run();

        Assert.Single(result.Nodes);
        Assert.Empty(result.Edges);
        Assert.True(result.Nodes["alone"].X > 0);
        Assert.True(result.Nodes["alone"].Y > 0);
    }

    [Fact]
    public void Run_MultipleDisconnectedComponents_HandlesCorrectly()
    {
        var layout = new GraphLayout();
        layout.AddNode("a1", 50, 30);
        layout.AddNode("a2", 50, 30);
        layout.AddEdge("a1", "a2");

        layout.AddNode("b1", 50, 30);
        layout.AddNode("b2", 50, 30);
        layout.AddEdge("b1", "b2");

        var result = layout.Run();

        Assert.Equal(4, result.Nodes.Count);
        Assert.Equal(2, result.Edges.Count);
    }
}
