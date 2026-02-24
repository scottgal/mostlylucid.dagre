using Mostlylucid.Dagre;

namespace Mostlylucid.Dagre.Tests;

public class NodeLabelTests
{
    [Fact]
    public void Properties_SetAndGet_WorksCorrectly()
    {
        var label = new NodeLabel();
        
        label.X = 100;
        label.Y = 200;
        label.Width = 50;
        label.Height = 30;
        label.Rank = 2;
        label.Order = 1;
        
        Assert.Equal(100, label.X);
        Assert.Equal(200, label.Y);
        Assert.Equal(50, label.Width);
        Assert.Equal(30, label.Height);
        Assert.Equal(2, label.Rank);
        Assert.Equal(1, label.Order);
    }

    [Fact]
    public void Indexer_GetSet_WorksLikeDictionary()
    {
        var label = new NodeLabel();
        
        label["x"] = 150f;
        label["y"] = 250f;
        label["width"] = 75f;
        label["height"] = 45f;
        
        Assert.Equal(150f, label["x"]);
        Assert.Equal(250f, label["y"]);
        Assert.Equal(75f, label["width"]);
        Assert.Equal(45f, label["height"]);
    }

    [Fact]
    public void Indexer_TypedAndUntyped_AreEquivalent()
    {
        var label = new NodeLabel();
        
        label["x"] = 100f;
        
        Assert.Equal(100, label.X);
        Assert.Equal(100f, label["x"]);
    }

    [Fact]
    public void ContainsKey_ReturnsTrueForSetProperties()
    {
        var label = new NodeLabel();
        
        Assert.False(label.ContainsKey("x"));
        
        label.X = 100;
        
        Assert.True(label.ContainsKey("x"));
    }

    [Fact]
    public void ContainsKey_OverflowKey_ReturnsTrueAfterSet()
    {
        var label = new NodeLabel();
        
        Assert.False(label.ContainsKey("custom"));
        
        label["custom"] = "value";
        
        Assert.True(label.ContainsKey("custom"));
    }

    [Fact]
    public void TryGetValue_ReturnsCorrectValue()
    {
        var label = new NodeLabel();
        label.X = 123;
        
        var found = label.TryGetValue("x", out var value);
        
        Assert.True(found);
        Assert.Equal(123f, value);
    }

    [Fact]
    public void TryGetValue_MissingKey_ReturnsFalse()
    {
        var label = new NodeLabel();
        
        var found = label.TryGetValue("missing", out var value);
        
        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void Remove_ClearsProperty()
    {
        var label = new NodeLabel();
        label.X = 100;
        
        var removed = label.Remove("x");
        
        Assert.True(removed);
        Assert.False(label.ContainsKey("x"));
    }

    [Fact]
    public void Keys_ReturnsAllSetKeys()
    {
        var label = new NodeLabel();
        label.X = 1;
        label.Y = 2;
        label.Width = 3;
        label["custom"] = "test";
        
        var keys = label.Keys;
        
        Assert.Equal(4, keys.Count);
        Assert.Contains("x", keys);
        Assert.Contains("y", keys);
        Assert.Contains("width", keys);
        Assert.Contains("custom", keys);
    }

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        var label = new NodeLabel();
        
        Assert.Equal(0, (int)label.Count);
        
        label.X = 1;
        label.Y = 2;
        
        Assert.Equal(2, label.Count);
        
        label["custom"] = "test";
        
        Assert.Equal(3, label.Count);
    }

    [Fact]
    public void Clear_RemovesAllProperties()
    {
        var label = new NodeLabel();
        label.X = 1;
        label.Y = 2;
        label["custom"] = "test";
        
        label.Clear();
        
        Assert.Equal(0, (int)label.Count);
        Assert.False(label.ContainsKey("x"));
        Assert.False(label.ContainsKey("custom"));
    }

    [Fact]
    public void GetEnumerator_IteratesOverAllProperties()
    {
        var label = new NodeLabel();
        label.X = 100;
        label.Y = 200;
        
        var dict = new Dictionary<string, object>();
        foreach (var kvp in label)
        {
            dict[kvp.Key] = kvp.Value;
        }
        
        Assert.Equal(2, dict.Count);
        Assert.Equal(100f, dict["x"]);
        Assert.Equal(200f, dict["y"]);
    }

    [Fact]
    public void OverflowProperties_StoreArbitraryData()
    {
        var label = new NodeLabel();
        var obj = new { Name = "Test" };
        
        label["myObject"] = obj;
        label["myNumber"] = 42;
        label["myString"] = "hello";
        
        Assert.Same(obj, label["myObject"]);
        Assert.Equal(42, label["myNumber"]);
        Assert.Equal("hello", label["myString"]);
    }

    [Fact]
    public void BorderProperties_WorkCorrectly()
    {
        var label = new NodeLabel();
        var left = new Dictionary<string, string> { { "0", "borderLeft" } };
        var right = new Dictionary<string, string> { { "0", "borderRight" } };
        
        label.BorderTop = "top";
        label.BorderBottom = "bottom";
        label.BorderLeft = left;
        label.BorderRight = right;
        
        Assert.Equal("top", label.BorderTop);
        Assert.Equal("bottom", label.BorderBottom);
        Assert.Same(left, label.BorderLeft);
        Assert.Same(right, label.BorderRight);
    }

    [Fact]
    public void TreeProperties_WorkCorrectly()
    {
        var label = new NodeLabel();
        
        label.Low = 1;
        label.Lim = 5;
        label.Parent = "parent";
        label.Cutvalue = -3;
        
        Assert.Equal(1, label.Low);
        Assert.Equal(5, label.Lim);
        Assert.Equal("parent", label.Parent);
        Assert.Equal(-3, label.Cutvalue);
    }

    [Fact]
    public void DummyAndNestingProperties_WorkCorrectly()
    {
        var label = new NodeLabel();
        
        label.Dummy = "edge";
        label.IsGroup = true;
        label.NestingRoot = "nest";
        label.NestingEdge = true;
        
        Assert.Equal("edge", label.Dummy);
        Assert.True(label.IsGroup);
        Assert.Equal("nest", label.NestingRoot);
        Assert.True(label.NestingEdge);
    }
}
