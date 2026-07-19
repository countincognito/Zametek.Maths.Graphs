using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Computes ancestor lookups and performs transitive reduction on an
    /// Activity-on-Arrow graph - only dummy edges are reduced. The engine is
    /// stateless: the graph state, the SCC finder and the dummy-edge orchestrator
    /// are supplied to its methods (the builder owns them). The reduction walk
    /// itself lives here; it removes each redundant dummy edge through the
    /// orchestrator's <see cref="IDummyEdgeOrchestrator{T, TResourceId, TWorkStreamId, TActivity}.RemoveDummyActivity"/>
    /// primitive.
    /// </summary>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public interface IArrowTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <summary>
        /// Builds a lookup from each node ID to the full set of its ancestor node
        /// IDs. Returns null if the graph has unsatisfied dependencies or circular
        /// dependencies.
        /// </summary>
        Dictionary<T, HashSet<T>>? GetAncestorNodesLookup(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder);

        /// <summary>
        /// Performs transitive reduction on the graph, removing all redundant dummy
        /// edges. Returns false if the reduction cannot be performed (unsatisfied
        /// dependencies or circular dependencies).
        /// </summary>
        bool ReduceGraph(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder,
            IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> orchestrator);
    }
}
