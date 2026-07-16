using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Shared ancestor-node calculation for both arrow and vertex transitive reducers.
    // The reducers handle the (graph-flavour specific) dependency-satisfied and circular
    // dependency checks and supply this calculation with a node-space view of the graph.
    //
    // "Ancestors" here means every node that comes before a given node - its parents,
    // its parents' parents, and so on all the way back to the root(s).
    //
    // The calculation runs in two phases:
    //
    //   Phase 1 walks backwards from the end nodes and records every node it can reach,
    //   giving each one a "dense index" (0, 1, 2, ... in discovery order). Node IDs can
    //   be any value type with gaps between values, so they cannot be used as array
    //   positions directly - the dense index is the node's position in the bitsets that
    //   phase 2 builds (see AncestorBitSets for how the bitsets themselves work).
    //
    //   Phase 2 computes each node's ancestor set as a bitset, in "parents first"
    //   order: a node's ancestors are exactly its parents plus everything its parents
    //   are descended from, so once every parent's set is known the node's own set is
    //   just those sets merged together (a cheap bitwise OR per word) plus one bit per
    //   parent.
    //
    // The dictionary-of-hashsets form used by the public lookup API is materialised
    // from the bitsets on demand, so there is a single traversal implementation.
    //
    // Both phases are iterative (explicit stacks) rather than recursive, so a deep
    // dependency chain cannot overflow the call stack.
    internal static class AncestorNodeCalculator
    {
        public static Dictionary<T, HashSet<T>>? GetAncestorNodesLookup<T>(
            IAncestorGraphView<T> view,
            IReadOnlyCollection<ICircularDependency<T>> circularDependencies)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            return GetAncestorBitSets(view, circularDependencies)?.ToDictionary();
        }

        public static AncestorBitSets<T>? GetAncestorBitSets<T>(
            IAncestorGraphView<T> view,
            IReadOnlyCollection<ICircularDependency<T>> circularDependencies)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            if (view is null)
            {
                throw new ArgumentNullException(nameof(view));
            }
            if (circularDependencies is null)
            {
                throw new ArgumentNullException(nameof(circularDependencies));
            }

            // Ancestor sets are only meaningful in a graph without cycles (in a cycle
            // every member would be an ancestor of every other member, including
            // itself), so the caller's circular-dependency findings are checked first.
            if (circularDependencies.Count != 0)
            {
                return null;
            }

            List<T> endNodeIds = view.EndNodeIds.ToList();

            // Phase 1: discover every node reachable from the end nodes and assign each
            // a dense index - its bit position in every bitset built in phase 2. The
            // index is simply "how many nodes were discovered before this one", which
            // guarantees the indexes are 0..count-1 with no gaps.
            var indexLookup = new Dictionary<T, int>();
            var ids = new List<T>();
            var discovery = new Stack<T>();
            foreach (T endNodeId in endNodeIds)
            {
                discovery.Push(endNodeId);
            }
            while (discovery.Count != 0)
            {
                T nodeId = discovery.Pop();
                // A node reachable along several paths is discovered once and then
                // skipped on every later encounter.
                if (indexLookup.ContainsKey(nodeId))
                {
                    continue;
                }
                indexLookup.Add(nodeId, ids.Count);
                ids.Add(nodeId);
                // Root nodes have no parents to follow (and asking the view for their
                // parents is invalid).
                if (view.IsRootNode(nodeId))
                {
                    continue;
                }
                foreach (T parentNodeId in view.ParentNodeIds(nodeId))
                {
                    if (!indexLookup.ContainsKey(parentNodeId))
                    {
                        discovery.Push(parentNodeId);
                    }
                }
            }

            int nodeCount = ids.Count;
            int wordCount = AncestorBitSets<T>.WordCountFor(nodeCount);
            // One bitset per node, indexed by dense index. A slot stays null until the
            // node's ancestor set has been computed - phase 2 uses that as its
            // "already done" marker.
            var ancestorWords = new ulong[nodeCount][];
            // Root nodes have no ancestors, so they can all share one empty bitset.
            // This is safe because stored bitsets are only ever read from - unions are
            // accumulated into separate scratch or freshly created arrays.
            var emptyWords = new ulong[wordCount];

            // Phase 2: compute the ancestor bitsets in "parents first" order, using an
            // explicit stack instead of recursion.
            //
            // The stack discipline: look at (peek, do not yet remove) the node on top
            // of the stack.
            //   - If its set is already computed, just pop it.
            //   - If it is a root, give it the shared empty set and pop it.
            //   - Otherwise, push any parents whose sets are not yet computed and leave
            //     the node where it is. It will be looked at again after those parents
            //     have been dealt with - and on that later visit all its parents are
            //     resolved, so its own set can be computed and it can be popped.
            //
            // The graph is known to be acyclic at this point (cycles were rejected
            // above), so following parents always terminates at the roots and every
            // node is eventually popped.
            var stack = new Stack<T>();
            foreach (T endNodeId in endNodeIds)
            {
                stack.Push(endNodeId);
            }

            while (stack.Count != 0)
            {
                T nodeId = stack.Peek();
                int nodeIndex = indexLookup[nodeId];

                if (ancestorWords[nodeIndex] != null)
                {
                    stack.Pop();
                    continue;
                }

                if (view.IsRootNode(nodeId))
                {
                    ancestorWords[nodeIndex] = emptyWords;
                    stack.Pop();
                    continue;
                }

                List<T> parentNodeIds = view.ParentNodeIds(nodeId).ToList();
                bool allParentsResolved = true;
                foreach (T parentNodeId in parentNodeIds)
                {
                    if (ancestorWords[indexLookup[parentNodeId]] is null)
                    {
                        stack.Push(parentNodeId);
                        allParentsResolved = false;
                    }
                }

                if (!allParentsResolved)
                {
                    continue;
                }

                // Every parent is resolved, so this node's ancestor set is: each parent
                // itself (one bit per parent), plus each parent's own ancestors (the
                // parent's whole bitset, merged in word by word with bitwise OR).
                var words = new ulong[wordCount];
                foreach (T parentNodeId in parentNodeIds)
                {
                    int parentIndex = indexLookup[parentNodeId];
                    AncestorBitSets<T>.SetBit(words, parentIndex);
                    ulong[] parentWords = ancestorWords[parentIndex];
                    for (int i = 0; i < wordCount; i++)
                    {
                        words[i] |= parentWords[i];
                    }
                }

                ancestorWords[nodeIndex] = words;
                stack.Pop();
            }

            return new AncestorBitSets<T>(indexLookup, ids.ToArray(), ancestorWords, wordCount);
        }
    }
}
