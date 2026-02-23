namespace Mostlylucid.Dagre;

/// <summary>
///     Clean, strongly-typed API for Dagre graph layout.
///     Hides internal JS-ported implementation details behind a modern C# interface.
/// </summary>
public sealed class GraphLayout
{
    private readonly Dictionary<(string, string), object> _edgeUserData = new();
    private readonly DagreGraph _graph;
    private readonly Dictionary<string, object> _nodeUserData = new(StringComparer.Ordinal);

    public GraphLayout(GraphLayoutOptions options = null)
    {
        options ??= new GraphLayoutOptions();
        _graph = new DagreGraph(true) { _isMultigraph = true };
        var gl = _graph.Graph();
        gl.RankSep = options.RankSeparation;
        gl.EdgeSep = options.EdgeSeparation;
        gl.NodeSep = options.NodeSeparation;
        gl.RankDir = options.Direction switch
        {
            LayoutDirection.TopToBottom => "tb",
            LayoutDirection.BottomToTop => "bt",
            LayoutDirection.LeftToRight => "lr",
            LayoutDirection.RightToLeft => "rl",
            _ => "tb"
        };
    }

    /// <summary>
    ///     Add a node to the graph with the specified dimensions.
    /// </summary>
    /// <param name="id">Unique node identifier.</param>
    /// <param name="width">Node width in pixels.</param>
    /// <param name="height">Node height in pixels.</param>
    /// <param name="userData">Optional user data to associate with this node.</param>
    public GraphLayout AddNode(string id, float width, float height, object userData = null)
    {
        var nd = new NodeLabel { ["width"] = width, ["height"] = height };
        _graph.SetNode(id, nd);
        if (userData != null)
            _nodeUserData[id] = userData;
        return this;
    }

    /// <summary>
    ///     Add a directed edge between two nodes.
    /// </summary>
    /// <param name="source">Source node ID.</param>
    /// <param name="target">Target node ID.</param>
    /// <param name="options">Optional edge options (weight, minlen, etc.).</param>
    /// <param name="userData">Optional user data to associate with this edge.</param>
    public GraphLayout AddEdge(string source, string target, EdgeOptions options = null, object userData = null)
    {
        options ??= EdgeOptions.Default;
        var el = new EdgeLabel
        {
            ["minlen"] = options.MinLength,
            ["weight"] = options.Weight,
            ["width"] = options.LabelWidth,
            ["height"] = options.LabelHeight,
            ["labeloffset"] = options.LabelOffset,
            ["labelpos"] = options.LabelPosition switch
            {
                LabelPosition.Left => "l",
                LabelPosition.Right => "r",
                LabelPosition.Center => "c",
                _ => "r"
            }
        };
        _graph.SetEdge(source, target, el);
        if (userData != null)
            _edgeUserData[(source, target)] = userData;
        return this;
    }

    /// <summary>
    ///     Run the layout algorithm and return the results.
    /// </summary>
    public LayoutResultData Run()
    {
        DagreLayout.RunLayout(_graph);

        var nodeResults = new Dictionary<string, NodeResult>(StringComparer.Ordinal);
        foreach (var v in _graph.Nodes())
        {
            var label = _graph.Node(v);
            if (label == null) continue;
            _nodeUserData.TryGetValue(v, out var ud);
            nodeResults[v] = new NodeResult
            {
                X = label.X,
                Y = label.Y,
                Width = label.Width,
                Height = label.Height,
                UserData = ud
            };
        }

        var edgeResults = new List<EdgeResult>();
        foreach (var e in _graph.Edges())
        {
            var label = _graph.Edge(e);
            if (label == null) continue;
            var points = new List<PointF>();
            if (label.Points is { Count: > 0 })
                foreach (var pt in label.Points)
                    points.Add(new PointF(pt.X, pt.Y));

            _edgeUserData.TryGetValue((e.v, e.w), out var eud);
            edgeResults.Add(new EdgeResult
            {
                Source = e.v,
                Target = e.w,
                Points = points,
                UserData = eud
            });
        }

        return new LayoutResultData { Nodes = nodeResults, Edges = edgeResults };
    }
}

/// <summary>
///     Options for configuring the graph layout algorithm.
/// </summary>
public sealed class GraphLayoutOptions
{
    /// <summary>Separation between ranks (layers). Default: 50.</summary>
    public int RankSeparation { get; init; } = 50;

    /// <summary>Separation between edges. Default: 20.</summary>
    public int EdgeSeparation { get; init; } = 20;

    /// <summary>Separation between nodes within the same rank. Default: 50.</summary>
    public int NodeSeparation { get; init; } = 50;

    /// <summary>Layout direction. Default: TopToBottom.</summary>
    public LayoutDirection Direction { get; init; } = LayoutDirection.TopToBottom;
}

/// <summary>
///     Layout direction for the graph.
/// </summary>
public enum LayoutDirection
{
    TopToBottom,
    BottomToTop,
    LeftToRight,
    RightToLeft
}

/// <summary>
///     Options for individual edges.
/// </summary>
public sealed class EdgeOptions
{
    internal static readonly EdgeOptions Default = new();

    /// <summary>Minimum number of ranks to span. Default: 1.</summary>
    public int MinLength { get; init; } = 1;

    /// <summary>Edge weight for crossing minimization. Default: 1.</summary>
    public int Weight { get; init; } = 1;

    /// <summary>Width of edge label. Default: 0.</summary>
    public float LabelWidth { get; init; } = 0;

    /// <summary>Height of edge label. Default: 0.</summary>
    public float LabelHeight { get; init; } = 0;

    /// <summary>Label offset from edge. Default: 10.</summary>
    public int LabelOffset { get; init; } = 10;

    /// <summary>Label position relative to edge. Default: Right.</summary>
    public LabelPosition LabelPosition { get; init; } = LabelPosition.Right;
}

/// <summary>
///     Position of an edge label.
/// </summary>
public enum LabelPosition
{
    Left,
    Right,
    Center
}

/// <summary>
///     Result of running the layout algorithm.
/// </summary>
public sealed class LayoutResultData
{
    /// <summary>Node positions keyed by node ID.</summary>
    public Dictionary<string, NodeResult> Nodes { get; init; }

    /// <summary>Edge routing results.</summary>
    public List<EdgeResult> Edges { get; init; }
}

/// <summary>
///     Layout result for a single node.
/// </summary>
public sealed class NodeResult
{
    /// <summary>Center X coordinate.</summary>
    public float X { get; init; }

    /// <summary>Center Y coordinate.</summary>
    public float Y { get; init; }

    /// <summary>Node width.</summary>
    public float Width { get; init; }

    /// <summary>Node height.</summary>
    public float Height { get; init; }

    /// <summary>User data associated with this node.</summary>
    public object UserData { get; init; }
}

/// <summary>
///     Layout result for a single edge.
/// </summary>
public sealed class EdgeResult
{
    /// <summary>Source node ID.</summary>
    public string Source { get; init; }

    /// <summary>Target node ID.</summary>
    public string Target { get; init; }

    /// <summary>Routed points along the edge path.</summary>
    public List<PointF> Points { get; init; }

    /// <summary>User data associated with this edge.</summary>
    public object UserData { get; init; }
}

/// <summary>
///     Simple 2D point with float coordinates.
/// </summary>
public readonly record struct PointF(float X, float Y);