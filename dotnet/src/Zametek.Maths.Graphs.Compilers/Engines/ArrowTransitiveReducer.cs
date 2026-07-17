using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Transitive reducer for Activity-on-Arrow graphs.
    // Computes the ancestor-node lookup and delegates dummy-edge removal to the
    // IDummyEdgeOrchestrator - only dummy edges are reduced in arrow graphs.
    // Operates on the shared ArrowGraphState.
    internal sealed class ArrowTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity>
        : ITransitiveReducer<T>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private readonly IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> m_DummyEdgeOrchestrator;
        private readonly IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> m_StronglyConnectedComponentsFinder;
        private readonly ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> m_State;
        private readonly IAncestorGraphView<T> m_AncestorGraphView;

        #endregion

        #region Ctor

        internal ArrowTransitiveReducer(
            IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> dummyEdgeOrchestrator,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> stronglyConnectedComponentsFinder,
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            m_DummyEdgeOrchestrator = dummyEdgeOrchestrator ?? throw new ArgumentNullException(nameof(dummyEdgeOrchestrator));
            m_StronglyConnectedComponentsFinder = stronglyConnectedComponentsFinder ?? throw new ArgumentNullException(nameof(stronglyConnectedComponentsFinder));
            m_State = state ?? throw new ArgumentNullException(nameof(state));
            m_AncestorGraphView = new ArrowAncestorGraphView<T, TResourceId, TWorkStreamId, TActivity>(m_State);
        }

        #endregion

        #region ITransitiveReducer

        public Dictionary<T, HashSet<T>>? GetAncestorNodesLookup()
        {
            if (!m_State.AllDependenciesSatisfied)
            {
                return null;
            }

            List<ICircularDependency<T>> circularDependencies =
                m_StronglyConnectedComponentsFinder.FindStronglyCircularDependencies(m_State, ignoreDummies: false);

            return AncestorNodeCalculator.GetAncestorNodesLookup(m_AncestorGraphView, circularDependencies);
        }

        public bool ReduceGraph()
        {
            AncestorBitSets<T>? ancestorBitSets = GetAncestorBitSets();

            if (ancestorBitSets is null)
            {
                return false;
            }

            List<T> endNodeIds = m_State.EndNodes.Select(x => x.Id).ToList();

            // The default orchestrator understands the compact bitset form directly; a
            // custom orchestrator only knows the public dictionary contract, so the
            // lookup is materialised for it on demand.
            if (m_DummyEdgeOrchestrator is DummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> defaultOrchestrator)
            {
                defaultOrchestrator.RemoveRedundantIncomingDummyEdges(endNodeIds, ancestorBitSets);
            }
            else
            {
                Dictionary<T, HashSet<T>> ancestorNodesLookup = ancestorBitSets.ToDictionary();
                foreach (T endNodeId in endNodeIds)
                {
                    m_DummyEdgeOrchestrator.RemoveRedundantIncomingDummyEdges(endNodeId, ancestorNodesLookup);
                }
            }

            return true;
        }

        #endregion

        #region Private Methods

        // Same checks as GetAncestorNodesLookup, but the ancestors stay in their compact
        // bitset form - ReduceGraph never materialises the dictionary-of-hashsets for the
        // default orchestrator.
        private AncestorBitSets<T>? GetAncestorBitSets()
        {
            if (!m_State.AllDependenciesSatisfied)
            {
                return null;
            }

            List<ICircularDependency<T>> circularDependencies =
                m_StronglyConnectedComponentsFinder.FindStronglyCircularDependencies(m_State, ignoreDummies: false);

            return AncestorNodeCalculator.GetAncestorBitSets(m_AncestorGraphView, circularDependencies);
        }

        #endregion
    }
}
