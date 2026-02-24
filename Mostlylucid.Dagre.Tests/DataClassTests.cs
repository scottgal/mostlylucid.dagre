using Mostlylucid.Dagre;

namespace Mostlylucid.Dagre.Tests;

public class GraphLabelTests
{
    [Fact]
    public void Properties_DefaultValues_AreCorrect()
    {
        var label = new GraphLabel();
        
        Assert.Equal(0, label.RankSep);
        Assert.Equal(0, label.EdgeSep);
        Assert.Equal(0, label.NodeSep);
        Assert.Null(label.RankDir);
        Assert.Null(label.Ranker);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var label = new GraphLabel
        {
            RankSep = 50,
            EdgeSep = 20,
            NodeSep = 30,
            RankDir = "TB",
            Ranker = "network-simplex",
            Acyclicer = "greedy",
            Align = "UL",
            MarginX = 10,
            MarginY = 15,
            Width = 500,
            Height = 300
        };
        
        Assert.Equal(50, label.RankSep);
        Assert.Equal(20, label.EdgeSep);
        Assert.Equal(30, label.NodeSep);
        Assert.Equal("TB", label.RankDir);
        Assert.Equal("network-simplex", label.Ranker);
        Assert.Equal("greedy", label.Acyclicer);
        Assert.Equal("UL", label.Align);
        Assert.Equal(10, label.MarginX);
        Assert.Equal(15, label.MarginY);
        Assert.Equal(500, label.Width);
        Assert.Equal(300, label.Height);
    }

    [Fact]
    public void InternalProperties_CanBeSet()
    {
        var label = new GraphLabel
        {
            NestingRoot = "nest",
            NodeRankFactor = 2,
            DummyChains = new List<string> { "a", "b" },
            MaxRank = 5,
            Root = "root"
        };
        
        Assert.Equal("nest", label.NestingRoot);
        Assert.Equal(2, label.NodeRankFactor);
        Assert.Equal(2, label.DummyChains.Count);
        Assert.Equal(5, label.MaxRank);
        Assert.Equal("root", label.Root);
    }
}

public class DagrePointTests
{
    [Fact]
    public void Constructor_FloatCoordinates_SetsProperties()
    {
        var point = new DagrePoint(10.5f, 20.7f);
        
        Assert.Equal(10.5f, point.X);
        Assert.Equal(20.7f, point.Y);
    }

    [Fact]
    public void Constructor_DoubleCoordinates_ConvertsToFloat()
    {
        var point = new DagrePoint(10.5, 20.7);
        
        Assert.Equal(10.5f, point.X);
        Assert.Equal(20.7f, point.Y);
    }

    [Fact]
    public void DefaultConstructor_Zeros()
    {
        var point = new DagrePoint();
        
        Assert.Equal(0, point.X);
        Assert.Equal(0, point.Y);
    }
}

public class DagreExceptionTests
{
    [Fact]
    public void DefaultConstructor_CreatesException()
    {
        var ex = new DagreException();
        
        Assert.NotNull(ex);
    }

    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        var ex = new DagreException("Test error");
        
        Assert.Equal("Test error", ex.Message);
    }
}

public class DagreInputNodeTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var node = new DagreInputNode();
        
        Assert.Equal(300, node.Width);
        Assert.Equal(100, node.Height);
        Assert.Equal(0, node.X);
        Assert.Equal(0, node.Y);
        Assert.Null(node.Tag);
        Assert.Null(node.Group);
        Assert.Empty(node.Childs);
        Assert.Empty(node.Parents);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var node = new DagreInputNode
        {
            Tag = "Test",
            Width = 200,
            Height = 80,
            X = 100,
            Y = 200
        };
        
        Assert.Equal("Test", node.Tag);
        Assert.Equal(200, node.Width);
        Assert.Equal(80, node.Height);
        Assert.Equal(100, node.X);
        Assert.Equal(200, node.Y);
    }
}

public class DagreInputEdgeTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var edge = new DagreInputEdge();
        
        Assert.Null(edge.From);
        Assert.Null(edge.To);
        Assert.Null(edge.Points);
        Assert.Null(edge.Tag);
        Assert.Equal(0, edge.MinLen);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var from = new DagreInputNode { Tag = "A" };
        var to = new DagreInputNode { Tag = "B" };
        var points = new[] { new DagreCurvePoint(10, 20), new DagreCurvePoint(30, 40) };
        
        var edge = new DagreInputEdge
        {
            From = from,
            To = to,
            MinLen = 2,
            Points = points,
            Tag = "TestEdge"
        };
        
        Assert.Same(from, edge.From);
        Assert.Same(to, edge.To);
        Assert.Equal(2, edge.MinLen);
        Assert.Same(points, edge.Points);
        Assert.Equal("TestEdge", edge.Tag);
    }
}

public class PointFTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var point = new PointF(10.5f, 20.7f);
        
        Assert.Equal(10.5f, point.X);
        Assert.Equal(20.7f, point.Y);
    }

    [Fact]
    public void RecordStruct_Equality()
    {
        var p1 = new PointF(10, 20);
        var p2 = new PointF(10, 20);
        var p3 = new PointF(10, 21);
        
        Assert.Equal(p1, p2);
        Assert.NotEqual(p1, p3);
    }
}

public class EdgeOptionsTests
{
    [Fact]
    public void Default_HasDefaultValues()
    {
        var options = EdgeOptions.Default;
        
        Assert.Equal(1, options.MinLength);
        Assert.Equal(1, options.Weight);
        Assert.Equal(0, options.LabelWidth);
        Assert.Equal(0, options.LabelHeight);
        Assert.Equal(10, options.LabelOffset);
        Assert.Equal(LabelPosition.Right, options.LabelPosition);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var options = new EdgeOptions
        {
            MinLength = 3,
            Weight = 5,
            LabelWidth = 50,
            LabelHeight = 20,
            LabelOffset = 15,
            LabelPosition = LabelPosition.Center
        };
        
        Assert.Equal(3, options.MinLength);
        Assert.Equal(5, options.Weight);
        Assert.Equal(50, options.LabelWidth);
        Assert.Equal(20, options.LabelHeight);
        Assert.Equal(15, options.LabelOffset);
        Assert.Equal(LabelPosition.Center, options.LabelPosition);
    }
}

public class GraphLayoutOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new GraphLayoutOptions();
        
        Assert.Equal(50, options.RankSeparation);
        Assert.Equal(20, options.EdgeSeparation);
        Assert.Equal(50, options.NodeSeparation);
        Assert.Equal(LayoutDirection.TopToBottom, options.Direction);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var options = new GraphLayoutOptions
        {
            RankSeparation = 100,
            EdgeSeparation = 30,
            NodeSeparation = 75,
            Direction = LayoutDirection.LeftToRight
        };
        
        Assert.Equal(100, options.RankSeparation);
        Assert.Equal(30, options.EdgeSeparation);
        Assert.Equal(75, options.NodeSeparation);
        Assert.Equal(LayoutDirection.LeftToRight, options.Direction);
    }
}
