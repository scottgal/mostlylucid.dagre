using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dagre
{

    public class Acyclic
    {

        public static void undo(DagreGraph g)
        {
            foreach (var e in g.Edges())
            {
                var label = g.Edge(e);
                if (label.Reversed)
                {
                    g.RemoveEdge(e);

                    var forwardName = label.ForwardName;
                    label.Reversed = false;
                    label.ForwardName = null;

                    g.SetEdge(e.w, e.v, label, forwardName);
                }
            }

        }

        public static Func<DagreEdgeIndex, int> weightFn(DagreGraph g)
        {
            return (e) => g.Edge(e).Weight;
        }
        public static void run(DagreGraph g)
        {
            var cyclicer = g.Graph().Acyclicer ?? "";
            var fas = (cyclicer == "greedy"
   ? greedyFAS(g, weightFn(g))
   : dfsFAS(g));
            foreach (var e in fas)
            {
                var label = g.Edge(e);
                g.RemoveEdge(e);
                label.ForwardName = e.name;
                label.Reversed = true;

                g.SetEdge(e.w, e.v, label, Util.uniqueId("rev"));

            }
        }

        public static DagreEdgeIndex[] greedyFAS(DagreGraph g, Func<DagreEdgeIndex, int> wf)
        {
            throw new NotImplementedException();
        }
        public static DagreEdgeIndex[] dfsFAS(DagreGraph g)
        {
            HashSet<string> visited = new HashSet<string>();
            List<DagreEdgeIndex> fas = new List<DagreEdgeIndex>();
            HashSet<string> stack = new HashSet<string>();
            Action<string> dfs = null;
            dfs = (v) =>
            {
                if (visited.Contains(v))
                {
                    return;
                }
                visited.Add(v);
                stack.Add(v);
                foreach (var e in g.OutEdges(v))
                {
                    if (stack.Contains(e.w))
                    {
                        fas.Add(e);
                    }
                    else
                    {
                        dfs(e.w);
                    }
                }
                stack.Remove(v);

            };
            foreach (var item in g.NodesRaw())
            {
                dfs(item);
            }
            return fas.ToArray();
        }

    }
}
