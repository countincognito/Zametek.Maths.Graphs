using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Tarjan's strongly connected components algorithm, written once over an
    // IGraphTraversal view. The arrow/vertex finders supply the appropriate traversal
    // (edge-space or node-space), so the algorithm here is graph-flavour agnostic.
    // https://en.wikipedia.org/wiki/Tarjan%27s_strongly_connected_components_algorithm
    //
    // Fix applied: companion HashSet<T> onStack replaces O(n) stack.Contains() calls,
    // giving the full O(V + E) complexity.
    internal static class TarjanStronglyConnectedComponents
    {
        public static List<ICircularDependency<T>> FindStronglyConnectedComponents<T>(
            IGraphTraversal<T> traversal,
            bool ignoreDummies)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            if (traversal is null)
            {
                throw new ArgumentNullException(nameof(traversal));
            }

            int index = 0;
            var stack = new Stack<T>();
            var onStack = new HashSet<T>();  // O(1) membership test - replaces O(n) stack.Contains()
            var indexLookup = new Dictionary<T, int>();
            var lowLinkLookup = new Dictionary<T, int>();
            var circularDependencies = new List<ICircularDependency<T>>();

            IList<T> keyList = traversal.Keys.ToList();

            foreach (T id in keyList)
            {
                indexLookup.Add(id, -1);
                lowLinkLookup.Add(id, -1);
            }

            void StrongConnect(T referenceId)
            {
                indexLookup[referenceId] = index;
                lowLinkLookup[referenceId] = index;
                index++;
                stack.Push(referenceId);
                onStack.Add(referenceId);

                foreach (T predecessorId in traversal.PredecessorKeys(referenceId))
                {
                    if (indexLookup[predecessorId] < 0)
                    {
                        StrongConnect(predecessorId);
                        lowLinkLookup[referenceId] = Math.Min(lowLinkLookup[referenceId], lowLinkLookup[predecessorId]);
                    }
                    else if (onStack.Contains(predecessorId))  // O(1) instead of O(n)
                    {
                        lowLinkLookup[referenceId] = Math.Min(lowLinkLookup[referenceId], indexLookup[predecessorId]);
                    }
                }

                if (lowLinkLookup[referenceId] == indexLookup[referenceId])
                {
                    var circularDependency = new CircularDependency<T>(Enumerable.Empty<T>());
                    T currentId;
                    do
                    {
                        currentId = stack.Pop();
                        onStack.Remove(currentId);

                        bool isDummy = traversal.IsRemovable(currentId);
                        if (!ignoreDummies || !isDummy)
                        {
                            circularDependency.Dependencies.Add(currentId);
                        }
                    } while (!referenceId.Equals(currentId));
                    circularDependencies.Add(circularDependency);
                }
            }

            foreach (T id in keyList)
            {
                if (indexLookup[id] < 0)
                {
                    StrongConnect(id);
                }
            }

            return circularDependencies;
        }

        public static List<ICircularDependency<T>> FindStronglyCircularDependencies<T>(
            IGraphTraversal<T> traversal,
            bool ignoreDummies)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            return FindStronglyConnectedComponents(traversal, ignoreDummies)
                .Where(x => x.Dependencies.Count > 1)
                .ToList();
        }
    }
}
