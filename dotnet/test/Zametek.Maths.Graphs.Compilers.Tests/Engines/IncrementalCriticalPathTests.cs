using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // The safety net for the incremental critical-path calculation.
    //
    // The priority-list calculation changes one activity's duration per iteration and
    // recalculates. Doing that incrementally is only worth anything if it is exactly
    // equivalent to recalculating in full, and "exactly" means every value, not just the
    // ones the priority-list selection happens to read - a future caller may read the
    // rest, and the two paths must not drift.
    //
    // So these compare the incremental path against the full one directly, rather than
    // against a recorded baseline: the full passes are the specification, and the test
    // runs both and demands they agree. The committed priority-list baseline covers the
    // end result on top of this.
    public class IncrementalCriticalPathTests
    {
        // Declines to begin an incremental calculation, so the builder falls back to
        // recalculating in full after every duration change - the behaviour these tests
        // compare against.
        private sealed class FullRecalculationOnlyEngine
            : IVertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>
        {
            private readonly IVertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>> m_Inner =
                new VertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>();

            public bool CalculateCriticalPathForwardFlow(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints,
                bool shuffle) =>
                m_Inner.CalculateCriticalPathForwardFlow(state, invalidConstraints, shuffle);

            public bool CalculateCriticalPathBackwardFlow(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints,
                bool shuffle) =>
                m_Inner.CalculateCriticalPathBackwardFlow(state, invalidConstraints, shuffle);

            public bool BackFillIsolatedNodes(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints) =>
                m_Inner.BackFillIsolatedNodes(state, invalidConstraints);

            public IVertexIncrementalCriticalPath<int> BeginIncrementalCriticalPath(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state) => null;
        }

        // Captures the graph state on its way through, so a test can drive an incremental
        // session against the same graph a builder is using without the builder needing to
        // expose its state.
        private sealed class StateCapturingEngine
            : IVertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>
        {
            private readonly IVertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>> m_Inner =
                new VertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>();

            internal IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> State { get; private set; }

            public bool CalculateCriticalPathForwardFlow(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints,
                bool shuffle)
            {
                State = state;
                return m_Inner.CalculateCriticalPathForwardFlow(state, invalidConstraints, shuffle);
            }

            public bool CalculateCriticalPathBackwardFlow(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints,
                bool shuffle) =>
                m_Inner.CalculateCriticalPathBackwardFlow(state, invalidConstraints, shuffle);

            public bool BackFillIsolatedNodes(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints) =>
                m_Inner.BackFillIsolatedNodes(state, invalidConstraints);

            public IVertexIncrementalCriticalPath<int> BeginIncrementalCriticalPath(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state) =>
                m_Inner.BeginIncrementalCriticalPath(state);
        }

        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> Build(
            CorpusShapes.GraphSpec spec,
            IVertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>> engine = null)
        {
            var graphBuilder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
                {
                    EdgeIdGenerator = new NextIdGenerator<int>(1_000_000),
                    CriticalPathEngine = engine
                        ?? new VertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>(),
                });

            foreach (CorpusShapes.ActivitySpec activity in spec.Activities)
            {
                var dependentActivity = new DependentActivity<int, int, int>(activity.Id, activity.Duration)
                {
                    MinimumEarliestStartTime = activity.MinimumEarliestStartTime,
                    MinimumFreeSlack = activity.MinimumFreeSlack,
                    MaximumLatestFinishTime = activity.MaximumLatestFinishTime,
                };

                graphBuilder.AddActivity(dependentActivity, new HashSet<int>(activity.Dependencies));
            }

            return graphBuilder;
        }

        [Fact]
        public void PriorityList_GivenCorpus_ThenIncrementalMatchesFullRecalculation()
        {
            foreach (CorpusShapes.GraphSpec spec in CorpusShapes.Generate())
            {
                List<int> incremental = Build(spec).CalculateCriticalPathPriorityList();
                List<int> full = Build(spec, new FullRecalculationOnlyEngine()).CalculateCriticalPathPriorityList();

                incremental.ShouldBe(full, $@"case {spec.Name}");
            }
        }

        [Theory]
        // Deliberately larger and deeper than the corpus, because the corpus graphs are
        // small enough that a propagation bug could stop before it reached anything.
        [InlineData(200, 8)]
        [InlineData(200, 40)]
        [InlineData(400, 20)]
        [InlineData(400, 100)]
        public void PriorityList_GivenLayeredGraph_ThenIncrementalMatchesFullRecalculation(int size, int layers)
        {
            CorpusShapes.GraphSpec spec = LayeredSpec(size, layers);

            List<int> incremental = Build(spec).CalculateCriticalPathPriorityList();
            List<int> full = Build(spec, new FullRecalculationOnlyEngine()).CalculateCriticalPathPriorityList();

            incremental.ShouldBe(full);
            incremental.Count.ShouldBe(size);
        }

        [Fact]
        public void CriticalPathValues_GivenCorpus_WhenDurationsAreZeroedOneAtATime_ThenIncrementalMatchesFullRecalculation()
        {
            // The strongest of these: it compares every value the two flows produce, not
            // just the priority list, after every single duration change. Free slack is
            // included, which the priority-list selection never reads.
            foreach (CorpusShapes.GraphSpec spec in CorpusShapes.Generate())
            {
                var capturingEngine = new StateCapturingEngine();
                VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> incremental =
                    Build(spec, capturingEngine);
                VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> full = Build(spec);

                incremental.CalculateCriticalPath();
                full.CalculateCriticalPath();

                IVertexIncrementalCriticalPath<int> session =
                    capturingEngine.BeginIncrementalCriticalPath(capturingEngine.State);
                session.ShouldNotBeNull($@"case {spec.Name}");

                foreach (int activityId in spec.Activities.Select(x => x.Id).OrderBy(x => x))
                {
                    incremental.Activity(activityId).Duration = 0;
                    full.Activity(activityId).Duration = 0;

                    if (!session.ApplyDurationChange(activityId))
                    {
                        incremental.CalculateCriticalPath();
                    }
                    full.CalculateCriticalPath();

                    AssertSameValues(incremental, full, $@"case {spec.Name}, after zeroing {activityId}");
                }
            }
        }

        private static void AssertSameValues(
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> incremental,
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> full,
            string because)
        {
            Render(incremental).ShouldBe(Render(full), because);
        }

        // Every value the two flows are responsible for, ordered by ID so the comparison
        // does not depend on iteration order.
        private static string Render(
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> graphBuilder)
        {
            string activities = string.Join(
                @",",
                graphBuilder.Activities
                    .OrderBy(x => x.Id)
                    .Select(x => $@"{x.Id}={Show(x.EarliestStartTime)}/{Show(x.LatestFinishTime)}/{Show(x.FreeSlack)}"));

            string events = string.Join(
                @",",
                graphBuilder.Events
                    .OrderBy(x => x.Id)
                    .Select(x => $@"{x.Id}={Show(x.EarliestFinishTime)}/{Show(x.LatestFinishTime)}"));

            return $@"activities {activities} events {events}";
        }

        private static string Show(int? value) => value.HasValue ? value.Value.ToString() : @"-";

        // Each activity depends on two in the previous layer, which gives the propagation
        // somewhere to fan out to and somewhere to stop.
        private static CorpusShapes.GraphSpec LayeredSpec(int size, int layers)
        {
            var activities = new List<CorpusShapes.ActivitySpec>();
            int perLayer = Math.Max(1, size / layers);

            for (int id = 1; id <= size; id++)
            {
                int layer = (id - 1) / perLayer;
                var dependencies = new HashSet<int>();

                if (layer > 0)
                {
                    int previousStart = ((layer - 1) * perLayer) + 1;
                    int previousEnd = Math.Min(layer * perLayer, size);

                    if (previousEnd >= previousStart)
                    {
                        dependencies.Add(previousStart + ((id * 7) % (previousEnd - previousStart + 1)));
                        dependencies.Add(previousStart + ((id * 13) % (previousEnd - previousStart + 1)));
                    }
                }

                var spec = new CorpusShapes.ActivitySpec(id, 1 + (id % 9), dependencies);

                // A scattering of constraints, because they are what make the two passes
                // interesting - the maximum latest finish time in particular feeds the
                // earliest start time through the duration, so zeroing an activity can
                // raise its own earliest start rather than only lowering its finish.
                if (id % 17 == 0)
                {
                    spec.MinimumEarliestStartTime = id % 5;
                }
                if (id % 23 == 0)
                {
                    spec.MaximumLatestFinishTime = 400 + (id % 11);
                }
                else if (id % 29 == 0)
                {
                    spec.MinimumFreeSlack = id % 4;
                }

                activities.Add(spec);
            }

            return new CorpusShapes.GraphSpec($@"layered-{size}-{layers}", activities);
        }
    }
}
