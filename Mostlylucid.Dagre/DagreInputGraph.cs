using Mostlylucid.Dagre.Indexed;

namespace Mostlylucid.Dagre;

public class DagreInputGraph
{
    public static bool ExceptionOnDuplicateEdge = false;
    public static bool ExceptionOnReverseDuplicateEdge = true;
    private readonly List<DagreInputEdge> edges = new();
    private readonly List<DagreInputNode> nodes = new();

    public bool VerticalLayout { get; set; } = true;

    public DagreInputNode GetNode(object tag)
    {
        return nodes.FirstOrDefault(z => z.Tag == tag);
    }

    public DagreInputEdge[] Edges()
    {
        return edges.ToArray();
    }

    public DagreInputNode[] Nodes()
    {
        return nodes.ToArray();
    }

    public DagreInputEdge GetEdge(DagreInputNode from, DagreInputNode to)
    {
        return edges.First(z => z.From == from && z.To == to);
    }

    public DagreInputEdge AddEdge(DagreInputNode from, DagreInputNode to, int minLen = 1)
    {
        if (edges.Any(z => z.From == from && z.To == to))
        {
            var fr = GetEdge(from, to);
            if (ExceptionOnDuplicateEdge)
                throw new DagreException("duplicate edge");
            return fr;
        }

        if (edges.Any(z => z.From == to && z.To == from))
        {
            var fr = GetEdge(to, from);
            if (ExceptionOnReverseDuplicateEdge)
                throw new DagreException("reverse duplicate edge");
            return fr;
        }

        if (to.Parents.Contains(from)) throw new DagreException("duplciate parent");
        to.Parents.Add(from);
        if (from.Childs.Contains(to)) throw new DagreException("duplciate child");
        from.Childs.Add(to);
        var edge = new DagreInputEdge { From = from, To = to, MinLen = minLen };
        edges.Add(edge);
        return edge;
    }


    public void AddNode(DagreInputNode node)
    {
        if (nodes.Contains(node)) throw new DagreException("duplciate node");
        nodes.Add(node);
    }

    public DagreInputNode AddNode(object tag = null, float? width = null, float? height = null)
    {
        var ret = new DagreInputNode();
        ret.Tag = tag;
        if (width != null && width.Value > 0)
            ret.Width = width.Value;
        if (height != null && height.Value > 0)
            ret.Height = height.Value;
        AddNode(ret);
        return ret;
    }

    public DagreInputGroup AddGroup(object tag = null)
    {
        var ret = new DagreInputGroup();
        ret.Tag = tag;

        AddNode(ret);
        return ret;
    }

    private void check()
    {
        foreach (var item in nodes)
        foreach (var ch in item.Childs)
        {
        }
    }

    public void Layout(Action<ExtProgressInfo> progress = null)
    {
        check();
        var dg = new DagreGraph(true) { _isMultigraph = true };

        var list1 = nodes.Where(z => z is DagreInputGroup || z.Childs.Count > 0 || z.Parents.Count > 0).ToList();

        foreach (var gg in list1.Where(z => !(z is DagreInputGroup)))
        {
            var ind = list1.IndexOf(gg);
            var nd = new NodeLabel { ["source"] = gg, ["width"] = gg.Width, ["height"] = gg.Height };
            dg.SetNode(ind + "", nd);
        }

        foreach (var gg in list1.Where(z => z is DagreInputGroup))
        {
            var ind = list1.IndexOf(gg);
            var nd = new NodeLabel { ["source"] = gg, ["isGroup"] = true, ["width"] = 0f, ["height"] = 0f };
            dg.SetNode(ind + "", nd);
        }

        foreach (var gg in list1.Where(z => z.Group != null && !(z is DagreInputGroup)))
        {
            var ind = list1.IndexOf(gg);
            var nd = dg.Node(ind.ToString());
            var ind2 = list1.IndexOf(gg.Group);
            dg.SetParent(ind.ToString(), ind2.ToString());
        }

        foreach (var gg in list1)
        {
            var ind = list1.IndexOf(gg);

            foreach (var item in gg.Childs)
            {
                var edge = edges.First(z => z.From == gg && z.To == item);
                var jj = new EdgeLabel
                {
                    ["minlen"] = edge.MinLen,
                    ["weight"] = 1,
                    ["width"] = 0,
                    ["height"] = 0,
                    ["labeloffset"] = 10,
                    ["labelpos"] = "r",
                    ["source"] = edge
                };
                dg.SetEdge(ind.ToString(), list1.IndexOf(item).ToString(), jj);
            }
        }

        dg.Graph().RankSep = 20;
        dg.Graph().EdgeSep = 20;
        dg.Graph().NodeSep = 25;
        if (VerticalLayout)
            dg.Graph().RankDir = "tb";
        else
            dg.Graph().RankDir = "lr";
        IndexedDagreLayout.RunLayout(dg, f => { progress?.Invoke(f); });

        //back
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = dg.Node(i + "");
            var n = nodes[i];
            n.X = node.X - node.Width / 2;
            n.Y = node.Y - node.Height / 2;
            n.Width = node.Width;
            n.Height = node.Height;
        }

        foreach (var item in dg.Edges())
        {
            var edge = dg.Edge(item);
            var src = edge.Source as DagreInputEdge;
            var rr = new List<DagreCurvePoint>();
            foreach (var pt in edge.Points) rr.Add(new DagreCurvePoint(pt.X, pt.Y));

            src.Points = rr.ToArray();
        }
    }

    public void SetGroup(DagreInputNode node, DagreInputNode dagreInputNode)
    {
        node.Group = dagreInputNode;
    }
}