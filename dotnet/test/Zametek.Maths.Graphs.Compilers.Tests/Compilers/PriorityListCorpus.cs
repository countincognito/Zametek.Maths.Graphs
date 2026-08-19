using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs.Tests
{
    // The Activity-on-Vertex view of the shared corpus, used to prove that changes to the
    // priority-list calculation (or to the vertex critical-path engine underneath it)
    // leave its output untouched. The priority list decides which activity is offered a
    // resource first, so it determines the final schedule: an optimisation that alters it
    // is a behaviour change, not an optimisation.
    //
    // The shapes themselves, and the reasoning behind them, live in CorpusShapes - the
    // arrow corpus builds the same networks, so the two engines are pinned against the
    // same set of graphs.
    internal static class PriorityListCorpus
    {
        internal sealed class Case
        {
            internal Case(string name, VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> graphBuilder)
            {
                Name = name;
                GraphBuilder = graphBuilder;
            }

            internal string Name { get; }

            internal VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> GraphBuilder { get; }
        }

        internal static List<Case> Generate()
        {
            return CorpusShapes.Generate().Select(Build).ToList();
        }

        private static Case Build(CorpusShapes.GraphSpec spec)
        {
            var graphBuilder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new NextIdGenerator<int>(0));

            foreach (CorpusShapes.ActivitySpec activitySpec in spec.Activities)
            {
                var activity = new DependentActivity<int, int, int>(activitySpec.Id, activitySpec.Duration)
                {
                    MinimumEarliestStartTime = activitySpec.MinimumEarliestStartTime,
                    MinimumFreeSlack = activitySpec.MinimumFreeSlack,
                    MaximumLatestFinishTime = activitySpec.MaximumLatestFinishTime,
                };
                graphBuilder.AddActivity(activity, activitySpec.Dependencies);
            }

            return new Case(spec.Name, graphBuilder);
        }
    }
}
