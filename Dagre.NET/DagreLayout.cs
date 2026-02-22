using System;
using System.Collections.Generic;
using System.Linq;

namespace Dagre
{
    public static class DagreLayout
    {      
        /*
         * This idea comes from the Gansner paper: to account for edge labels in our
         * layout we split each rank in half by doubling minlen and halving ranksep.
         * Then we can place labels at these mid-points between nodes.
         *
         * We also add some minimal padding to the width to push the label for the edge
         * away from the edge itself a bit.
         */
        public static void makeSpaceForEdgeLabels(DagreGraph g)
        {
            var graph = g.Graph();
            graph.RankSep = graph.RankSep / 2;
            foreach (var e in g.EdgesRaw())
            {
                var edge = g.EdgeRaw(e);
                edge.Minlen = edge.Minlen * 2;
                if (!string.Equals(edge.LabelPos, "c", StringComparison.OrdinalIgnoreCase))
                {
                    if (graph.RankDir == "TB" || graph.RankDir == "BT")
                    {
                        edge.Width += edge.LabelOffset;
                    }
                    else
                    {
                        edge.Height += edge.LabelOffset;
                    }
                }
            }
        }

        public static void removeSelfEdges(DagreGraph g)
        {
            var ar = (DagreEdgeIndex[])g.EdgesRaw().Clone();
            foreach (var e in ar)
            {
                if (e.v == e.w)
                {
                    var node = g.NodeRaw(e.v);
                    if (node.SelfEdges == null)
                    {
                        node.SelfEdges = new List<SelfEdgeInfo>();
                    }
                    node.SelfEdges.Add(new SelfEdgeInfo() { e = e, label = g.EdgeRaw(e) });
                    g.RemoveEdge(e);
                }
            }
        }

        public static void rank(DagreGraph g)
        {
            string res = null;
            if (g.Graph().Ranker != null)
                res = g.Graph().Ranker;
            switch (res)
            {
                case "network-simplex":
                    throw new NotImplementedException();                    
                case "tight-tree":
                    throw new NotImplementedException();                    
                case "longest-path":
                    throw new NotImplementedException();                    
                default:
                    networkSimplexRanker(g);
                    break;
            }
        }
        public static void networkSimplexRanker(DagreGraph g)
        {
            NetworkSimplex.networkSimplex(g);
        }
        /*
 * Creates temporary dummy nodes that capture the rank in which each edge's
 * label is going to, if it has one of non-zero width and height. We do this
 * so that we can safely remove empty ranks while preserving balance for the
 * label's position.
 */
        public static void injectEdgeLabelProxies(DagreGraph g)
        {
            foreach (var e in g.EdgesRaw())
            {
                var edge = g.EdgeRaw(e);
                if (edge.ContainsKey("width") && edge.Width != 0 && edge.ContainsKey("height") && edge.Height != 0)
                {
                    var vNode = g.NodeRaw(e.v);
                    var wNode = g.NodeRaw(e.w);

                    var label = new NodeLabel();
                    label.Rank = (wNode.Rank - vNode.Rank) / 2 + vNode.Rank;
                    label.E = e;
                    Util.addDummyNode(g, "edge-proxy", label, "_ep");
                }
            }
        }

        public static void removeEmptyRanks(DagreGraph g)
        {
            Dictionary<int, object> layers = new Dictionary<int, object>();

            // Ranks may not start at 0, so we need to offset them
            var allNodes = g.NodesRaw();
            int offset = int.MaxValue;
            bool hasRanked = false;
            foreach (var z in allNodes)
            {
                var nl = g.NodeRaw(z);
                if (nl.ContainsKey("rank"))
                {
                    if (nl.Rank < offset) offset = nl.Rank;
                    hasRanked = true;
                }
            }
            if (hasRanked)
            {

                foreach (var v in g.NodesRaw())
                {
                    var nodeLabel = g.NodeRaw(v);
                    if (!nodeLabel.ContainsKey("rank")) continue;
                    var rank = -offset;

                    rank += nodeLabel.Rank;
                    if (!layers.TryGetValue(rank, out var layer))
                    {
                        layer = new List<string>();
                        layers.Add(rank, layer);
                    }
                    ((List<string>)layer).Add(v);
                }
            }

            if (layers.Count > 0 && g.Graph().NodeRankFactor > 0)
            {
                var delta = 0;
                var nodeRankFactor = g.Graph().NodeRankFactor;
                int maxLayer = 0;
                foreach (var k in layers.Keys)
                    if (k > maxLayer) maxLayer = k;
                for (int i = 0; i <= maxLayer; i++)
                {
                    if (!layers.ContainsKey(i) && i % nodeRankFactor != 0)
                    {
                        --delta;
                    }
                    else if (delta != 0)
                    {
                        if (layers.TryGetValue(i, out var layerVals))
                        {
                            var vs = (List<string>)layerVals;
                            foreach (var v in vs)
                            {
                                (g.NodeRaw(v)).Rank += delta;
                            }
                        }
                    }
                }
            }
        }


        public static void runLayout(DagreGraph g, Action<ExtProgressInfo> progress = null)        
        {
            ExtProgressInfo ext = new ExtProgressInfo();

            progress?.Invoke(ext);

            makeSpaceForEdgeLabels(g);
            removeSelfEdges(g);
            Acyclic.run(g);

            NestingGraph.run(g);

            ext.Caption = "rank";
            rank(Util.asNonCompoundGraph(g));

            injectEdgeLabelProxies(g);

            removeEmptyRanks(g);
            NestingGraph.cleanup(g);

            Util.normalizeRanks(g);

            assignRankMinMax(g);

            removeEdgeLabelProxies(g);

            ext.MainProgress = 0.1f;
            progress?.Invoke(ext);
            ext.Caption = "Normalize.run";
            Normalize.run(g);

            ParentDummyChains._parentDummyChains(g);

            AddBorderSegments._addBorderSegments(g);
            ext.Caption = "order";
            ext.MainProgress = 0.3f;
            progress?.Invoke(ext);
            Order._order(g, (f) =>
            {
                ext.AdditionalProgress = f;
                progress?.Invoke(ext);
            });


            ext.MainProgress = 0.5f;
            progress?.Invoke(ext);
            insertSelfEdges(g);

            CoordinateSystem.adjust(g);
            position(g);
            positionSelfEdges(g);
            removeBorderNodes(g);

            ext.Caption = "undo";
            Normalize.undo(g, (f) =>
            {
                ext.AdditionalProgress = f;
                progress?.Invoke(ext);
            });



            fixupEdgeLabelCoords(g);
            CoordinateSystem.undo(g);
            translateGraph(g);
            assignNodeIntersects(g);
            reversePointsForReversedEdges(g);
            Acyclic.undo(g);

            ext.AdditionalProgress = 1;
            ext.MainProgress = 1;
            progress?.Invoke(ext);
        }

        public static void reversePointsForReversedEdges(DagreGraph g)
        {
            foreach (var e in g.Edges())
            {
                var edge = g.Edge(e);
                if (edge.Reversed)
                {
                    edge.Points.Reverse();
                }
            }
        }

        public static void assignNodeIntersects(DagreGraph g)
        {
            foreach (var e in g.Edges())
            {
                var edge = g.Edge(e);
                var nodeV = g.Node(e.v);
                var nodeW = g.Node(e.w);
                DagrePoint p1, p2;
                if (edge.Points == null)
                {
                    edge.Points = new List<DagrePoint>();
                    p1 = new DagrePoint(nodeW.X, nodeW.Y);
                    p2 = new DagrePoint(nodeV.X, nodeV.Y);
                }
                else
                {
                    p1 = edge.Points[0];
                    p2 = edge.Points[edge.Points.Count - 1];
                }
                edge.Points.Insert(0, Util.intersectRect(nodeV, p1));
                edge.Points.Add(Util.intersectRect(nodeW, p2));
            }

        }
        public static void translateGraph(DagreGraph g)
        {
            double minX = double.PositiveInfinity;
            double maxX = 0;
            double minY = double.PositiveInfinity;
            double maxY = 0;
            var graphLabel = g.Graph();
            float marginX = graphLabel.MarginX;
            float marginY = graphLabel.MarginY;

            foreach (var v in g.Nodes())
            {
                var node = g.Node(v);
                minX = Math.Min(minX, node.X - node.Width / 2f);
                maxX = Math.Max(maxX, node.X + node.Width / 2f);
                minY = Math.Min(minY, node.Y - node.Height / 2f);
                maxY = Math.Max(maxY, node.Y + node.Height / 2f);
            }

            foreach (var e in g.Edges())
            {
                var edge = g.Edge(e);
                if (edge.ContainsKey("x"))
                {
                    minX = Math.Min(minX, edge.X - edge.Width / 2f);
                    maxX = Math.Max(maxX, edge.X + edge.Width / 2f);
                    minY = Math.Min(minY, edge.Y - edge.Height / 2f);
                    maxY = Math.Max(maxY, edge.Y + edge.Height / 2f);
                }
            }

            minX -= marginX;
            minY -= marginY;

            foreach (var v in g.Nodes())
            {
                var node = g.Node(v);
                node.X -= (float)minX;
                node.Y -= (float)minY;
            }

            foreach (var e in g.Edges())
            {
                var edge = g.Edge(e);
                if (edge.Points != null)
                {
                    for (int pi = 0; pi < edge.Points.Count; pi++)
                    {
                        var p = edge.Points[pi];
                        edge.Points[pi] = new DagrePoint(p.X - (float)minX, p.Y - (float)minY);
                    }
                }

                if (edge.ContainsKey("x")) { edge.X -= (float)minX; }
                if (edge.ContainsKey("y")) { edge.Y -= (float)minY; }
            }

            graphLabel.Width = maxX - minX + marginX;
            graphLabel.Height = maxY - minY + marginY;
        }
        public static void fixupEdgeLabelCoords(DagreGraph g)
        {
            foreach (var e in g.Edges())
            {
                var edge = g.Edge(e);
                if (edge.ContainsKey("x"))
                {
                    if (edge.LabelPos == "l" || edge.LabelPos == "r")
                    {
                        edge.Width -= edge.LabelOffset;
                    }
                    switch (edge.LabelPos)
                    {
                        case "l": edge.X -= edge.Width / 2f + edge.LabelOffset; break;
                        case "r": edge.X += edge.Width / 2f + edge.LabelOffset; break;
                    }
                }
            }

        }
        public static DagrePoint makePoint(float x, float y)
        {
            return new DagrePoint(x, y);
        }

        public static DagrePoint makePoint(double x, double y)
        {
            return new DagrePoint(x, y);
        }
        public static void positionSelfEdges(DagreGraph g)
        {
            foreach (var v in g.Nodes())
            {
                var node = g.Node(v);
                if (node.Dummy == "selfedge")
                {
                    var edgeObj = (DagreEdgeIndex)node.E;
                    var selfNode = g.Node(edgeObj.v);
                    var x = selfNode.X + selfNode.Width / 2f;
                    var y = selfNode.Y;
                    var dx = node.X - x;
                    var dy = selfNode.Height / 2f;
                    var label = (EdgeLabel)node.Label;
                    g.SetEdge(edgeObj.v, edgeObj.w, label, edgeObj.name);
                    g.RemoveNode(v);
                    label.Points = new List<DagrePoint>{
                        new DagrePoint(x + 2 * dx / 3, y - dy),
                        new DagrePoint(x + 5 * dx / 6, y - dy),
                        new DagrePoint(x + dx, y),
                        new DagrePoint(x + 5 * dx / 6, y + dy),
                        new DagrePoint(x + 2 * dx / 3, y + dy)
                    };
                    label.X = node.X;
                    label.Y = node.Y;
                }
            }
        }

        public static void position(DagreGraph g)
        {
            g = Util.asNonCompoundGraph(g);

            

            var layering = Util.buildLayerMatrix(g);
            var rankSep = g.Graph().RankSep;
            double prevY = 0;
            foreach (var layer in layering)
            {
                float maxHeight = 0;
                foreach (var v in layer)
                {
                    var h = (g.Node(v)).Height;
                    if (h > maxHeight) maxHeight = h;
                }
                foreach (var v in layer)
                {
                    (g.Node(v)).Y = (float)(prevY + maxHeight / 2f);
                }

                prevY += maxHeight + rankSep;
            }
            foreach (var kvp in BrandesKopf.positionX(g))
            {
                (g.Node(kvp.Key)).X = kvp.Value;
            }
        }




        public static void removeBorderNodes(DagreGraph g)
        {
            foreach (var v in g.Nodes())
            {
                if (g.HasChildren(v))
                {
                    var node = g.Node(v);
                    var t = g.Node(node.BorderTop);
                    var b = g.Node(node.BorderBottom);
                    var lastKey1 = node.BorderLeft.Keys.Last();
                    var l = g.Node(node.BorderLeft[lastKey1]);
                    var lastKey2 = node.BorderRight.Keys.Last();
                    var r = g.Node(node.BorderRight[lastKey2]);
                    node.Width = Math.Abs(r.X - l.X);
                    node.Height = Math.Abs(b.Y - t.Y);
                    node.X = l.X + node.Width / 2;
                    node.Y = t.Y + node.Height / 2;
                }
            }

            foreach (var v in g.Nodes())
            {
                var nd = g.Node(v);
                if (nd.Dummy == "border")
                {
                    g.RemoveNode(v);
                }
            }
        }

        public static void insertSelfEdges(DagreGraph g)
        {
            var layers = Util.buildLayerMatrix(g);
            foreach (var layer in layers)
            {
                var orderShift = 0;
                for (int i = 0; i < layer.Length; i++)
                {
                    var v = layer[i];
                    var node = g.Node(v);

                    node.Order = i + orderShift;
                    if (node.SelfEdges != null && node.ContainsKey("selfEdges"))
                    {
                        foreach (var selfEdge in node.SelfEdges)
                        {
                            var selfLabel = (EdgeLabel)selfEdge.label;
                            var attrs = new NodeLabel();
                            attrs.Width = selfLabel.Width;
                            attrs.Height = selfLabel.Height;
                            attrs.Rank = node.Rank;
                            attrs.Order = i + (++orderShift);
                            attrs.E = selfEdge.e;
                            attrs.Label = selfLabel;
                            Util.addDummyNode(g, "selfedge", attrs, "_se");
                        }
                        node.Remove("selfEdges");
                    }
                }
            }
        }

        public static void removeEdgeLabelProxies(DagreGraph g)
        {
            foreach (var v in g.NodesRaw())
            {
                var node = g.NodeRaw(v);
                if (node.Dummy == "edge-proxy")
                {
                    g.EdgeRaw((DagreEdgeIndex)node.E).LabelRank = node.Rank;
                    g.RemoveNode(v);
                }
            }

        }

        public static void assignRankMinMax(DagreGraph g)
        {
            int maxRank = 0;
            foreach (var v in g.NodesRaw())
            {
                var node = g.NodeRaw(v);
                if (node.BorderTop != null)
                {
                    node.MinRank = (g.NodeRaw(node.BorderTop)).Rank;
                    node.MaxRank = (g.NodeRaw(node.BorderBottom)).Rank;
                    maxRank = Math.Max(maxRank, node.MaxRank);
                }
            }

            g.Graph().MaxRank = maxRank;
        }
    }
}
