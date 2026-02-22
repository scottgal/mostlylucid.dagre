using System;
using System.Collections.Generic;
using System.Linq;

namespace Dagre
{
    /// <summary>
    /// Entry used throughout the ordering phase for barycenter calculations,
    /// conflict resolution, and subgraph sorting.
    /// </summary>
    public class OrderEntry
    {
        /// <summary>Single node id (from barycenter phase).</summary>
        public string V;
        /// <summary>Ordered list of node ids (after conflict resolution / sort).</summary>
        public List<string> Vs;
        /// <summary>Original index in the movable list.</summary>
        public int I;
        /// <summary>Barycenter value (null if no edges).</summary>
        public float? Barycenter;
        /// <summary>Weight of incoming edges.</summary>
        public float? Weight;

        // Internal fields used by resolveConflicts
        internal int Indegree;
        internal List<OrderEntry> InEntries;
        internal List<OrderEntry> OutEntries;
        internal bool Merged;
    }

    public class SortSubGraph
    {
        public static OrderEntry[] barycenter(DagreGraph g, string[] movable)
        {
            var result = new OrderEntry[movable.Length];
            for (int i = 0; i < movable.Length; i++)
            {
                var v = movable[i];
                var inV = g.InEdges(v);
                if (inV.Length == 0)
                {
                    result[i] = new OrderEntry { V = v };
                }
                else
                {
                    float sum = 0;
                    float weight = 0;
                    foreach (var e in inV)
                    {
                        var edge = g.Edge(e);
                        var nodeU = g.Node(e.v);
                        sum += edge.Weight * nodeU.Order;
                        weight += edge.Weight;
                    }
                    result[i] = new OrderEntry
                    {
                        V = v,
                        Barycenter = sum / weight,
                        Weight = weight
                    };
                }
            }
            return result;
        }

        public static void mergeBarycenters(OrderEntry target, OrderEntry other)
        {
            if (target.Barycenter.HasValue)
            {
                target.Barycenter = (target.Barycenter.Value * target.Weight.Value + other.Barycenter.Value * other.Weight.Value) / (target.Weight.Value + other.Weight.Value);
                target.Weight += other.Weight;
            }
            else
            {
                target.Barycenter = other.Barycenter;
                target.Weight = other.Weight;
            }
        }

        public static OrderEntry sortSubraph(DagreGraph g, string v, DagreGraph cg, bool biasRight)
        {
            var movable = g.Children(v);
            var node = g.Node(v);
            string bl = null;
            string br = null;
            if (node != null)
            {
                // On layer graphs, borderLeft/Right are stored as string node IDs
                // in the overflow dictionary via the "_borderLeft"/"_borderRight" keys.
                object blVal, brVal;
                if (node.TryGetValue("_borderLeft", out blVal))
                    bl = blVal as string;
                if (node.TryGetValue("_borderRight", out brVal))
                    br = brVal as string;
            }
            var subgraphs = new Dictionary<string, OrderEntry>(StringComparer.Ordinal);

            if (bl != null)
            {
                int count = 0;
                foreach (var z in movable)
                    if (z != bl && z != br) count++;
                var filtered = new string[count];
                int fi = 0;
                foreach (var z in movable)
                    if (z != bl && z != br) filtered[fi++] = z;
                movable = filtered;
            }

            var barycenters = barycenter(g, movable);
            foreach (var entry in barycenters)
            {
                if (g.HasChildren(entry.V))
                {
                    var subgraphResult = sortSubraph(g, entry.V, cg, biasRight);
                    subgraphs[entry.V] = subgraphResult;
                    if (subgraphResult.Barycenter.HasValue)
                    {
                        mergeBarycenters(entry, subgraphResult);
                    }
                }
            }

            var entries = ResolveConflicts.resolveConflicts(barycenters, cg);
            // expand subgraphs
            foreach (var entry in entries)
            {
                var expanded = new List<string>();
                foreach (var item in entry.Vs)
                {
                    if (subgraphs.TryGetValue(item, out var sub))
                    {
                        expanded.AddRange(sub.Vs);
                    }
                    else
                    {
                        expanded.Add(item);
                    }
                }
                entry.Vs = expanded;
            }

            var result = sort(entries, biasRight);
            if (bl != null)
            {
                var newVs = new List<string> { bl };
                newVs.AddRange(result.Vs);
                newVs.Add(br);
                result.Vs = newVs;
                if (g.Predecessors(bl).Length != 0)
                {
                    var blPred = g.Node(g.Predecessors(bl)[0]);
                    var brPred = g.Node(g.Predecessors(br)[0]);
                    if (!result.Barycenter.HasValue)
                    {
                        result.Barycenter = 0;
                        result.Weight = 0;
                    }
                    result.Barycenter = (result.Barycenter.Value * result.Weight.Value + blPred.Order + brPred.Order) / (result.Weight.Value + 2);
                    result.Weight += 2;
                }
            }
            return result;
        }

        public static int consumeUnsortable(List<string> vs, List<OrderEntry> unsortable, int index)
        {
            while (unsortable.Count != 0 && unsortable[unsortable.Count - 1].I <= index)
            {
                var last = unsortable[unsortable.Count - 1];
                unsortable.RemoveAt(unsortable.Count - 1);
                vs.AddRange(last.Vs);
                index++;
            }
            return index;
        }

        public static OrderEntry sort(OrderEntry[] entries, bool biasRight)
        {
            // partition into sortable (has barycenter) and unsortable
            var sortable = new List<OrderEntry>();
            var unsortable = new List<OrderEntry>();
            foreach (var entry in entries)
            {
                if (entry.Barycenter.HasValue)
                {
                    sortable.Add(entry);
                }
                else
                {
                    unsortable.Add(entry);
                }
            }

            unsortable.Sort((a, b) => -a.I + b.I);
            var vs = new List<string>();
            float sum = 0;
            float weight = 0;
            int vsIndex = 0;

            sortable.Sort((entryV, entryW) =>
            {
                if (entryV.Barycenter.Value < entryW.Barycenter.Value)
                    return -1;
                if (entryV.Barycenter.Value > entryW.Barycenter.Value)
                    return 1;
                return !biasRight ? (entryV.I - entryW.I) : (entryW.I - entryV.I);
            });

            vsIndex = consumeUnsortable(vs, unsortable, vsIndex);
            foreach (var entry in sortable)
            {
                vsIndex += entry.Vs.Count;
                vs.AddRange(entry.Vs);
                sum += entry.Barycenter.Value * entry.Weight.Value;
                weight += entry.Weight.Value;
                vsIndex = consumeUnsortable(vs, unsortable, vsIndex);
            }

            var result = new OrderEntry { Vs = vs };
            if (weight != 0)
            {
                result.Barycenter = sum / weight;
                result.Weight = weight;
            }
            return result;
        }
    }
}
