using Mostlylucid.Dagre;

namespace Mostlylucid.Dagre.Tests;

public class EdgeLabelTests
{
    [Fact]
    public void Properties_SetAndGet_WorksCorrectly()
    {
        var label = new EdgeLabel();
        
        label.X = 100;
        label.Y = 200;
        label.Width = 50;
        label.Height = 30;
        label.Weight = 5;
        label.Minlen = 2;
        
        Assert.Equal(100, label.X);
        Assert.Equal(200, label.Y);
        Assert.Equal(50, label.Width);
        Assert.Equal(30, label.Height);
        Assert.Equal(5, label.Weight);
        Assert.Equal(2, label.Minlen);
    }

    [Fact]
    public void Indexer_GetSet_WorksLikeDictionary()
    {
        var label = new EdgeLabel();
        
        label["x"] = 150f;
        label["y"] = 250f;
        label["weight"] = 10;
        label["minlen"] = 3;
        
        Assert.Equal(150f, label["x"]);
        Assert.Equal(250f, label["y"]);
        Assert.Equal(10, label["weight"]);
        Assert.Equal(3, label["minlen"]);
    }

    [Fact]
    public void Indexer_TypedAndUntyped_AreEquivalent()
    {
        var label = new EdgeLabel();
        
        label["weight"] = 7;
        
        Assert.Equal(7, label.Weight);
        Assert.Equal(7, label["weight"]);
    }

    [Fact]
    public void ContainsKey_ReturnsTrueForSetProperties()
    {
        var label = new EdgeLabel();
        
        Assert.False(label.ContainsKey("weight"));
        
        label.Weight = 5;
        
        Assert.True(label.ContainsKey("weight"));
    }

    [Fact]
    public void ContainsKey_OverflowKey_ReturnsTrueAfterSet()
    {
        var label = new EdgeLabel();
        
        Assert.False(label.ContainsKey("custom"));
        
        label["custom"] = "value";
        
        Assert.True(label.ContainsKey("custom"));
    }

    [Fact]
    public void TryGetValue_ReturnsCorrectValue()
    {
        var label = new EdgeLabel();
        label.Minlen = 3;
        
        var found = label.TryGetValue("minlen", out var value);
        
        Assert.True(found);
        Assert.Equal(3, value);
    }

    [Fact]
    public void Remove_ClearsProperty()
    {
        var label = new EdgeLabel();
        label.Weight = 10;
        
        var removed = label.Remove("weight");
        
        Assert.True(removed);
        Assert.False(label.ContainsKey("weight"));
    }

    [Fact]
    public void Keys_ReturnsAllSetKeys()
    {
        var label = new EdgeLabel();
        label.Weight = 1;
        label.Minlen = 2;
        label["custom"] = "test";
        
        var keys = label.Keys;
        
        Assert.Equal(3, keys.Count);
        Assert.Contains("weight", keys);
        Assert.Contains("minlen", keys);
        Assert.Contains("custom", keys);
    }

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        var label = new EdgeLabel();
        
        Assert.Equal(0, (int)label.Count);
        
        label.Weight = 1;
        label.Minlen = 2;
        
        Assert.Equal(2, label.Count);
    }

    [Fact]
    public void Clear_RemovesAllProperties()
    {
        var label = new EdgeLabel();
        label.Weight = 1;
        label["custom"] = "test";
        
        label.Clear();
        
        Assert.Equal(0, (int)label.Count);
    }

    [Fact]
    public void Points_WorkCorrectly()
    {
        var label = new EdgeLabel();
        var points = new List<DagrePoint>
        {
            new(10, 20),
            new(30, 40),
            new(50, 60)
        };
        
        label.Points = points;
        
        Assert.Same(points, label.Points);
        Assert.Equal(3, label.Points.Count);
    }

    [Fact]
    public void LabelProperties_WorkCorrectly()
    {
        var label = new EdgeLabel();
        
        label.LabelPos = "r";
        label.LabelOffset = 15;
        label.LabelRank = 2;
        
        Assert.Equal("r", label.LabelPos);
        Assert.Equal(15, label.LabelOffset);
        Assert.Equal(2, label.LabelRank);
    }

    [Fact]
    public void ReversedAndForwardName_WorkCorrectly()
    {
        var label = new EdgeLabel();
        
        label.Reversed = true;
        label.ForwardName = "forward";
        
        Assert.True(label.Reversed);
        Assert.Equal("forward", label.ForwardName);
    }

    [Fact]
    public void NetworkSimplexProperties_WorkCorrectly()
    {
        var label = new EdgeLabel();
        
        label.Cutvalue = -5;
        label.NestingEdge = true;
        
        Assert.Equal(-5, label.Cutvalue);
        Assert.True(label.NestingEdge);
    }

    [Fact]
    public void Source_WorkCorrectly()
    {
        var label = new EdgeLabel();
        var source = new { Id = 1 };
        
        label.Source = source;
        
        Assert.Same(source, label.Source);
    }

    [Fact]
    public void OverflowProperties_StoreArbitraryData()
    {
        var label = new EdgeLabel();
        
        label["myObject"] = "test";
        label["myNumber"] = 42;
        
        Assert.Equal("test", label["myObject"]);
        Assert.Equal(42, label["myNumber"]);
    }

    [Fact]
    public void GetEnumerator_IteratesOverAllProperties()
    {
        var label = new EdgeLabel();
        label.Weight = 5;
        label.Minlen = 2;
        
        var dict = new Dictionary<string, object>();
        foreach (var kvp in label)
        {
            dict[kvp.Key] = kvp.Value;
        }
        
        Assert.Equal(2, dict.Count);
    }
}
