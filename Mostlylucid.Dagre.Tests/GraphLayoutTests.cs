namespace Mostlylucid.Dagre.Tests;

/// <summary>
///     Tests for the public GraphLayout API using the original DagreLayout engine.
/// </summary>
public class GraphLayoutTests
{
    [Fact]
    public void SingleNode_HasPosition()
    {
        var layout = new GraphLayout();
        layout.AddNode("A", 80, 40);
        var result = layout.Run();

        Assert.Single(result.Nodes);
        Assert.True(result.Nodes.ContainsKey("A"));
        Assert.Equal(80, result.Nodes["A"].Width);
        Assert.Equal(40, result.Nodes["A"].Height);
    }

    [Fact]
    public void TwoNodes_OneEdge_NodesOnDifferentRanks()
    {
        var layout = new GraphLayout();
        layout.AddNode("A", 80, 40);
        layout.AddNode("B", 80, 40);
        layout.AddEdge("A", "B");
        var result = layout.Run();

        Assert.Equal(2, result.Nodes.Count);
        Assert.Single(result.Edges);

        // A should be above B (lower Y) in top-to-bottom layout
        Assert.True(result.Nodes["A"].Y < result.Nodes["B"].Y,
            $"Expected A.Y ({result.Nodes["A"].Y}) < B.Y ({result.Nodes["B"].Y})");
    }

    [Fact]
    public void LeftToRight_NodesArrangedHorizontally()
    {
        var layout = new GraphLayout(new GraphLayoutOptions
        {
            Direction = LayoutDirection.LeftToRight
        });
        layout.AddNode("A", 80, 40);
        layout.AddNode("B", 80, 40);
        layout.AddEdge("A", "B");
        var result = layout.Run();

        // A should be to the left of B
        Assert.True(result.Nodes["A"].X < result.Nodes["B"].X,
            $"Expected A.X ({result.Nodes["A"].X}) < B.X ({result.Nodes["B"].X})");
    }

    [Fact]
    public void Chain_NodesOrderedByRank()
    {
        var layout = new GraphLayout();
        layout.AddNode("A", 80, 40);
        layout.AddNode("B", 80, 40);
        layout.AddNode("C", 80, 40);
        layout.AddEdge("A", "B");
        layout.AddEdge("B", "C");
        var result = layout.Run();

        Assert.True(result.Nodes["A"].Y < result.Nodes["B"].Y);
        Assert.True(result.Nodes["B"].Y < result.Nodes["C"].Y);
    }

    [Fact]
    public void Diamond_AllNodesPositioned()
    {
        var layout = new GraphLayout();
        layout.AddNode("A", 80, 40);
        layout.AddNode("B", 80, 40);
        layout.AddNode("C", 80, 40);
        layout.AddNode("D", 80, 40);
        layout.AddEdge("A", "B");
        layout.AddEdge("A", "C");
        layout.AddEdge("B", "D");
        layout.AddEdge("C", "D");
        var result = layout.Run();

        Assert.Equal(4, result.Nodes.Count);
        Assert.Equal(4, result.Edges.Count);

        // A at top, D at bottom
        Assert.True(result.Nodes["A"].Y < result.Nodes["D"].Y);
        // B and C on same rank (same Y)
        Assert.Equal(result.Nodes["B"].Y, result.Nodes["C"].Y, 1.0);
    }

    [Fact]
    public void EdgesHavePoints()
    {
        var layout = new GraphLayout();
        layout.AddNode("A", 80, 40);
        layout.AddNode("B", 80, 40);
        layout.AddEdge("A", "B");
        var result = layout.Run();

        var edge = Assert.Single(result.Edges);
        Assert.NotEmpty(edge.Points);
        Assert.Equal("A", edge.Source);
        Assert.Equal("B", edge.Target);
    }

    [Fact]
    public void UserData_PreservedOnNodesAndEdges()
    {
        var layout = new GraphLayout();
        layout.AddNode("A", 80, 40, userData: "nodeDataA");
        layout.AddNode("B", 80, 40, userData: 42);
        layout.AddEdge("A", "B", userData: "edgeData");
        var result = layout.Run();

        Assert.Equal("nodeDataA", result.Nodes["A"].UserData);
        Assert.Equal(42, result.Nodes["B"].UserData);
        Assert.Equal("edgeData", result.Edges[0].UserData);
    }

    [Fact]
    public void EdgeOptions_MinLength()
    {
        var layout = new GraphLayout();
        layout.AddNode("A", 80, 40);
        layout.AddNode("B", 80, 40);
        layout.AddEdge("A", "B", new EdgeOptions { MinLength = 3 });
        var result = layout.Run();

        // With minlen=3, the gap should be larger than default
        var gap = result.Nodes["B"].Y - result.Nodes["A"].Y;
        Assert.True(gap > 100, $"Expected large gap with minlen=3, got {gap}");
    }

    [Fact]
    public void FanOut_MultipleTargets()
    {
        var layout = new GraphLayout();
        layout.AddNode("root", 80, 40);
        for (var i = 0; i < 5; i++)
        {
            var id = $"child{i}";
            layout.AddNode(id, 80, 40);
            layout.AddEdge("root", id);
        }

        var result = layout.Run();
        Assert.Equal(6, result.Nodes.Count);
        Assert.Equal(5, result.Edges.Count);
    }

    [Theory]
    [InlineData(10, 12)]
    [InlineData(50, 60)]
    [InlineData(100, 150)]
    public void ScaledGraphs_CompleteWithoutError(int nodeCount, int edgeCount)
    {
        var rng = new Random(42);
        var layout = new GraphLayout();

        for (var i = 0; i < nodeCount; i++)
            layout.AddNode(i.ToString(), 80, 40);

        var added = new HashSet<(int, int)>();
        var edgesAdded = 0;
        while (edgesAdded < edgeCount)
        {
            var src = rng.Next(nodeCount - 1);
            var tgt = rng.Next(src + 1, nodeCount);
            if (added.Add((src, tgt)))
            {
                layout.AddEdge(src.ToString(), tgt.ToString());
                edgesAdded++;
            }
        }

        var result = layout.Run();
        Assert.Equal(nodeCount, result.Nodes.Count);
        Assert.Equal(edgeCount, result.Edges.Count);

        // All nodes should have finite positions
        foreach (var (id, node) in result.Nodes)
        {
            Assert.False(float.IsNaN(node.X), $"Node {id} has NaN X");
            Assert.False(float.IsNaN(node.Y), $"Node {id} has NaN Y");
        }
    }

    [Fact]
    public void AllDirections_ProduceValidLayout()
    {
        foreach (var dir in Enum.GetValues<LayoutDirection>())
        {
            var layout = new GraphLayout(new GraphLayoutOptions { Direction = dir });
            layout.AddNode("A", 80, 40);
            layout.AddNode("B", 80, 40);
            layout.AddEdge("A", "B");
            var result = layout.Run();

            Assert.Equal(2, result.Nodes.Count);
            Assert.Single(result.Edges);
        }
    }

    [Fact]
    public void NoEdges_NodesStillPositioned()
    {
        var layout = new GraphLayout();
        layout.AddNode("A", 80, 40);
        layout.AddNode("B", 80, 40);
        layout.AddNode("C", 80, 40);
        var result = layout.Run();

        Assert.Equal(3, result.Nodes.Count);
        Assert.Empty(result.Edges);
    }

    [Fact]
    public void CyclicEdges_HandledGracefully()
    {
        var layout = new GraphLayout();
        layout.AddNode("A", 80, 40);
        layout.AddNode("B", 80, 40);
        layout.AddNode("C", 80, 40);
        layout.AddEdge("A", "B");
        layout.AddEdge("B", "C");
        layout.AddEdge("C", "A"); // cycle

        var result = layout.Run();
        Assert.Equal(3, result.Nodes.Count);
        Assert.Equal(3, result.Edges.Count);
    }

    [Fact]
    public void SelfLoop_HandledGracefully()
    {
        var layout = new GraphLayout();
        layout.AddNode("A", 80, 40);
        layout.AddNode("B", 80, 40);
        layout.AddEdge("A", "B");
        layout.AddEdge("A", "A"); // self-loop

        var result = layout.Run();
        Assert.Equal(2, result.Nodes.Count);
    }

    [Fact]
    public void DifferentNodeSizes_LayoutsCorrectly()
    {
        var layout = new GraphLayout();
        layout.AddNode("small", 40, 20);
        layout.AddNode("large", 200, 100);
        layout.AddEdge("small", "large");
        var result = layout.Run();

        Assert.Equal(40, result.Nodes["small"].Width);
        Assert.Equal(200, result.Nodes["large"].Width);
    }

    [Fact]
    public void FluentApi_ChainingWorks()
    {
        var result = new GraphLayout()
            .AddNode("A", 80, 40)
            .AddNode("B", 80, 40)
            .AddEdge("A", "B")
            .Run();

        Assert.Equal(2, result.Nodes.Count);
    }
}
