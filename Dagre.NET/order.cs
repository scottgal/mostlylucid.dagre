using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace Dagre
{
    public class Order
    {

        /*
         * Applies heuristics to minimize edge crossings in the graph and sets the best
         * order solution as an order attribute on each node.
         *
         * Pre-conditions:
         *
         *    1. Graph must be DAG
         *    2. Graph nodes must be objects with a "rank" attribute
         *    3. Graph edges must have the "weight" attribute
         *
         * Post-conditions:
         *
         *    1. Graph nodes will have an "order" attribute based on the results of the
         *       algorithm.
         */

        public static void _order(DagreGraph g, Action<float> progress = null)
        {
            var rank = Util.maxRank(g);

            // Build layer graphs concurrently
            var downArr = new DagreGraph[rank];
            var upArr = new DagreGraph[rank];
            Parallel.ForEachAsync(Enumerable.Range(0, rank), async (i, _) =>
            {
                downArr[i] = buildLayerGraph(g, i + 1, "inEdges");
                upArr[i] = buildLayerGraph(g, rank - i - 1, "outEdges");
                await Task.CompletedTask;
            }).GetAwaiter().GetResult();
            // downArr and upArr are already arrays; no need for List wrappers

            progress?.Invoke(0.5f);

            var layering = initOrder(g);
            assignOrder(g, layering);

            float bestCC = float.PositiveInfinity;
            string[][] best = null;


            for (int i = 0, lastBest = 0; lastBest < 4; ++i, ++lastBest)
            {
                sweepLayerGraphs((i % 2 != 0) ? downArr : upArr, i % 4 >= 2);

                var rawLayering = Util.buildLayerMatrix(g);
                var cc = crossCount(g, rawLayering);
                if (cc < bestCC)
                {
                    lastBest = 0;
                    best = rawLayering.ToArray();
                    bestCC = cc;
                }
            }

            assignOrder(g, best);

        }

        public static string[] reorderKeys(string[] ret)
        {
            // Partition in-place: digits first, then non-digits
            int digitEnd = 0;
            for (int i = 0; i < ret.Length; i++)
            {
                if (ret[i].All(char.IsDigit))
                {
                    if (i != digitEnd)
                        (ret[digitEnd], ret[i]) = (ret[i], ret[digitEnd]);
                    digitEnd++;
                }
            }
            // Sort digit portion by numeric value
            Array.Sort(ret, 0, digitEnd, Comparer<string>.Create((x, y) => int.Parse(x) - int.Parse(y)));
            // Sort non-digit portion lexicographically
            Array.Sort(ret, digitEnd, ret.Length - digitEnd, StringComparer.Ordinal);
            return ret;
        }

        public static void sweepLayerGraphs(DagreGraph[] layerGraphs, bool biasRight)
        {
            var cg = new DagreGraph(false);
            foreach (var lg in layerGraphs)
            {
                var root = lg.Graph().Root;
                var sorted = SortSubGraph.sortSubraph(lg as DagreGraph, root, cg, biasRight);
                var vs = sorted.Vs;
                var length = vs.Count;
                for (var i = 0; i < length; i++)
                {
                    var vv = vs[i];
                    ((lg as DagreGraph).Node(vv)).Order = i;
                }
                addSubgraphConstraints(lg, cg, sorted.Vs);
            }

        }
        public static void addSubgraphConstraints(DagreGraph g, DagreGraph cg, List<string> vs)
        {
            var prev = new Dictionary<string, string>(StringComparer.Ordinal);
            string rootPrev = null;
            foreach (var v in vs)
            {
                var child = g.Parent(v);
                string parent = null;
                string prevChild = null;
                while (child != null)
                {
                    parent = g.Parent(child);
                    if (parent != null)
                    {
                        prevChild = null;
                        if (prev.TryGetValue(parent, out var pc))
                            prevChild = pc;
                        prev[parent] = child;
                    }
                    else
                    {
                        prevChild = rootPrev;
                        rootPrev = child;
                    }
                    if (prevChild != null && prevChild != child)
                    {
                        cg.SetEdge(prevChild, child);
                        return;
                    }
                    child = parent;
                }
            }
        }
        public static void assignOrder(DagreGraph g, string[][] layering)
        {
            foreach (var layer in layering)
            {
                for (int i = 0; i < layer.Length; i++)
                {
                    (g.Node(layer[i])).Order = i;
                }
            }
        }

        #region buildLayerGraph

        /*
         * Constructs a graph that can be used to sort a layer of nodes. The graph will
         * contain all base and subgraph nodes from the request layer in their original
         * hierarchy and any edges that are incident on these nodes and are of the type
         * requested by the "relationship" parameter.
         *
         * Nodes from the requested rank that do not have parents are assigned a root
         * node in the output graph, which is set in the root graph attribute. This
         * makes it easy to walk the hierarchy of movable nodes during ordering.
         *
         * Pre-conditions:
         *
         *    1. Input graph is a DAG
         *    2. Base nodes in the input graph have a rank attribute
         *    3. Subgraph nodes in the input graph has minRank and maxRank attributes
         *    4. Edges have an assigned weight
         *
         * Post-conditions:
         *
         *    1. Output graph has all nodes in the movable rank with preserved
         *       hierarchy.
         *    2. Root nodes in the movable layer are made children of the node
         *       indicated by the root attribute of the graph.
         *    3. Non-movable nodes incident on movable nodes, selected by the
         *       relationship parameter, are included in the graph (without hierarchy).
         *    4. Edges incident on movable nodes, selected by the relationship
         *       parameter, are added to the output graph.
         *    5. The weights for copied edges are aggregated as need, since the output
         *       graph is not a multi-graph.
         */
        public static DagreGraph buildLayerGraph(DagreGraph g, int rank, string relationship)
        {
            string root;
            while (true)
            {
                root = Util.uniqueId("_root");
                if (!g.HasNode(root)) break;
            }
            var graph = new DagreGraph(true) { _isCompound = true };
            graph.Graph().Root = root;
            graph.SetDefaultNodeLabel((v) => g.Node(v));
            foreach (var v in g.Nodes())
            {
                var node = g.Node(v);
                RankTag rtag = null;

                if (node.RankTag == null)
                {
                    rtag = node.RankTag = new RankTag();
                    if (node.ContainsKey("rank"))
                        rtag.Rank = node.Rank;
                    if (node.ContainsKey("minRank"))
                        rtag.MinRank = node.MinRank;
                    if (node.ContainsKey("maxRank"))
                        rtag.MaxRank = node.MaxRank;
                }
                rtag = node.RankTag;
                if ((rtag.Rank != null && rtag.Rank == rank) || (rtag.MinRank != null && rtag.MaxRank != null && rtag.MinRank <= rank && rank <= rtag.MaxRank))
                {
                    graph.SetNode(v);
                    var parent = g.Parent(v);
                    graph.SetParent(v, parent ?? root);
                    // This assumes we have only short edges!
                    DagreEdgeIndex[] rr = null;
                    if (relationship == "inEdges")
                    {
                        rr = g.InEdges(v);
                    }
                    else if (relationship == "outEdges")
                    {
                        rr = g.OutEdges(v);
                    }
                    else
                    {
                        throw new DagreException();
                    }
                    foreach (var e in rr)
                    {
                        var u = e.v == v ? e.w : e.v;
                        var existingEdge = graph.EdgeRaw(u, v) as EdgeLabel;
                        var weight = existingEdge != null ? existingEdge.Weight : 0;
                        var edgeLabel = g.Edge(e);
                        var j = new EdgeLabel();
                        j["weight"] = edgeLabel.Weight + weight;
                        graph.SetEdge(u, v, j);
                    }
                    if (rtag.MinRank != null)
                    {
                        var jj = new NodeLabel();
                        var rankStr = rank.ToString();
                        // Store border node IDs as overflow strings for layer graph consumption
                        // (sortSubGraphModule reads these via TryGetValue + 'as string')
                        if (node.BorderLeft != null && node.BorderLeft.TryGetValue(rankStr, out var bl))
                            jj.Add("_borderLeft", bl);
                        if (node.BorderRight != null && node.BorderRight.TryGetValue(rankStr, out var br))
                            jj.Add("_borderRight", br);

                        graph.SetNode(v, jj);
                    }
                }
            }
            return graph;
        }

        public static void createRootNode(DagreGraph g)
        {
        }
        #endregion

        public static void initOrderDfs(DagreGraph g, List<List<string>> layers, HashSet<string> visited, string v)
        {
            if (!visited.Add(v)) return;

            var node = g.Node(v);
            if (node == null || !node.ContainsKey("rank")) return;

            var rank = node.Rank;
            if (rank >= 0 && rank < layers.Count)
                layers[rank].Add(v);

            var successors = g.Successors(v);
            if (successors != null)
            {
                foreach (var item in successors)
                {
                    initOrderDfs(g, layers, visited, item);
                }
            }
        }

        #region init order
        /*
        * Assigns an initial order value for each node by performing a DFS search
        * starting from nodes in the first rank. Nodes are assigned an order in their
        * rank as they are first visited.
        *
        * This approach comes from Gansner, et al., "A Technique for Drawing Directed
        * Graphs."
        *
        * Returns a layering matrix with an array per layer and each layer sorted by
        * the order of its nodes.
        */
        public static string[][] initOrder(DagreGraph g)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var allNodes = g.Nodes();
            var nodes = new List<string>(allNodes.Length);
            foreach (var v in allNodes)
            {
                if (g.Children(v).Length == 0) nodes.Add(v);
            }
            // Compute maxRank from ALL nodes (not just leaves) because
            // initOrderDfs traverses successors which may include non-leaf nodes
            int maxRank = -1;
            bool hasRank = false;
            foreach (var v in allNodes)
            {
                var nodeLabel = g.Node(v);
                if (nodeLabel != null && nodeLabel.ContainsKey("rank"))
                {
                    var rank = nodeLabel.Rank;
                    if (!hasRank || rank > maxRank)
                    {
                        maxRank = rank;
                        hasRank = true;
                    }
                }
            }
            if (hasRank)
            {
                var layers = new List<List<string>>(maxRank + 1);
                for (int i = 0; i < maxRank + 1; i++)
                {
                    layers.Add(new List<string>());
                }
                // Group nodes by rank and sort by rank, then DFS
                var dicc = new Dictionary<int, List<string>>();
                foreach (var v in nodes)
                {
                    var nodeRank = (g.Node(v)).Rank;
                    if (!dicc.TryGetValue(nodeRank, out var rankList))
                    {
                        rankList = new List<string>();
                        dicc[nodeRank] = rankList;
                    }
                    rankList.Add(v);
                }
                // Sort keys and flatten — avoid LINQ OrderBy in hot path
                var sortedKeys = new List<int>(dicc.Keys);
                sortedKeys.Sort();
                foreach (var key in sortedKeys)
                {
                    foreach (var v in dicc[key])
                    {
                        initOrderDfs(g, layers, visited, v);
                    }
                }

                var result = new string[layers.Count][];
                for (int i = 0; i < layers.Count; i++)
                    result[i] = layers[i].ToArray();
                return result;
            }
            return Array.Empty<string[]>();
        }

        #endregion

        #region cross-count

        /*
         * A function that takes a layering (an array of layers, each with an array of
         * ordererd nodes) and a graph and returns a weighted crossing count.
         *
         * Pre-conditions:
         *
         *    1. Input graph must be simple (not a multigraph), directed, and include
         *       only simple edges.
         *    2. Edges in the input graph must have assigned weights.
         *
         * Post-conditions:
         *
         *    1. The graph and layering matrix are left unchanged.
         *
         * This algorithm is derived from Barth, et al., "Bilayer Cross Counting."
         */
        public static int crossCount(DagreGraph g, List<string[]> layering)
        {
            var cc = 0;
            for (var i = 1; i < layering.Count; ++i)
            {
                cc += twoLayerCrossCount(g, layering[i - 1], layering[i]);
            }
            return cc;
        }

        public static int twoLayerCrossCount(DagreGraph g, string[] northLayer, string[] southLayer)
        {
            // Sort all of the edges between the north and south layers by their position
            // in the north layer and then the south. Map these edges to the position of
            // their head in the south layer.
            int southCount = southLayer.Length;
            var southPos = new Dictionary<string, int>(southCount, StringComparer.Ordinal);
            for (int idx = 0; idx < southLayer.Length; idx++)
            {
                southPos[southLayer[idx]] = idx;
            }

            var southEntries = new List<CrossEntry>();
            foreach (var v in northLayer)
            {
                var outEdges = g.OutEdges(v);
                var entries = new List<CrossEntry>();
                foreach (var e in outEdges)
                {
                    if (southPos.TryGetValue(e.w, out var pos))
                    {
                        entries.Add(new CrossEntry
                        {
                            Pos = pos,
                            Weight = (g.Edge(e)).Weight
                        });
                    }
                }
                entries.Sort((a, b) => a.Pos - b.Pos);
                southEntries.AddRange(entries);
            }

            // Build the accumulator tree
            var firstIndex = 1;
            while (firstIndex < southCount)
            {
                firstIndex <<= 1;
            }
            var tree = new int[2 * firstIndex - 1];
            firstIndex -= 1;
            // Calculate the weighted crossings
            var cc = 0;
            foreach (var entry in southEntries)
            {
                var index = entry.Pos + firstIndex;
                var w = entry.Weight;
                tree[index] += w;
                int weightSum = 0;
                while (index > 0)
                {
                    if ((index & 1) != 0)
                    {
                        weightSum += tree[index + 1];
                    }
                    index = (index - 1) >> 1;
                    tree[index] += w;
                }
                cc += w * weightSum;
            }
            return cc;
        }

        struct CrossEntry
        {
            public int Pos;
            public int Weight;
        }
        #endregion
    }
    public class BarycenterResult
    {

        public string v;
        public int? barycenter = null;
        public int weight;
    }
    public class RankTag
    {
        public int? Rank;
        public int? MinRank;
        public int? MaxRank;
    }
}
