using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Tarjan's strongly connected components algorithm for Arrow (Activity-on-Arrow) graphs.
    // Activities are edges; the algorithm traverses edge-space.
    // https://en.wikipedia.org/wiki/Tarjan%27s_strongly_connected_components_algorithm
    //
    // Fix applied: companion HashSet<T> onStack replaces O(n) stack.Contains() calls,
    // giving the full O(V + E) complexity.
    internal sealed class ArrowTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, TEvent>
        : IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, TEvent>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where TEvent : IEvent<T>
    {
        public IList<ICircularDependency<T>> FindStronglyConnectedComponents(
            IEnumerable<T> edgeIds,
            IDictionary<T, Edge<T, TActivity>> edgeLookup,
            IDictionary<T, Node<T, TEvent>> edgeHeadNodeLookup,
            IDictionary<T, Node<T, TEvent>> edgeTailNodeLookup)
        {
            if (edgeIds is null)
            {
                throw new ArgumentNullException(nameof(edgeIds));
            }
            if (edgeLookup is null)
            {
                throw new ArgumentNullException(nameof(edgeLookup));
            }
            if (edgeHeadNodeLookup is null)
            {
                throw new ArgumentNullException(nameof(edgeHeadNodeLookup));
            }
            if (edgeTailNodeLookup is null)
            {
                throw new ArgumentNullException(nameof(edgeTailNodeLookup));
            }

            int index = 0;
            var stack = new Stack<T>();
            var onStack = new HashSet<T>();  // O(1) membership test — replaces O(n) stack.Contains()
            var indexLookup = new Dictionary<T, int>();
            var lowLinkLookup = new Dictionary<T, int>();
            var circularDependencies = new List<ICircularDependency<T>>();

            IList<T> edgeIdList = edgeIds.ToList();

            foreach (T id in edgeIdList)
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

                Edge<T, TActivity> referenceEdge = edgeLookup[referenceId];
                Node<T, TEvent> tailNode = edgeTailNodeLookup[referenceId];
                if (tailNode.NodeType == NodeType.End || tailNode.NodeType == NodeType.Normal)
                {
                    foreach (T incomingEdgeId in tailNode.IncomingEdges)
                    {
                        if (indexLookup[incomingEdgeId] < 0)
                        {
                            StrongConnect(incomingEdgeId);
                            lowLinkLookup[referenceId] = Math.Min(lowLinkLookup[referenceId], lowLinkLookup[incomingEdgeId]);
                        }
                        else if (onStack.Contains(incomingEdgeId))  // O(1) instead of O(n)
                        {
                            lowLinkLookup[referenceId] = Math.Min(lowLinkLookup[referenceId], indexLookup[incomingEdgeId]);
                        }
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
                        Edge<T, TActivity> currentEdge = edgeLookup[currentId];
                        if (!currentEdge.Content.CanBeRemoved)
                        {
                            circularDependency.Dependencies.Add(currentId);
                        }
                    } while (!referenceId.Equals(currentId));
                    circularDependencies.Add(circularDependency);
                }
            }

            foreach (T id in edgeIdList)
            {
                if (indexLookup[id] < 0)
                {
                    StrongConnect(id);
                }
            }

            return circularDependencies;
        }
    }
}
