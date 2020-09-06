using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class Graph<T, TEdgeContent, TNodeContent>
        : IEquatable<Graph<T, TEdgeContent, TNodeContent>>
        where T : struct, IComparable<T>, IEquatable<T>
        where TEdgeContent : IHaveId<T>, ICloneObject
        where TNodeContent : IHaveId<T>, ICloneObject
    {
        #region Fields

        private const int HashFactorOne = 17;
        private const int HashFactorTwo = 23;

        #endregion

        #region Ctors

        public Graph()
            : this(Enumerable.Empty<Edge<T, TEdgeContent>>(), Enumerable.Empty<Node<T, TNodeContent>>())
        {
        }

        public Graph(IEnumerable<Edge<T, TEdgeContent>> edges, IEnumerable<Node<T, TNodeContent>> nodes)
        {
            if (edges is null)
            {
                throw new ArgumentNullException(nameof(edges));
            }
            if (nodes is null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }
            Edges = edges.ToList();
            Nodes = nodes.ToList();
        }

        #endregion

        #region Properties

        public IList<Edge<T, TEdgeContent>> Edges
        {
            get;
        }

        public IList<Node<T, TNodeContent>> Nodes
        {
            get;
        }

        #endregion

        #region Overrides

        public override bool Equals(object obj)
        {
            return Equals(obj as Graph<T, TEdgeContent, TNodeContent>);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = HashFactorOne;
                hash = Edges.Select(x => x.Id).OrderBy(x => x).Aggregate(hash, (a, b) => a * HashFactorTwo + b.GetHashCode());
                hash = Nodes.Select(x => x.Id).OrderBy(x => x).Aggregate(hash, (a, b) => a * HashFactorTwo + b.GetHashCode());
                return hash;
            }
        }

        #endregion

        #region IEquatable

        public bool Equals(Graph<T, TEdgeContent, TNodeContent> other)
        {
            if (other is null)
            {
                return false;
            }
            return Edges.OrderBy(x => x.Id).SequenceEqual(other.Edges.OrderBy(x => x.Id))
                && Nodes.OrderBy(x => x.Id).SequenceEqual(other.Nodes.OrderBy(x => x.Id));
        }

        #endregion
    }
}
