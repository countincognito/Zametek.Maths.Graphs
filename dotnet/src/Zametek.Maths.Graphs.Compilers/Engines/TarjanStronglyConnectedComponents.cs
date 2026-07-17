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
    //
    // Fix applied: the depth-first search is implemented iteratively with an explicit
    // frame stack rather than recursion, so very deep graphs (e.g. a dependency chain
    // of tens of thousands of activities) cannot overflow the call stack.
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

            // Each frame is one in-flight StrongConnect "call": the key being
            // visited plus the enumerator over its remaining predecessors.
            var frames = new Stack<(T ReferenceId, IEnumerator<T> Predecessors)>();

            void BeginStrongConnect(T referenceId)
            {
                indexLookup[referenceId] = index;
                lowLinkLookup[referenceId] = index;
                index++;
                stack.Push(referenceId);
                onStack.Add(referenceId);
                frames.Push((referenceId, traversal.PredecessorKeys(referenceId).GetEnumerator()));
            }

            void StrongConnect(T rootId)
            {
                BeginStrongConnect(rootId);

                while (frames.Count != 0)
                {
                    (T referenceId, IEnumerator<T> predecessors) = frames.Peek();
                    bool descended = false;

                    while (predecessors.MoveNext())
                    {
                        T predecessorId = predecessors.Current;
                        if (indexLookup[predecessorId] < 0)
                        {
                            // Descend into the unvisited predecessor (the recursive call).
                            BeginStrongConnect(predecessorId);
                            descended = true;
                            break;
                        }
                        else if (onStack.Contains(predecessorId))  // O(1) instead of O(n)
                        {
                            lowLinkLookup[referenceId] = Math.Min(lowLinkLookup[referenceId], indexLookup[predecessorId]);
                        }
                    }

                    if (descended)
                    {
                        continue;
                    }

                    // All predecessors handled - the "call" for referenceId returns.
                    frames.Pop();
                    predecessors.Dispose();

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

                    // Propagate the low-link back to the caller frame (the update the
                    // recursive version performs after the call returns).
                    if (frames.Count != 0)
                    {
                        T parentId = frames.Peek().ReferenceId;
                        lowLinkLookup[parentId] = Math.Min(lowLinkLookup[parentId], lowLinkLookup[referenceId]);
                    }
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
