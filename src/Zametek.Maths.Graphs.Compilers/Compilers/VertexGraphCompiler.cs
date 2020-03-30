using System;

namespace Zametek.Maths.Graphs
{
    public class VertexGraphCompiler<T, TResourceId, TDependentActivity>
        : VertexGraphCompilerBase<T, TResourceId, TDependentActivity, IActivity<T, TResourceId>, IEvent<T>>
        where TDependentActivity : IDependentActivity<T, TResourceId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
    {
        #region Ctors

        protected VertexGraphCompiler(VertexGraphBuilderBase<T, TResourceId, TDependentActivity, IEvent<T>> vertexGraphBuilder)
            : base(vertexGraphBuilder)
        {
        }

        public VertexGraphCompiler()
            : this(CreateDependentActivityVertexGraphBuilder())
        {
        }

        #endregion

        #region Private Methods

        private static VertexGraphBuilder<T, TResourceId, TDependentActivity> CreateDependentActivityVertexGraphBuilder()
        {
            T edgeId = default;
            T nodeId = default;
            return new VertexGraphBuilder<T, TResourceId, TDependentActivity>(
                () => edgeId = edgeId.Previous(),
                () => nodeId = nodeId.Previous());
        }

        #endregion
    }
}
