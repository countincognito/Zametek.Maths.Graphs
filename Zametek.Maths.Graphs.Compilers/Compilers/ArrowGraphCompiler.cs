using System;

namespace Zametek.Maths.Graphs
{
    public class ArrowGraphCompiler<T, TDependentActivity>
        : ArrowGraphCompilerBase<T, TDependentActivity, IActivity<T>, IEvent<T>>
        where TDependentActivity : IDependentActivity<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        protected ArrowGraphCompiler(ArrowGraphBuilderBase<T, TDependentActivity, IEvent<T>> arrowGraphBuilder)
            : base(arrowGraphBuilder)
        { }

        #endregion

        #region Public Methods

        public static ArrowGraphCompiler<T, TDependentActivity> Create()
        {
            T edgeId = default(T);
            T nodeId = default(T);
            var arrowGraphBuilder = new DependentActivityArrowGraphBuilder(
                () => edgeId = edgeId.Previous(),
                () => nodeId = nodeId.Previous());
            return new ArrowGraphCompiler<T, TDependentActivity>(arrowGraphBuilder);
        }

        #endregion

        #region Private Types

        private class DependentActivityArrowGraphBuilder
            : ArrowGraphBuilder<T, TDependentActivity>
        {
            #region Ctors

            public DependentActivityArrowGraphBuilder(Func<T> edgeIdGenerator, Func<T> nodeIdGenerator)
                : base(edgeIdGenerator, nodeIdGenerator)
            { }

            public DependentActivityArrowGraphBuilder(
                Graph<T, TDependentActivity, IEvent<T>> graph,
                Func<T> edgeIdGenerator,
                Func<T> nodeIdGenerator)
                : base(graph, edgeIdGenerator, nodeIdGenerator)
            { }

            #endregion

            #region Overrides

            protected override TDependentActivity CreateDummyActivity(T id)
            {
                return (TDependentActivity)DependentActivity<T>.CreateDependentActivityDummy(id);
            }

            #endregion
        }

        #endregion
    }
}
