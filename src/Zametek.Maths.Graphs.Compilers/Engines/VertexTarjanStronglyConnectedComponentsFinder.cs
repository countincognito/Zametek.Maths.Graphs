using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Tarjan's strongly connected components algorithm for Vertex (Activity-on-Vertex) graphs.
    // Activities are nodes; the algorithm traverses node-space.
    // https://en.wikipedia.org/wiki/Tarjan%27s_strongly_connected_components_algorithm
    //
    // Fix applied: companion HashSet<T> onStack replaces O(n) stack.Contains() calls,
    // giving the full O(V + E) complexity.
    internal sealed class VertexTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, TEvent>
        : IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, TEvent>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where TEvent : IEvent<T>
    {
        public IList<ICircularDependency<T>> FindStronglyConnectedComponents(
            IEnumerable<T> nodeIds,
            IDictionary<T, Node<T, TActivity>> nodeLookup,
            IDictionary<T, Node<T, TActivity>> edgeHeadNodeLookup,
            IDictionary<T, Node<T, TActivity>> edgeTailNodeLookup)
        {
            if (nodeIds is null)
            {
                throw new ArgumentNullException(nameof(nodeIds));
            }
            if (nodeLookup is null)
            {
                throw new ArgumentNullException(nameof(nodeLookup));
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

            IList<T> nodeIdList = nodeIds.ToList();

            foreach (T id in nodeIdList)
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

                Node<T, TActivity> referenceNode = nodeLookup[referenceId];
                if (referenceNode.NodeType == NodeType.End || referenceNode.NodeType == NodeType.Normal)
                {
                    foreach (T incomingEdgeId in referenceNode.IncomingEdges)
                    {
                        Node<T, TActivity> tailNode = edgeTailNodeLookup[incomingEdgeId];
                        T tailNodeId = tailNode.Id;
                        if (indexLookup[tailNodeId] < 0)
                        {
                            StrongConnect(tailNodeId);
                            lowLinkLookup[referenceId] = Math.Min(lowLinkLookup[referenceId], lowLinkLookup[tailNodeId]);
                        }
                        else if (onStack.Contains(tailNodeId))  // O(1) instead of O(n)
                        {
                            lowLinkLookup[referenceId] = Math.Min(lowLinkLookup[referenceId], indexLookup[tailNodeId]);
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
                        Node<T, TActivity> currentNode = nodeLookup[currentId];
                        if (!currentNode.Content.CanBeRemoved)
                        {
                            circularDependency.Dependencies.Add(currentId);
                        }
                    } while (!referenceId.Equals(currentId));
                    circularDependencies.Add(circularDependency);
                }
            }

            foreach (T id in nodeIdList)
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
