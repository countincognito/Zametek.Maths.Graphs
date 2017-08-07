using System;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Zametek.Maths.Graphs.Compilers.Tests")]

namespace Zametek.Maths.Graphs
{
    public abstract class GraphCompilerBase<T, TEdgeContent, TNodeContent, TActivity, TEvent>
        where T : struct, IComparable<T>, IEquatable<T>
        where TEdgeContent : IHaveId<T>, IWorkingCopy
        where TNodeContent : IHaveId<T>, IWorkingCopy
        where TActivity : IActivity<T>
        where TEvent : IEvent<T>
    {
        #region Fields

        private readonly object m_Lock;
        private readonly GraphBuilderBase<T, TEdgeContent, TNodeContent, TActivity, TEvent> m_GraphBuilder;

        #endregion

        #region Ctors

        protected GraphCompilerBase(GraphBuilderBase<T, TEdgeContent, TNodeContent, TActivity, TEvent> graphBuilder)
        {
            m_GraphBuilder = graphBuilder ?? throw new ArgumentNullException(nameof(graphBuilder));
            m_Lock = new object();
        }

        #endregion

        #region Properties

        public int Duration
        {
            get
            {
                lock (m_Lock)
                {
                    return m_GraphBuilder.Duration;
                }
            }
        }

        public int CyclomaticComplexity
        {
            get
            {
                lock (m_Lock)
                {
                    int edgeCount = m_GraphBuilder.Edges.Count();
                    int nodeCount = m_GraphBuilder.Nodes.Where(x => x.NodeType != NodeType.Isolated).Count();
                    return edgeCount - nodeCount + 2;
                }
            }
        }

        internal GraphBuilderBase<T, TEdgeContent, TNodeContent, TActivity, TEvent> Builder
        {
            get
            {
                lock (m_Lock)
                {
                    return m_GraphBuilder;
                }
            }
        }

        #endregion

        #region Public Methods

        public T GetNextActivityId()
        {
            lock (m_Lock)
            {
                return m_GraphBuilder.ActivityIds.DefaultIfEmpty().Max().Next();
            }
        }

        public void Reset()
        {
            lock (m_Lock)
            {
                m_GraphBuilder.Reset();
            }
        }

        public abstract bool AddActivity(TActivity activity);

        public abstract bool RemoveActivity(T activityId);

        public Graph<T, TEdgeContent, TNodeContent> ToGraph()
        {
            lock (m_Lock)
            {
                return m_GraphBuilder.ToGraph();
            }
        }

        #endregion
    }
}
