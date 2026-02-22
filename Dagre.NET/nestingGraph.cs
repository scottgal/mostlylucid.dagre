using System;
using System.Collections.Generic;
using System.Linq;

namespace Dagre
{
    public class NestingGraph
    {

        /*
         * A nesting graph creates dummy nodes for the tops and bottoms of subgraphs,
         * adds appropriate edges to ensure that all cluster nodes are placed between
         * these boundries, and ensures that the graph is connected.
         *
         * In addition we ensure, through the use of the minlen property, that nodes
         * and subgraph border nodes to not end up on the same rank.
         *
         * Preconditions:
         *
         *    1. Input graph is a DAG
         *    2. Nodes in the input graph has a minlen attribute
         *
         * Postconditions:
         *
         *    1. Input graph is connected.
         *    2. Dummy nodes are added for the tops and bottoms of subgraphs.
         *    3. The minlen attribute for nodes is adjusted to ensure nodes do not
         *       get placed on the same rank as subgraph border nodes.
         *
         * The nesting graph idea comes from Sander, "Layout of Compound Directed
         * Graphs."
         */
        public static void run(DagreGraph g)
        {
            var root = Util.addDummyNode(g, "root", new NodeLabel(), "_root");
            var depths = treeDepths(g);
            Dictionary<string, int> d = new Dictionary<string, int>();

            var height = depths.Values.Max() - 1;// Note: depths is an Object not an array
            var nodeSep = 2 * height + 1;

            g.Graph().NestingRoot = root;


            // Multiply minlen by nodeSep to align nodes on non-border ranks.
            foreach (var e in g.EdgesRaw())
            {
                var edge = g.EdgeRaw(e);
                edge.Minlen = edge.Minlen * nodeSep;
            }

            // Calculate a weight that is sufficient to keep subgraphs vertically compact
            var weight = sumWeights(g) + 1;

            // Create border nodes and link them up
            foreach (var child in g.Children())
            {
                dfs(g, root, nodeSep, weight, height, depths, child);
            }


            // Save the multiplier for node layers for later removal of empty border
            // layers.
            g.Graph().NodeRankFactor = nodeSep;
        }



        public static void cleanup(DagreGraph g)
        {
            var graphLabel = g.Graph();
            g.RemoveNode(graphLabel.NestingRoot);
            graphLabel.NestingRoot = null;
            

            foreach (var e in g.EdgesRaw())
            {
                var edge = g.EdgeRaw(e);
                if (edge.NestingEdge)
                {
                    g.RemoveEdge(e);
                }
            }

        }
        static NodeLabel generateEmptyWidHei()
        {
            var ret = new NodeLabel();
            ret.Width = 0f;
            ret.Height = 0f;
            return ret;
        }
        public static void dfs(DagreGraph g, string root, int nodeSep, int weight, int height, Dictionary<string, int> depths, string v)
        {
            var children = g.Children(v);
            if (children == null || children.Length == 0)
            {
                if (v != root)
                {
                    var arg = new EdgeLabel();
                    arg.Weight = 0;
                    arg.Minlen = nodeSep;
                    g.SetEdge(root, v, arg);
                }
                return;
            }


            var top = Util.addDummyNode(g, "border", generateEmptyWidHei(), "_bt");
            var bottom = Util.addDummyNode(g, "border", generateEmptyWidHei(), "_bb");
            var label = g.NodeRaw(v);
            g.SetParent(top, v);
            label.BorderTop = top;

            g.SetParent(bottom, v);
            label.BorderBottom = bottom;

            foreach (var child in children)
            {
                dfs(g, root, nodeSep, weight, height, depths, child);
                var childNode = g.Node(child);
                var childTop = childNode.BorderTop != null ? childNode.BorderTop : child;
                var childBottom = childNode.BorderBottom != null ? childNode.BorderBottom : child;
                var thisWeight = childNode.BorderTop != null ? weight : 2 * weight;
                var minlen = childTop != childBottom ? 1 : height - depths[v] + 1;
                var j1 = new EdgeLabel();
                j1.Weight = thisWeight;
                j1.Minlen = minlen;
                j1.NestingEdge = true;
                g.SetEdge(top, childTop, j1);
                var j2 = new EdgeLabel();
                j2.Weight = thisWeight;
                j2.Minlen = minlen;
                j2.NestingEdge = true;
                g.SetEdge(childBottom, bottom, j2);
            }
            if (g.Parent(v) == null)
            {
                var j2 = new EdgeLabel();
                j2.Weight = 0;
                j2.Minlen = height + depths[v];
                g.SetEdge(root, top, j2);
            }
        }

        public static int sumWeights(DagreGraph g)
        {
            return g.EdgesRaw().Sum(z => (g.EdgeRaw(z)).Weight);
        }

        public static Dictionary<string, int> treeDepths(DagreGraph g)
        {
            Dictionary<string, int> depths = new Dictionary<string, int>();
            Action<string, int> dfs = null;
            dfs = (v, depth) =>
            {
                var children = g.Children(v);
                if (children != null && children.Length > 0)
                {
                    foreach (var child in children)
                    {
                        dfs(child, depth + 1);
                    }
                }
                depths[v] = depth;
            };

            foreach (var v in g.Children())
            {
                dfs(v, 1);
            }
            return depths;
        }
    }

}
