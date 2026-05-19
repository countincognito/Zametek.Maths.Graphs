using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Orchestrates all dummy-edge operations for an Activity-on-Arrow graph.
    // The orchestrator holds no graph state of its own — it operates on the dictionaries
    // owned by ArrowGraphBuilder, which are supplied at construction time.
    internal interface IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, System.IComparable<T>, System.IEquatable<T>
        where TResourceId : struct, System.IComparable<TResourceId>, System.IEquatable<TResourceId>
        where TWorkStreamId : struct, System.IComparable<TWorkStreamId>, System.IEquatable<TWorkStreamId>
    {
        // Inserts a dummy edge from tailNode to headNode.
        void ConnectWithDummyEdge(Node<T, IEvent<T>> tailNode, Node<T, IEvent<T>> headNode);

        // Removes a dummy edge from the graph, merging adjacent nodes where possible.
        bool RemoveDummyActivity(T activityId);

        // Redirects redundant dummy edges (canonical arrow-graph AOA normalisation step 1).
        bool RedirectDummyEdges();

        // Removes dummy edges that are transitively implied (transitive-reduction for dummy edges only).
        bool RemoveRedundantDummyEdges();

        // Removes dummy edges made redundant by transitivity starting from nodeId.
        void RemoveRedundantIncomingDummyEdges(T nodeId, Dictionary<T, HashSet<T>> nodeIdAncestorLookup);

        // Returns all dummy edges in depth-first descending order from the start node.
        List<Edge<T, TActivity>> GetDummyEdgesInDescendingOrder();
    }
}
