using System;
using System.Collections.Generic;

namespace Dagre
{
    public class CoordinateSystem
    {
        public static void undo(DagreGraph g)
        {
            var rankDir = g.Graph().RankDir.ToLower();
            if (rankDir == "bt" || rankDir == "rl")
            {
                reverseY(g);
            }

            if (rankDir == "lr" || rankDir == "rl")
            {
                swapXY(g);
                swapWidthHeight(g);
            }
        }

        public static void reverseY(DagreGraph g)
        {
            foreach (var v in g.Nodes())
            {
                var node = g.Node(v);
                node.Y = -node.Y;
            }
            foreach (var e in g.Edges())
            {
                var edge = g.Edge(e);
                if (edge.Points != null)
                {
                    for (int pi = 0; pi < edge.Points.Count; pi++)
                    {
                        var p = edge.Points[pi];
                        edge.Points[pi] = new DagrePoint(p.X, -p.Y);
                    }
                }

                if (edge.ContainsKey("y"))
                {
                    edge.Y = -edge.Y;
                }
            }
        }

        public static void swapXY(DagreGraph g)
        {
            foreach (var v in g.Nodes())
            {
                var node = g.Node(v);
                var x = node.X;
                node.X = node.Y;
                node.Y = x;
            }

            foreach (var e in g.Edges())
            {
                var edge = g.Edge(e);
                if (edge.Points != null)
                {
                    for (int pi = 0; pi < edge.Points.Count; pi++)
                    {
                        var p = edge.Points[pi];
                        edge.Points[pi] = new DagrePoint(p.Y, p.X);
                    }
                }

                if (edge.ContainsKey("x"))
                {
                    var x = edge.X;
                    edge.X = edge.Y;
                    edge.Y = x;
                }
            }
        }

        public static void adjust(DagreGraph g)
        {
            var rankDir = g.Graph().RankDir.ToLower();
            if (rankDir == "lr" || rankDir == "rl")
            {
                swapWidthHeight(g);
            }
        }

        public static void swapWidthHeight(DagreGraph g)
        {
            foreach (var v in g.Nodes())
            {
                var node = g.Node(v);
                var w = node.Width;
                node.Width = node.Height;
                node.Height = w;
            }
            foreach (var e in g.Edges())
            {
                var edge = g.Edge(e);
                var w = edge.Width;
                edge.Width = edge.Height;
                edge.Height = w;
            }
        }

    }
}
