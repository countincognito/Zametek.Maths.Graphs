using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs.Tests
{
    // A deterministic corpus of graphs used to prove that changes to the priority-list
    // calculation (or to the critical-path engine underneath it) leave its output
    // untouched. The priority list decides which activity is offered a resource first,
    // so it determines the final schedule: an optimisation that alters it is a
    // behaviour change, not an optimisation.
    //
    // The shapes are chosen to stress the parts of the calculation most likely to
    // reorder under a rewrite - depth, width, density, and above all ties, since
    // activities sharing a total slack value are separated only by the secondary
    // ordering.
    //
    // Determinism matters more than statistical quality here, because the output is
    // compared against a committed baseline. The generator therefore uses its own
    // xorshift generator rather than System.Random, whose sequence is not guaranteed
    // to stay stable across runtimes.
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

        // Small xorshift32 generator: fully specified, so the corpus is reproducible on
        // any runtime and the committed baseline stays valid.
        private sealed class DeterministicRandom
        {
            private uint m_State;

            internal DeterministicRandom(uint seed)
            {
                m_State = seed == 0 ? 1u : seed;
            }

            private uint NextUInt()
            {
                uint x = m_State;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                m_State = x;
                return x;
            }

            internal int Next(int maxExclusive) => (int)(NextUInt() % (uint)maxExclusive);

            internal int Next(int minInclusive, int maxExclusive) =>
                minInclusive + Next(maxExclusive - minInclusive);

            internal bool NextChance(int percent) => Next(100) < percent;
        }

        internal static List<Case> Generate()
        {
            var cases = new List<Case>();

            foreach (int size in new[] { 5, 12, 40, 90 })
            {
                cases.Add(new Case($@"chain-{size}", Chain(size, uniformDuration: false)));
                cases.Add(new Case($@"chain-tied-{size}", Chain(size, uniformDuration: true)));
                cases.Add(new Case($@"fan-out-{size}", FanOut(size)));
                cases.Add(new Case($@"fan-in-{size}", FanIn(size)));
                cases.Add(new Case($@"diamonds-{size}", Diamonds(size)));
            }

            foreach (int size in new[] { 20, 60, 120 })
            {
                foreach (int layers in new[] { 3, 6, 12 })
                {
                    cases.Add(new Case($@"layered-{size}-{layers}", Layered(size, layers, seed: (uint)(size * 31 + layers))));
                    cases.Add(new Case($@"layered-tied-{size}-{layers}", LayeredTiedDurations(size, layers, seed: (uint)(size * 17 + layers))));
                }
            }

            foreach (int size in new[] { 15, 45, 100 })
            {
                foreach (int density in new[] { 10, 30, 60 })
                {
                    cases.Add(new Case($@"random-{size}-{density}", RandomDag(size, density, seed: (uint)(size * 13 + density))));
                }
            }

            foreach (int size in new[] { 10, 35, 80 })
            {
                cases.Add(new Case($@"disconnected-{size}", Disconnected(size, seed: (uint)(size * 7))));
                cases.Add(new Case($@"zero-durations-{size}", WithZeroDurations(size, seed: (uint)(size * 11))));
                cases.Add(new Case($@"constrained-{size}", WithTimeConstraints(size, seed: (uint)(size * 19))));
            }

            return cases;
        }

        #region Shapes

        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> CreateBuilder() =>
            new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(new NextIdGenerator<int>(0));

        // 1 -> 2 -> 3 -> ... : maximum depth, minimum width.
        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> Chain(int size, bool uniformDuration)
        {
            var graphBuilder = CreateBuilder();
            for (int id = 1; id <= size; id++)
            {
                int duration = uniformDuration ? 5 : 1 + (id % 9);
                graphBuilder.AddActivity(
                    new DependentActivity<int, int, int>(id, duration),
                    id == 1 ? [] : [id - 1]);
            }
            return graphBuilder;
        }

        // One root that everything else depends on: minimum depth, maximum width, and
        // every dependent shares the same slack, so the whole graph is one large tie.
        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> FanOut(int size)
        {
            var graphBuilder = CreateBuilder();
            graphBuilder.AddActivity(new DependentActivity<int, int, int>(1, 5));
            for (int id = 2; id <= size; id++)
            {
                graphBuilder.AddActivity(new DependentActivity<int, int, int>(id, 5), [1]);
            }
            return graphBuilder;
        }

        // Many independent activities converging on a single sink.
        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> FanIn(int size)
        {
            var graphBuilder = CreateBuilder();
            var sources = new HashSet<int>();
            for (int id = 1; id < size; id++)
            {
                graphBuilder.AddActivity(new DependentActivity<int, int, int>(id, 1 + (id % 7)));
                sources.Add(id);
            }
            graphBuilder.AddActivity(new DependentActivity<int, int, int>(size, 4), sources);
            return graphBuilder;
        }

        // Repeated split-and-join: the two middle activities of each diamond tie on slack.
        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> Diamonds(int size)
        {
            var graphBuilder = CreateBuilder();
            int id = 1;
            int previousJoin = 0;
            while (id + 3 <= size)
            {
                int split = id++;
                int left = id++;
                int right = id++;
                int join = id++;
                graphBuilder.AddActivity(
                    new DependentActivity<int, int, int>(split, 2),
                    previousJoin == 0 ? [] : [previousJoin]);
                graphBuilder.AddActivity(new DependentActivity<int, int, int>(left, 3), [split]);
                graphBuilder.AddActivity(new DependentActivity<int, int, int>(right, 3), [split]);
                graphBuilder.AddActivity(new DependentActivity<int, int, int>(join, 2), [left, right]);
                previousJoin = join;
            }
            while (id <= size)
            {
                graphBuilder.AddActivity(
                    new DependentActivity<int, int, int>(id, 2),
                    previousJoin == 0 ? [] : [previousJoin]);
                previousJoin = id;
                id++;
            }
            return graphBuilder;
        }

        // Layered DAG: each activity depends on up to two activities in the layer above.
        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> Layered(int size, int layers, uint seed)
        {
            return LayeredCore(size, layers, seed, uniformDuration: false);
        }

        // As above, but every duration is identical so that total slack ties are
        // widespread - the case most likely to expose an ordering change.
        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> LayeredTiedDurations(int size, int layers, uint seed)
        {
            return LayeredCore(size, layers, seed, uniformDuration: true);
        }

        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> LayeredCore(
            int size, int layers, uint seed, bool uniformDuration)
        {
            var graphBuilder = CreateBuilder();
            var rng = new DeterministicRandom(seed);
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
                        dependencies.Add(rng.Next(previousStart, previousEnd + 1));
                        dependencies.Add(rng.Next(previousStart, previousEnd + 1));
                    }
                }
                int duration = uniformDuration ? 4 : rng.Next(1, 10);
                graphBuilder.AddActivity(
                    new DependentActivity<int, int, int>(id, duration),
                    dependencies);
            }
            return graphBuilder;
        }

        // Every activity may depend on any lower-numbered activity, which keeps the
        // graph acyclic while varying density.
        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> RandomDag(int size, int densityPercent, uint seed)
        {
            var graphBuilder = CreateBuilder();
            var rng = new DeterministicRandom(seed);
            for (int id = 1; id <= size; id++)
            {
                var dependencies = new HashSet<int>();
                for (int candidate = 1; candidate < id; candidate++)
                {
                    if (rng.NextChance(densityPercent))
                    {
                        dependencies.Add(candidate);
                    }
                }
                graphBuilder.AddActivity(new DependentActivity<int, int, int>(id, rng.Next(1, 8)), dependencies);
            }
            return graphBuilder;
        }

        // Several independent components plus isolated activities, so the calculation
        // has to cope with more than one critical path at a time.
        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> Disconnected(int size, uint seed)
        {
            var graphBuilder = CreateBuilder();
            var rng = new DeterministicRandom(seed);
            int componentStart = 1;
            for (int id = 1; id <= size; id++)
            {
                bool startsNewComponent = id == componentStart;
                var dependencies = new HashSet<int>();
                if (!startsNewComponent && rng.NextChance(70))
                {
                    dependencies.Add(rng.Next(componentStart, id));
                }
                graphBuilder.AddActivity(new DependentActivity<int, int, int>(id, rng.Next(1, 8)), dependencies);
                if (rng.NextChance(25))
                {
                    componentStart = id + 1;
                }
            }
            return graphBuilder;
        }

        // Zero-duration activities count as dummies, so they never enter the priority
        // list - but they still carry dependencies through the graph.
        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> WithZeroDurations(int size, uint seed)
        {
            var graphBuilder = CreateBuilder();
            var rng = new DeterministicRandom(seed);
            for (int id = 1; id <= size; id++)
            {
                var dependencies = new HashSet<int>();
                if (id > 1)
                {
                    dependencies.Add(rng.Next(1, id));
                }
                int duration = rng.NextChance(30) ? 0 : rng.Next(1, 8);
                graphBuilder.AddActivity(new DependentActivity<int, int, int>(id, duration), dependencies);
            }
            return graphBuilder;
        }

        // Time constraints alter earliest start and latest finish times, which feed the
        // slack values the selection is based on. The constraints are kept mutually
        // consistent so that the graph still compiles.
        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> WithTimeConstraints(int size, uint seed)
        {
            var graphBuilder = CreateBuilder();
            var rng = new DeterministicRandom(seed);
            for (int id = 1; id <= size; id++)
            {
                var dependencies = new HashSet<int>();
                if (id > 1)
                {
                    dependencies.Add(rng.Next(1, id));
                }
                var activity = new DependentActivity<int, int, int>(id, rng.Next(1, 8));

                // Never set MinimumFreeSlack and MaximumLatestFinishTime together - the
                // compiler rejects that combination as a contradictory constraint.
                int choice = rng.Next(4);
                if (choice == 0)
                {
                    activity.MinimumEarliestStartTime = rng.Next(0, 20);
                }
                else if (choice == 1)
                {
                    activity.MinimumFreeSlack = rng.Next(0, 5);
                }
                else if (choice == 2)
                {
                    // Generous enough that it stays satisfiable for a graph this size.
                    activity.MaximumLatestFinishTime = 5_000 + rng.Next(0, 500);
                }

                graphBuilder.AddActivity(activity, dependencies);
            }
            return graphBuilder;
        }

        #endregion
    }
}
