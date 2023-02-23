using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class Node<T, TContent>
        : IHaveId<T>, IHaveContent<TContent>, IEquatable<Node<T, TContent>>, ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
        where TContent : IHaveId<T>, ICloneObject
    {
        #region Fields

        private readonly HashSet<T> m_IncomingEdges;
        private readonly HashSet<T> m_OutgoingEdges;

        private const int HashFactorOne = 17;
        private const int HashFactorTwo = 23;

        #endregion

        #region Ctors

        public Node(TContent content)
            : this(NodeType.Normal, content)
        {
        }

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

        public T Id => Content.Id;

        public NodeType NodeType
        {
            get;
            private set;
        }

        public TContent Content
        {
            get;
        }

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

        public void SetNodeType(NodeType nodeType)
        {
            NodeType = nodeType;
        }

        #endregion

        #region Overrides

        public override bool Equals(object obj)
        {
            return Equals(obj as Node<T, TContent>);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = HashFactorOne;
                hash = hash * HashFactorTwo + Id.GetHashCode();
                return hash;
            }
        }

        #endregion

        #region IEquatable

        public bool Equals(Node<T, TContent> other)
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

        public object CloneObject()
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
