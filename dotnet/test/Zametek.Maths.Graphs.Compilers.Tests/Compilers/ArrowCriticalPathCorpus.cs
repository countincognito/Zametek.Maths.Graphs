using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs.Tests
{
    // The Activity-on-Arrow view of the shared corpus, used to prove that changes to the
    // arrow critical-path engine leave its output untouched.
    //
    // The shapes are the same networks the vertex corpus builds (see CorpusShapes), so
    // both engines are pinned against the same graphs. What differs is the representation:
    // here activities sit on edges and events on nodes, and the builder inserts dummy
    // edges to express the dependencies, so the graph the engine walks is considerably
    // larger than the activity list that produced it.
    internal static class ArrowCriticalPathCorpus
    {
        // The dummy-activity and event ID generators start where the C# arrow tests start
        // them, so generated IDs are stable and the baseline stays valid.
        private const int c_FirstDummyActivityId = 100_000;
        private const int c_FirstEventId = 0;

        internal sealed class Case
        {
            internal Case(string name, ArrowGraphBuilder<int, int, int, IActivity<int, int, int>> graphBuilder)
            {
                Name = name;
                GraphBuilder = graphBuilder;
            }

            internal string Name { get; }

            internal ArrowGraphBuilder<int, int, int, IActivity<int, int, int>> GraphBuilder { get; }
        }

        internal static List<Case> Generate()
        {
            return CorpusShapes.Generate().Select(Build).ToList();
        }

        private static Case Build(CorpusShapes.GraphSpec spec)
        {
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(
                new NextIdGenerator<int>(c_FirstDummyActivityId),
                new NextIdGenerator<int>(c_FirstEventId));

            foreach (CorpusShapes.ActivitySpec activitySpec in spec.Activities)
            {
                var activity = new Activity<int, int, int>(activitySpec.Id, activitySpec.Duration)
                {
                    MinimumEarliestStartTime = activitySpec.MinimumEarliestStartTime,
                    MinimumFreeSlack = activitySpec.MinimumFreeSlack,
                    MaximumLatestFinishTime = activitySpec.MaximumLatestFinishTime,
                };
                graphBuilder.AddActivity(activity, activitySpec.Dependencies);
            }

            // The arrow critical-path tests all reduce before calculating, because the
            // builder's raw dependency wiring leaves redundant dummy edges behind that
            // change the free-slack values. Reducing here keeps the corpus consistent with
            // how the engine is actually driven.
            graphBuilder.TransitiveReduction();

            return new Case(spec.Name, graphBuilder);
        }
    }
}
