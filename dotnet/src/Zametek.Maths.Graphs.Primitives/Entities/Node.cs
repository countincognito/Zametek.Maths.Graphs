using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A directed-graph node carrying a content payload (an event in arrow graphs; an activity in vertex graphs). Equality is by ID only.
    /// </summary>
    public class Node<T, TContent>
        : IHaveId<T>, IHaveContent<TContent>, IEquatable<Node<T, TContent>>, ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
        where TContent : IHaveId<T>, ICloneObject
    {
        #region Fields

        private readonly HashSet<T> m_IncomingEdges;
        private readonly HashSet<T> m_OutgoingEdges;

        private const int c_HashFactorOne = 17;
        private const int c_HashFactorTwo = 23;

        #endregion

        #region Ctors

        /// <summary>
        /// Creates a normal node carrying the given content.
        /// </summary>
        public Node(TContent content)
            : this(NodeType.Normal, content)
        {
        }

        /// <summary>
        /// Creates a node of the given type carrying the given content.
        /// </summary>
        public Node(NodeType nodetype, TContent content)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }
            NodeType = nodetype;
            Content = content;
            m_IncomingEdges = new HashSet<T>();
            m_OutgoingEdges = new HashSet<T>();
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public T Id => Content.Id;

        /// <summary>
        /// The position of the node within the graph.
        /// </summary>
        public NodeType NodeType
        {
            get;
            private set;
        }

        /// <inheritdoc/>
        public TContent Content
        {
            get;
        }

        /// <summary>
        /// The IDs of the edges pointing into this node (invalid for Start and Isolated nodes).
        /// </summary>
        public HashSet<T> IncomingEdges
        {
            get
            {
                if (NodeType == NodeType.Start || NodeType == NodeType.Isolated)
                {
                    throw new InvalidOperationException(Properties.Resources.Message_CannotRequestIncomingEdgesOfStartOrIsolatedNode);
                }
                return m_IncomingEdges;
            }
        }

        /// <summary>
        /// The IDs of the edges leaving this node (invalid for End and Isolated nodes).
        /// </summary>
        public HashSet<T> OutgoingEdges
        {
            get
            {
                if (NodeType == NodeType.End || NodeType == NodeType.Isolated)
                {
                    throw new InvalidOperationException(Properties.Resources.Message_CannotRequestOutgoingEdgesOfEndOrIsolatedNode);
                }
                return m_OutgoingEdges;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Changes the position classification of the node.
        /// </summary>
        public void SetNodeType(NodeType nodeType)
        {
            NodeType = nodeType;
        }

        #endregion

        #region Overrides

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as Node<T, TContent>);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = c_HashFactorOne;
                hash = hash * c_HashFactorTwo + Id.GetHashCode();
                hash = hash * c_HashFactorTwo + NodeType.GetHashCode();
                return hash;
            }
        }

        #endregion

        #region IEquatable

        /// <inheritdoc/>
        public bool Equals(Node<T, TContent>? other)
        {
            if (other is null)
            {
                return false;
            }
            return Id.Equals(other.Id)
                    && NodeType == other.NodeType
                    && m_IncomingEdges.OrderBy(x => x).SequenceEqual(other.m_IncomingEdges.OrderBy(x => x))
                    && m_OutgoingEdges.OrderBy(x => x).SequenceEqual(other.m_OutgoingEdges.OrderBy(x => x));
        }

        #endregion

        #region ICloneObject

        /// <inheritdoc/>
        public virtual object CloneObject()
        {
            var output = new Node<T, TContent>(NodeType, (TContent)Content.CloneObject());
            foreach (T edgeId in m_IncomingEdges)
            {
                output.m_IncomingEdges.Add(edgeId);
            }
            foreach (T edgeId in m_OutgoingEdges)
            {
                output.m_OutgoingEdges.Add(edgeId);
            }
            return output;
        }

        #endregion
    }
}
