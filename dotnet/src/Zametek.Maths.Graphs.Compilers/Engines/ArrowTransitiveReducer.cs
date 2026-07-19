using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Transitive reducer for Activity-on-Arrow graphs - only dummy edges are
    // reduced. Stateless: the graph state, SCC finder and dummy-edge orchestrator
    // are supplied to each method. The reduction walk lives here and removes each
    // redundant dummy edge through the orchestrator's RemoveDummyActivity primitive
    // (the orchestrator owns edge mutation; the reducer owns the traversal).
    /// <summary>
    /// Default transitive reducer for Activity-on-Arrow graphs.
    /// </summary>
    public sealed class ArrowTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity>
        : IArrowTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <inheritdoc/>
        public Dictionary<T, HashSet<T>>? GetAncestorNodesLookup(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (sccFinder is null)
            {
                throw new ArgumentNullException(nameof(sccFinder));
            }

            if (!state.AllDependenciesSatisfied)
            {
                return null;
            }

            List<ICircularDependency<T>> circularDependencies =
                sccFinder.FindStronglyCircularDependencies(state, ignoreDummies: false);

            var ancestorGraphView = new ArrowAncestorGraphView<T, TResourceId, TWorkStreamId, TActivity>(state);
            return AncestorNodeCalculator.GetAncestorNodesLookup(ancestorGraphView, circularDependencies);
        }

        /// <inheritdoc/>
        public bool ReduceGraph(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder,
            IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> orchestrator)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (sccFinder is null)
            {
                throw new ArgumentNullException(nameof(sccFinder));
            }
            if (orchestrator is null)
            {
                throw new ArgumentNullException(nameof(orchestrator));
            }

            AncestorBitSets<T>? ancestorBitSets = GetAncestorBitSets(state, sccFinder);

            if (ancestorBitSets is null)
            {
                return false;
            }

            IEnumerable<T> endNodeIds = state.EndNodes.Select(x => x.Id);
            RemoveRedundantIncomingDummyEdges(state, endNodeIds, ancestorBitSets, orchestrator);

            return true;
        }

        // Same checks as GetAncestorNodesLookup, but the ancestors stay in their compact
        // bitset form - ReduceGraph never materialises the dictionary-of-hashsets.
        private static AncestorBitSets<T>? GetAncestorBitSets(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder)
        {
            if (!state.AllDependenciesSatisfied)
            {
                return null;
            }

            List<ICircularDependency<T>> circularDependencies =
                sccFinder.FindStronglyCircularDependencies(state, ignoreDummies: false);

            var ancestorGraphView = new ArrowAncestorGraphView<T, TResourceId, TWorkStreamId, TActivity>(state);
            return AncestorNodeCalculator.GetAncestorBitSets(ancestorGraphView, circularDependencies);
        }

        // Iterative (was recursive) so a deep dependency chain cannot overflow the
        // stack. A single shared visited set means each node's incoming dummy edges
        // are processed once: every node removes only its own incoming dummy edges,
        // using the static ancestor bitsets, so the operation is independent of visit
        // order and idempotent per node. Removal itself is delegated to the
        // orchestrator so a custom orchestrator can still customise edge removal.
        private static void RemoveRedundantIncomingDummyEdges(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IEnumerable<T> rootNodeIds,
            AncestorBitSets<T> ancestorBitSets,
            IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> orchestrator)
        {
            var visited = new HashSet<T>();
            var stack = new Stack<T>();
            foreach (T rootNodeId in rootNodeIds)
            {
                stack.Push(rootNodeId);
            }

            ulong[] scratch = ancestorBitSets.CreateScratch();

            while (stack.Count != 0)
            {
                T currentNodeId = stack.Pop();
                if (!visited.Add(currentNodeId))
                {
                    continue;
                }

                Node<T, IEvent<T>> node = state.Node(currentNodeId);

                if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
                {
                    continue;
                }

                // Go through all the incoming edges and collate the
                // ancestors of their tail nodes into the scratch bitset.
                AncestorBitSets<T>.ClearScratch(scratch);
                foreach (T incomingEdgeId in node.IncomingEdges)
                {
                    ancestorBitSets.UnionAncestorsInto(scratch, state.EdgeTailNode(incomingEdgeId).Id);
                }

                // Go through the incoming dummy edges and remove any that
                // connect directly to any ancestors of the non-dummy edges'
                // tail nodes.
                List<T> incomingDummyEdges = node.IncomingEdges
                    .Select(x => state.Edge(x))
                    .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved)
                    .Select(x => x.Id)
                    .ToList();

                foreach (T dummyEdgeId in incomingDummyEdges)
                {
                    T dummyEdgeTailNodeId = state.EdgeTailNode(dummyEdgeId).Id;
                    if (ancestorBitSets.ScratchContains(scratch, dummyEdgeTailNodeId))
                    {
                        orchestrator.RemoveDummyActivity(state, dummyEdgeId);
                    }
                }

                // Continue with all the remaining incoming edges' tail nodes.
                List<T> remainingIncomingEdges = node.IncomingEdges
                    .Select(x => state.EdgeTailNode(x).Id)
                    .ToList();

                foreach (T tailNodeId in remainingIncomingEdges)
                {
                    stack.Push(tailNodeId);
                }
            }
        }
    }
}
