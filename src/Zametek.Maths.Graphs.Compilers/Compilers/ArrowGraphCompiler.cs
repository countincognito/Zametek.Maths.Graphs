using System;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class ArrowGraphCompiler<T, TDependentActivity>
        : ArrowGraphCompilerBase<T, TDependentActivity, IActivity<T>, IEvent<T>>
        where TDependentActivity : class, IDependentActivity<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        protected ArrowGraphCompiler(ArrowGraphBuilderBase<T, TDependentActivity, IEvent<T>> arrowGraphBuilder)
            : base(arrowGraphBuilder)
        {
        }

        public ArrowGraphCompiler()
            : this(CreateDependentActivityArrowGraphBuilder())
        {
        }

        #endregion

        #region Private Methods

        private static DependentActivityArrowGraphBuilder CreateDependentActivityArrowGraphBuilder()
        {
            T edgeId = default;
            T nodeId = default;
            return new DependentActivityArrowGraphBuilder(
                () => edgeId = edgeId.Previous(),
                () => nodeId = nodeId.Previous());
        }

        #endregion

        #region Private Types

        private class DependentActivityArrowGraphBuilder
            : ArrowGraphBuilderBase<T, TDependentActivity, IEvent<T>>
        {
            #region Fields

            private static readonly Func<T, IEvent<T>> s_EventGenerator = (id) => new Event<T>(id);
            private static readonly Func<T, int?, int?, IEvent<T>> s_EventGeneratorEventWithTimes = (id, earliestFinishTime, latestFinishTime) => new Event<T>(id, earliestFinishTime, latestFinishTime);
            private static readonly Func<T, TDependentActivity> s_DummyActivityGenerator = (id) => new DependentActivity<T>(id, 0, canBeRemoved: true) as TDependentActivity;

            #endregion

            #region Ctors

            public DependentActivityArrowGraphBuilder(
                Func<T> edgeIdGenerator,
                Func<T> nodeIdGenerator)
                : base(
                      edgeIdGenerator,
                      nodeIdGenerator,
                      s_EventGenerator,
                      s_EventGeneratorEventWithTimes,
                      s_DummyActivityGenerator)
            {
            }

            public DependentActivityArrowGraphBuilder(
                Graph<T, TDependentActivity, IEvent<T>> graph,
                Func<T> edgeIdGenerator,
                Func<T> nodeIdGenerator)
                : base(
                      graph,
                      edgeIdGenerator,
                      nodeIdGenerator,
                      s_EventGenerator)
            {
            }

            #endregion

            #region Overrides

            public override object CloneObject()
            {
                Graph<T, TDependentActivity, IEvent<T>> arrowGraphCopy = ToGraph();

                T minNodeId = arrowGraphCopy.Nodes.Select(x => x.Id).DefaultIfEmpty().Min();
                minNodeId = minNodeId.Previous();

                T minEdgeId = arrowGraphCopy.Edges.Select(x => x.Id).DefaultIfEmpty().Min();
                minEdgeId = minEdgeId.Previous();

                return new DependentActivityArrowGraphBuilder(
                    arrowGraphCopy,
                    () => minEdgeId = minEdgeId.Previous(),
                    () => minNodeId = minNodeId.Previous());
            }

            #endregion
        }

        #endregion
    }
}
