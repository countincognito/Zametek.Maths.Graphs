using System;

namespace Zametek.Maths.Graphs
{
    public class VertexGraphCompiler<T, TDependentActivity>
        : VertexGraphCompilerBase<T, TDependentActivity, IActivity<T>, IEvent<T>>
        where TDependentActivity : IDependentActivity<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        protected VertexGraphCompiler(VertexGraphBuilderBase<T, TDependentActivity, IEvent<T>> vertexGraphBuilder)
            : base(vertexGraphBuilder)
        {
        }

        public VertexGraphCompiler()
            : this(CreateDependentActivityVertexGraphBuilder())
        {
        }

        #endregion

        #region Private Methods

        private static VertexGraphBuilder<T, TDependentActivity> CreateDependentActivityVertexGraphBuilder()
        {
            T edgeId = default;
            T nodeId = default;
            return new VertexGraphBuilder<T, TDependentActivity>(
                () => edgeId = edgeId.Previous(),
                () => nodeId = nodeId.Previous());
        }

        #endregion
    }
}
