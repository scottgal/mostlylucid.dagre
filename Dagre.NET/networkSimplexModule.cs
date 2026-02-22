using System;
using System.Collections.Generic;
using System.Linq;

namespace Dagre
{
    public class NetworkSimplex
    {
        public static void initLowLimValues(DagreGraph tree, string root = null)
        {
            if (root == null)
            {
                root = tree.NodesRaw()[0];
            }

            dfsAssignLowLim(tree, new HashSet<string>(), 1, root);
        }

        public static int dfsAssignLowLim(DagreGraph tree, HashSet<string> visited, int nextLim, string v, string parent = null)
        {
            var low = nextLim;
            var label = tree.NodeRaw(v);

            visited.Add(v);
            foreach (var w in tree.Neighbors(v))
            {
                if (!visited.Contains(w))
                {
                    nextLim = dfsAssignLowLim(tree, visited, nextLim, w, v);
                }
            }

            label.Low = low;
            label.Lim = nextLim++;
            if (parent != null)
            {
                label.Parent = parent;
            }
            else
            {
                label.Parent = null;
            }

            return nextLim;
        }

        public static string[] postorder(DagreGraph t, string[] g)
        {
            return GraphLib.dfs(t, g, "post");
        }
        public static string[] preorder(DagreGraph t, string[] g)
        {
            return GraphLib.dfs(t, g, "pre");
        }

        public static void initCutValues(DagreGraph t, DagreGraph g)
        {
            var vs = postorder(t, t.NodesRaw());
            // Skip the last element (root) — iterate up to Length-1
            for (int i = 0; i < vs.Length - 1; i++)
            {
                assignCutValue(t, g, vs[i]);
            }
        }

        public static void assignCutValue(DagreGraph t, DagreGraph g, string child)
        {
            var childLab = t.NodeRaw(child);
            var parent = childLab.Parent;
            var edge = t.EdgeRaw(child, parent);
            if (edge != null)
            {
                var res = calcCutValue(t, g, child);
                ((EdgeLabel)edge).Cutvalue = res;
            }
        }

        public static int calcCutValue(DagreGraph t, DagreGraph g, string child)
        {
            var childLab = t.NodeRaw(child);
            var parent = childLab.Parent;
            var childIsTail = true;
            var graphEdge = g.EdgeRaw(child, parent);
            var cutValue = 0;

            if (graphEdge == null)
            {
                childIsTail = false;
                graphEdge = g.EdgeRaw(parent, child);
            }

            cutValue = graphEdge.Weight;

            foreach (var e in g.NodeEdges(child))
            {
                var isOutEdge = e.v == child;
                var other = isOutEdge ? e.w : e.v;
                if (other != parent)
                {
                    var pointsToHead = isOutEdge == childIsTail;
                    var otherWeight = (g.EdgeRaw(e)).Weight;

                    cutValue += pointsToHead ? otherWeight : -otherWeight;
                    if (isTreeEdge(t, child, other))
                    {
                        var otherCutValue = (t.EdgeRaw(child, other)).Cutvalue;
                        cutValue += pointsToHead ? -otherCutValue : otherCutValue;
                    }
                }
            }

            return cutValue;
        }

        public static bool isTreeEdge(DagreGraph tree, string u, string v)
        {
            return tree.EdgeRaw(u, v) != null;
        }

        public static DagreEdgeIndex enterEdge(DagreGraph t, DagreGraph g, DagreEdgeIndex edge)
        {
            var v = edge.v;
            var w = edge.w;

            if (g.EdgeRaw(v, w) == null)
            {
                v = edge.w;
                w = edge.v;
            }

            var vLabel = t.NodeRaw(v);
            var wLabel = t.NodeRaw(w);
            var tailLabel = vLabel;
            var flip = false;

            if (vLabel.Lim > wLabel.Lim)
            {
                tailLabel = wLabel;
                flip = true;
            }

            // Single-pass min-slack scan instead of Where+OrderBy+First
            DagreEdgeIndex best = null;
            int? bestSlack = null;
            foreach (var ee in g.EdgesRaw())
            {
                if (flip == isDescendant(t, t.NodeRaw(ee.v), tailLabel) &&
                    flip != isDescendant(t, t.NodeRaw(ee.w), tailLabel))
                {
                    var s = slack(g, ee);
                    if (best == null || (s != null && (bestSlack == null || s < bestSlack)))
                    {
                        best = ee;
                        bestSlack = s;
                    }
                }
            }
            return best;
        }

        public static bool isDescendant(DagreGraph tree, NodeLabel vLabel, NodeLabel rootLabel)
        {
            return rootLabel.Low <= vLabel.Lim && vLabel.Lim <= rootLabel.Lim;
        }

        public static void networkSimplex(DagreGraph g)
        {
            g = Util.simplify(g);

            longestPath(g);

            var tree = feasibleTree(g);

            initLowLimValues(tree);

            initCutValues(tree, g);

            DagreEdgeIndex e = null, f = null;
            int step = 0;
            while ((e = leaveEdge(tree)) != null)
            {
                f = enterEdge(tree, g, e);
                exchangeEdges(tree, g, e, f, step);
                step++;
            }
        }

        public static void exchangeEdges(DagreGraph t, DagreGraph g, DagreEdgeIndex e, DagreEdgeIndex f, int step)
        {
            t.RemoveEdge(e.v, e.w);
            t.SetEdge(f.v, f.w, new EdgeLabel());

            initLowLimValues(t);
            initCutValues(t, g);
            updateRanks(t, g);
        }

        public static DagreEdgeIndex leaveEdge(DagreGraph tree)
        {
            foreach (var e in tree.EdgesRaw())
            {
                var edge = tree.EdgeRaw(e);
                if (edge != null && edge.Cutvalue < 0)
                    return e;
            }
            return null;
        }

        public static void updateRanks(DagreGraph t, DagreGraph g)
        {
            string root = null;
            foreach (var v in t.Nodes())
            {
                var nl = g.Node(v);
                if (nl.Parent == null) { root = v; break; }
            }
            var vs = preorder(t, new string[] { root });
            for (int i = 1; i < vs.Length; i++)
            {
                var v = vs[i];
                var tNodeLabel = t.Node(v);
                var parent = tNodeLabel.Parent;

                var edge = g.EdgeRaw(v, parent);
                var flipped = false;

                if (edge == null)
                {
                    edge = g.EdgeRaw(parent, v);
                    flipped = true;
                }

                var gNode = g.Node(v);
                var gParent = g.Node(parent);
                gNode.Rank = gParent.Rank + (flipped ? edge.Minlen : -edge.Minlen);
            }
        }

        public static void longestPath(DagreGraph g)
        {
            HashSet<string> visited = new HashSet<string>();

            Func<string, int> dfs = null;
            dfs = (v) =>
            {
                var label = g.NodeRaw(v);
                if (visited.Contains(v))
                {
                    return label.Rank;
                }
                visited.Add(v);
                var rank = int.MaxValue;
                foreach (var e in g.OutEdges(v))
                {
                    var edgeLabel = g.EdgeRaw(e);
                    var x = dfs(e.w) - edgeLabel.Minlen;
                    if (x < rank)
                    {
                        rank = x;
                    }
                }

                if (rank == int.MaxValue)
                {
                    rank = 0;
                }

                label.Rank = rank;
                return label.Rank;
            };

            foreach (var item in g.Sources())
            {
                dfs(item);
            }
        }

        public static int? slack(DagreGraph g, DagreEdgeIndex e)
        {
            var node1 = g.NodeRaw(e.w) as NodeLabel;
            var node2 = g.NodeRaw(e.v) as NodeLabel;
            var edge = g.EdgeRaw(e);
            if (node1 == null || node2 == null || edge == null) return null;
            return node1.Rank - node2.Rank - edge.Minlen;
        }

        public static DagreGraph feasibleTree(DagreGraph g)
        {
            var t = new DagreGraph(false) { _isDirected = false };

            var start = g.NodesRaw()[0];
            var size = g.NodeCount();
            t.SetNode(start, new NodeLabel());

            DagreEdgeIndex edge;
            int delta;
            while (tightTree(t, g) < size)
            {
                edge = findMinSlackEdge(t, g);
                delta = t.HasNode(edge.v) ? (int)slack(g, edge) : -(int)slack(g, edge);
                shiftRanks(t, g, delta);
            }

            return t;
        }

        public static int tightTree(DagreGraph t, DagreGraph g)
        {
            var nodes = t.NodesRaw();
            var stack = new List<string>(nodes.Length);
            for (int i = nodes.Length - 1; i >= 0; i--)
                stack.Add(nodes[i]);
            while (stack.Count > 0)
            {
                var v = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                foreach (var e in g.NodeEdges(v))
                {
                    var edgeV = e.v;
                    var w = (v == edgeV) ? e.w : edgeV;
                    var _slack = slack(g, e);
                    if (!t.HasNode(w) && (_slack == null || _slack == 0))
                    {
                        t.SetNode(w, new NodeLabel());
                        t.SetEdge(v, w, new EdgeLabel());
                        stack.Add(w);
                    }
                }
            }
            return t.NodeCount();
        }

        public static DagreEdgeIndex findMinSlackEdge(DagreGraph t, DagreGraph g)
        {
            DagreEdgeIndex best = null;
            int? bestSlack = null;
            foreach (var e in g.Edges())
            {
                if (t.HasNode(e.v) != t.HasNode(e.w))
                {
                    var s = slack(g, e);
                    if (best == null || (s != null && (bestSlack == null || s < bestSlack)))
                    {
                        best = e;
                        bestSlack = s;
                    }
                }
            }
            return best;
        }

        public static void shiftRanks(DagreGraph t, DagreGraph g, int delta)
        {
            foreach (var v in t.Nodes())
            {
                (g.Node(v)).Rank += delta;
            }
        }
    }

    public class GraphLib
    {
        public static string[] dfs(DagreGraph g, string[] vs, string order)
        {
            HashSet<string> visited = new HashSet<string>();

            Func<string, string[]> navigation;
            if (g._isDirected)
                navigation = (u) =>
                {
                    var s = g.Successors(u);
                    Array.Sort(s, StringComparer.Ordinal);
                    return s;
                };
            else
                navigation = (u) =>
                {
                    var n = g.Neighbors(u);
                    Array.Sort(n, StringComparer.Ordinal);
                    return n;
                };

            List<string> acc = new List<string>();

            foreach (var v in vs)
            {
                if (!g.HasNode(v))
                {
                    throw new DagreException("graph does not have node: " + v);
                }
                doDfs(g, v, order == "post", visited, navigation, acc);
            }
            return acc.ToArray();
        }

        public static void doDfs(DagreGraph g, string v, bool postorder, HashSet<string> visited, Func<string, string[]> navigation, List<string> acc)
        {
            if (visited.Contains(v)) return;

            visited.Add(v);
            if (!postorder) { acc.Add(v); }
            foreach (var w in navigation(v))
            {
                doDfs(g, w, postorder, visited, navigation, acc);
            }
            if (postorder)
            {
                acc.Add(v);
            }
        }
    }
}
