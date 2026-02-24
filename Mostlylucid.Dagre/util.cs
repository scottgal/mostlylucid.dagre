namespace Mostlylucid.Dagre;

public class Util
{
    public static int UniqueCounter;

    /*
     * Adjusts the ranks for all nodes in the graph such that all nodes v have
     * rank(v) >= 0 and at least one node w has rank(w) = 0.
     */
    public static void NormalizeRanks(DagreGraph g)
    {
        var min = int.MaxValue;
        foreach (var v in g.NodesRaw())
        {
            var node = g.NodeRaw(v);
            if (node.ContainsKey("rank"))
                if (node.Rank < min)
                    min = node.Rank;
        }

        foreach (var v in g.NodesRaw())
        {
            var node = g.NodeRaw(v);
            if (node.ContainsKey("rank")) node.Rank -= min;
        }
    }

    public static DagreGraph AsNonCompoundGraph(DagreGraph g)
    {
        var graph = new DagreGraph(false) { _isMultigraph = g.IsMultigraph() };
        graph.SetGraph(g.Graph());
        foreach (var v in g.NodesRaw())
            if (!g.HasChildren(v))
                graph.SetNode(v, g.NodeRaw(v));

        foreach (var e in g.EdgesRaw()) graph.SetEdge(e.v, e.w, g.EdgeRaw(e), e.name);

        return graph;
    }

    public static int[] Range(int start, int end)
    {
        var count = end - start;
        if (count <= 0) return Array.Empty<int>();
        var result = new int[count];
        for (var i = 0; i < count; i++)
            result[i] = start + i;
        return result;
    }

    public static string UniqueId(string str)
    {
        var id = Interlocked.Increment(ref UniqueCounter);
        return string.Create(str.Length + CountDigits(id), (str, id), static (span, state) =>
        {
            state.str.AsSpan().CopyTo(span);
            state.id.TryFormat(span[state.str.Length..], out _);
        });
    }

    private static int CountDigits(int n)
    {
        if (n < 10) return 1;
        if (n < 100) return 2;
        if (n < 1000) return 3;
        if (n < 10000) return 4;
        if (n < 100000) return 5;
        return n.ToString().Length;
    }

    /*
     * Adds a dummy node to the graph and return v.
     */
    public static string AddDummyNode(DagreGraph g, string type, NodeLabel attrs, string name)
    {
        string v = null;

        do
        {
            v = UniqueId(name);
        } while (g.HasNode(v));

        attrs.Dummy = type;

        g.SetNode(v, attrs);
        return v;
    }

    public static int MaxRank(DagreGraph g)
    {
        var max = 0;
        foreach (var v in g.Nodes())
        {
            var node = g.Node(v);
            if (node.ContainsKey("rank") && node.Rank > max)
                max = node.Rank;
        }

        return max;
    }

    /*
     * Returns a new graph with only simple edges. Handles aggregation of data
     * associated with multi-edges.
     */
    public static DagreGraph Simplify(DagreGraph g)
    {
        var simplified = new DagreGraph(false).SetGraph(g.Graph());
        foreach (var v in g.NodesRaw()) simplified.SetNode(v, g.NodeRaw(v));
        foreach (var e in g.EdgesRaw())
        {
            var r = simplified.EdgeRaw(e.v, e.w);
            var label = g.EdgeRaw(e);
            if (label == null) continue;

            var simpleWeight = r != null ? r.Weight : 0;
            var simpleMinlen = r != null ? r.Minlen : 1;

            var merged = new EdgeLabel();
            merged.Weight = simpleWeight + label.Weight;
            merged.Minlen = Math.Max(simpleMinlen, label.Minlen);

            simplified.SetEdge(e.v, e.w, merged);
        }

        return simplified;
    }


    /*
     * Given a DAG with each node assigned "rank" and "order" properties, this
     * function will produce a matrix with the ids of each node.
     */
    public static List<string[]> BuildLayerMatrix(DagreGraph g)
    {
        var rank = MaxRank(g);
        var layers = new List<List<string>>(rank + 1);
        for (var i = 0; i <= rank; i++) layers.Add(new List<string>());

        foreach (var v in g.Nodes())
        {
            var node = g.Node(v);

            if (node.ContainsKey("rank")) layers[node.Rank].Add(v);
        }

        // Sort each layer by node order and convert to array
        var result = new List<string[]>(layers.Count);
        foreach (var layer in layers)
        {
            layer.Sort((a, b) => g.Node(a).Order.CompareTo(g.Node(b).Order));
            result.Add(layer.ToArray());
        }

        return result;
    }

    internal static object AddBorderNode(DagreGraph g, string v)
    {
        throw new NotImplementedException();
    }

    internal static int[] Range(int v1, int v2, int step)
    {
        var count = step > 0
            ? Math.Max(0, (v2 - v1 + step - 1) / step)
            : Math.Max(0, (v1 - v2 - step - 1) / -step);
        var ret = new int[count];
        for (var i = 0; i < count; i++) ret[i] = v1 + i * step;
        return ret;
    }

    /*
     * Finds where a line starting at point ({x, y}) would intersect a rectangle
     * ({x, y, width, height}) if it were pointing at the rectangle's center.
     */
    internal static DagrePoint IntersectRect(NodeLabel rect, DagrePoint point)
    {
        var x = rect.X;
        var y = rect.Y;

        // Rectangle intersection algorithm from:
        // http://math.stackexchange.com/questions/108113/find-edge-between-two-boxes
        var dx = point.X - x;
        var dy = point.Y - y;
        var w = rect.Width / 2f;
        var h = rect.Height / 2f;
        if (dx == 0 && dy == 0) throw new DagreException("Not possible to find intersection inside of the rectangle");

        double sx, sy;
        if (Math.Abs(dy) * w > Math.Abs(dx) * h)
        {
            // Intersection is top or bottom of rect.
            if (dy < 0) h = -h;
            sx = h * dx / dy;
            sy = h;
        }
        else
        {
            // Intersection is left or right of rect.
            if (dx < 0) w = -w;
            sx = w;
            sy = w * dy / dx;
        }

        return new DagrePoint(x + sx, y + sy);
    }

    /// <summary>
    ///     Shape-aware intersection. Checks the node's "shape" property and
    ///     dispatches to the appropriate intersection function. Falls back to
    ///     IntersectRect for unknown or rectangular shapes.
    /// </summary>
    internal static DagrePoint IntersectNode(NodeLabel node, DagrePoint point)
    {
        if (node.TryGetValue("shape", out var shapeObj) && shapeObj is string shape)
        {
            if (shape == "Diamond")
                return IntersectDiamond(node, point);
            if (shape is "Circle" or "DoubleCircle")
                return IntersectEllipse(node, point);
        }
        return IntersectRect(node, point);
    }

    /// <summary>
    ///     Intersect a ray from the diamond center to the given point with the
    ///     diamond boundary. The diamond has 4 vertices at the cardinal tips:
    ///     top (cx, cy-hh), right (cx+hw, cy), bottom (cx, cy+hh), left (cx-hw, cy).
    /// </summary>
    internal static DagrePoint IntersectDiamond(NodeLabel node, DagrePoint point)
    {
        var cx = node.X;
        var cy = node.Y;
        var hw = node.Width / 2f;
        var hh = node.Height / 2f;

        var dx = point.X - cx;
        var dy = point.Y - cy;
        if (dx == 0 && dy == 0)
            return new DagrePoint(cx, cy - hh); // default: top tip

        // Diamond boundary in normalized coordinates: |dx/hw| + |dy/hh| = 1
        // Scale the direction to find the intersection
        var adx = Math.Abs(dx);
        var ady = Math.Abs(dy);
        var denom = adx / hw + ady / hh;
        if (denom < 0.0001)
            return new DagrePoint(cx, cy - hh);

        var scale = 1.0 / denom;
        return new DagrePoint(cx + dx * scale, cy + dy * scale);
    }

    /// <summary>
    ///     Intersect a ray from the ellipse center to the given point with the
    ///     ellipse boundary (used for Circle, DoubleCircle).
    /// </summary>
    internal static DagrePoint IntersectEllipse(NodeLabel node, DagrePoint point)
    {
        var cx = node.X;
        var cy = node.Y;
        var rx = node.Width / 2.0;
        var ry = node.Height / 2.0;

        var dx = point.X - cx;
        var dy = point.Y - cy;
        if (dx == 0 && dy == 0)
            return new DagrePoint(cx, cy - ry);

        // Parametric: intersection at t where (dx*t/rx)^2 + (dy*t/ry)^2 = 1
        var len = Math.Sqrt(dx * dx / (rx * rx) + dy * dy / (ry * ry));
        if (len < 0.0001)
            return new DagrePoint(cx, cy - ry);

        return new DagrePoint(cx + dx / len, cy + dy / len);
    }
}