using System;
using System.Collections.Generic;
using System.Linq;

namespace Dagre
{
    public static class ParentDummyChains
    {
        struct PathResult
        {
            public string[] Path;
            public string Lca;
        }

        struct PostorderEntry
        {
            public int Low;
            public int Lim;
        }

        static void dummyChainIteration(DagreGraph g, string v, Dictionary<string, PostorderEntry> postorderNums)
        {
            var node = g.NodeRaw(v);
            var edgeObj = (DagreEdgeIndex)node.EdgeObj;
            var pathData = findPath(g, postorderNums, edgeObj.v, edgeObj.w);
            var path = pathData.Path;
            var lca = pathData.Lca;
            var pathIdx = 0;
            var pathV = path[pathIdx];
            var ascending = true;
            while (v != edgeObj.w)
            {
                node = g.NodeRaw(v);
                if (ascending)
                {
                    while ((pathV = path[pathIdx]) != lca && (g.Node(pathV)).MaxRank < node.Rank)
                    {
                        pathIdx++;
                    }
                    if (pathV == lca)
                    {
                        ascending = false;
                    }
                }
                if (!ascending)
                {
                    while (pathIdx < path.Length - 1 && (g.Node(pathV = path[pathIdx + 1])).MinRank <= node.Rank)
                    {
                        pathIdx++;
                    }
                    pathV = path[pathIdx];
                }
                g.SetParent(v, pathV);
                v = g.FirstSuccessor(v);
            }
        }
        public static void _parentDummyChains(DagreGraph g)
        {
            var postorderNums = postorder(g);
            var dummyChains = g.Graph().DummyChains;
            if (dummyChains != null && dummyChains.Count > 0)
            {
                foreach (var v in dummyChains)
                {
                    dummyChainIteration(g, v, postorderNums);
                }
            }
        }

        // Find a path from v to w through the lowest common ancestor (LCA). Return the
        // full path and the LCA.
        static PathResult findPath(DagreGraph g, Dictionary<string, PostorderEntry> postorderNums, string v, string w)
        {
            var vPath = new List<string>();
            var wPath = new List<string>();
            var vNums = postorderNums[v];
            var wNums = postorderNums[w];
            var low = Math.Min(vNums.Low, wNums.Low);
            var lim = Math.Max(vNums.Lim, wNums.Lim);
            // Traverse up from v to find the LCA
            string parent = v;
            do
            {
                parent = g.Parent(parent);
                vPath.Add(parent);
            }
            while (parent != null && (postorderNums[parent].Low > low || lim > postorderNums[parent].Lim));
            var lca = parent;
            // Traverse from w to LCA
            parent = w;
            while ((parent = g.Parent(parent)) != lca)
            {
                wPath.Add(parent);
            }
            wPath.Reverse();
            var combined = new string[vPath.Count + wPath.Count];
            vPath.CopyTo(combined, 0);
            wPath.CopyTo(combined, vPath.Count);
            return new PathResult { Path = combined, Lca = lca };
        }

        static Dictionary<string, PostorderEntry> postorder(DagreGraph g)
        {
            var result = new Dictionary<string, PostorderEntry>(StringComparer.Ordinal);
            var lim = 0;
            Action<string> dfs = null;
            dfs = (v) =>
            {
                var low = lim;
                foreach (var item in g.Children(v))
                {
                    dfs(item);
                }
                result.Add(v, new PostorderEntry { Low = low, Lim = lim++ });
            };
            foreach (var item in g.Children())
            {
                dfs(item);
            }
            return result;
        }
    }
}
