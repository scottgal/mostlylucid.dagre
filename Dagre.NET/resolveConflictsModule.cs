using System;
using System.Collections.Generic;
using System.Linq;

namespace Dagre
{
    public class ResolveConflicts
    {
        /*
         * Given a list of entries of the form {v, barycenter, weight} and a
         * constraint graph this function will resolve any conflicts between the
         * constraint graph and the barycenters for the entries. If the barycenters for
         * an entry would violate a constraint in the constraint graph then we coalesce
         * the nodes in the conflict into a new node that respects the contraint and
         * aggregates barycenter and weight information.
         *
         * This implementation is based on the description in Forster, "A Fast and
         * Simple Hueristic for Constrained Two-Level Crossing Reduction," thought it
         * differs in some specific details.
         *
         * Pre-conditions:
         *
         *    1. Each entry has the form {v, barycenter, weight}, or if the node has
         *       no barycenter, then {v}.
         *
         * Returns:
         *
         *    A new list of entries of the form {vs, i, barycenter, weight}. The list
         *    `vs` may either be a singleton or it may be an aggregation of nodes
         *    ordered such that they do not violate constraints from the constraint
         *    graph. The property `i` is the lowest original index of any of the
         *    elements in `vs`.
         */

        public static void mergeEntries(OrderEntry target, OrderEntry source)
        {
            float sum = 0;
            float weight = 0;
            if (target.Weight.HasValue && target.Weight.Value != 0)
            {
                sum += target.Barycenter.Value * target.Weight.Value;
                weight += target.Weight.Value;
            }
            if (source.Weight.HasValue && source.Weight.Value != 0)
            {
                sum += source.Barycenter.Value * source.Weight.Value;
                weight += source.Weight.Value;
            }
            var merged = new List<string>(source.Vs.Count + target.Vs.Count);
            merged.AddRange(source.Vs);
            merged.AddRange(target.Vs);
            target.Vs = merged;
            target.Barycenter = sum / weight;
            target.Weight = weight;
            target.I = Math.Min(source.I, target.I);
            source.Merged = true;
        }

        public static OrderEntry[] resolveConflicts(OrderEntry[] entities, DagreGraph cg)
        {
            var mappedEntries = new Dictionary<string, OrderEntry>(StringComparer.Ordinal);
            for (int i = 0; i < entities.Length; i++)
            {
                var entry = entities[i];

                var mapped = new OrderEntry
                {
                    Indegree = 0,
                    InEntries = new List<OrderEntry>(),
                    OutEntries = new List<OrderEntry>(),
                    Vs = new List<string> { entry.V },
                    I = i
                };

                if (entry.Barycenter.HasValue)
                {
                    mapped.Barycenter = entry.Barycenter;
                    mapped.Weight = entry.Weight;
                }

                mappedEntries[entry.V] = mapped;
            }

            foreach (var e in cg.Edges())
            {
                OrderEntry entryV, entryW;
                if (mappedEntries.TryGetValue(e.v, out entryV) && mappedEntries.TryGetValue(e.w, out entryW))
                {
                    entryW.Indegree++;
                    entryV.OutEntries.Add(entryW);
                }
            }

            var sourceSet = new List<OrderEntry>();
            foreach (var entry2 in mappedEntries.Values)
                if (entry2.Indegree == 0) sourceSet.Add(entry2);

            var results = new List<OrderEntry>();
            while (sourceSet.Count != 0)
            {
                var entry = sourceSet[sourceSet.Count - 1];
                sourceSet.RemoveAt(sourceSet.Count - 1);
                results.Add(entry);

                // Process in-entries in reverse
                entry.InEntries.Reverse();
                foreach (var uEntry in entry.InEntries)
                {
                    if (uEntry.Merged)
                    {
                        continue;
                    }
                    if (!uEntry.Barycenter.HasValue || !entry.Barycenter.HasValue
                        || uEntry.Barycenter.Value >= entry.Barycenter.Value)
                    {
                        mergeEntries(entry, uEntry);
                    }
                }

                // Process out-entries
                foreach (var wEntry in entry.OutEntries)
                {
                    wEntry.InEntries.Add(entry);
                    if (--wEntry.Indegree == 0)
                    {
                        sourceSet.Add(wEntry);
                    }
                }
            }

            int resultCount = 0;
            foreach (var e in results)
                if (!e.Merged) resultCount++;
            var output = new OrderEntry[resultCount];
            int idx = 0;
            foreach (var e in results)
            {
                if (!e.Merged)
                    output[idx++] = new OrderEntry
                    {
                        Vs = e.Vs,
                        I = e.I,
                        Barycenter = e.Barycenter,
                        Weight = e.Weight
                    };
            }
            return output;
        }
    }
}
