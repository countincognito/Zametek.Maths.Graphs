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
        { }

        #endregion

        #region Public Methods

        public static VertexGraphCompiler<T, TDependentActivity> Create()
        {
            T edgeId = default(T);
            T nodeId = default(T);
            var vertexGraphBuilder = new VertexGraphBuilder<T, TDependentActivity>(
                () => edgeId = edgeId.Previous(),
                () => nodeId = nodeId.Previous());
            return new VertexGraphCompiler<T, TDependentActivity>(vertexGraphBuilder);
        }

        #endregion
    }
}
