using System;

namespace Zametek.Maths.Graphs
{
    public class VertexGraphCompiler<T, TResourceId, TWorkStreamId, TDependentActivity>
        : VertexGraphCompilerBase<T, TResourceId, TWorkStreamId, TDependentActivity, IActivity<T, TResourceId, TWorkStreamId>, IEvent<T>>
        where TDependentActivity : IDependentActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Ctors

        protected VertexGraphCompiler(VertexGraphBuilderBase<T, TResourceId, TWorkStreamId, TDependentActivity, IEvent<T>> vertexGraphBuilder)
            : base(vertexGraphBuilder)
        {
        }

        public VertexGraphCompiler()
            : this(CreateDependentActivityVertexGraphBuilder())
        {
        }

        #endregion

        #region Private Methods

        private static VertexGraphBuilder<T, TResourceId, TWorkStreamId, TDependentActivity> CreateDependentActivityVertexGraphBuilder()
        {
            T edgeId = default;
            T nodeId = default;
            return new VertexGraphBuilder<T, TResourceId, TWorkStreamId, TDependentActivity>(
                () => edgeId = edgeId.Previous(),
                () => nodeId = nodeId.Previous());
        }

        #endregion
    }
}
