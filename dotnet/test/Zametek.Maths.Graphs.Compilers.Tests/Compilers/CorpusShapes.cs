using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs.Tests
{
    // The graph shapes shared by the equivalence corpora, described as data rather than
    // as built graphs so that the same set can be constructed as either an
    // Activity-on-Vertex or an Activity-on-Arrow graph. Both representations describe the
    // same network, so pinning both against the same shapes keeps the two engines honest
    // about computing the same thing.
    //
    // The shapes are chosen to stress the parts of a critical-path calculation most likely
    // to change under a rewrite - depth, width, density, and above all ties, since
    // activities sharing a slack value are separated only by the secondary ordering.
    //
    // Determinism matters more than statistical quality here, because the output is
    // compared against a committed baseline. The generator therefore uses its own xorshift
    // generator rather than System.Random, whose sequence is not guaranteed to stay stable
    // across runtimes.
    internal static class CorpusShapes
    {
        #region Spec types

        internal sealed class ActivitySpec
        {
            internal ActivitySpec(int id, int duration, HashSet<int> dependencies)
            {
                Id = id;
                Duration = duration;
                Dependencies = dependencies;
            }

            internal int Id { get; }

            internal int Duration { get; }

            internal HashSet<int> Dependencies { get; }

            internal int? MinimumEarliestStartTime { get; set; }

            internal int? MinimumFreeSlack { get; set; }

            internal int? MaximumLatestFinishTime { get; set; }
        }

        internal sealed class GraphSpec
        {
            internal GraphSpec(string name, List<ActivitySpec> activities)
            {
                Name = name;
                Activities = activities;
            }

            internal string Name { get; }

            // Ordered: the corpora add activities in exactly this order, since insertion
            // order is part of what the baselines pin.
            internal List<ActivitySpec> Activities { get; }
        }

        #endregion

        // Small xorshift32 generator: fully specified, so the corpus is reproducible on
        // any runtime and the committed baselines stay valid.
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

        internal static List<GraphSpec> Generate()
        {
            var specs = new List<GraphSpec>();

            foreach (int size in new[] { 5, 12, 40, 90 })
            {
                specs.Add(new GraphSpec($@"chain-{size}", Chain(size, uniformDuration: false)));
                specs.Add(new GraphSpec($@"chain-tied-{size}", Chain(size, uniformDuration: true)));
                specs.Add(new GraphSpec($@"fan-out-{size}", FanOut(size)));
                specs.Add(new GraphSpec($@"fan-in-{size}", FanIn(size)));
                specs.Add(new GraphSpec($@"diamonds-{size}", Diamonds(size)));
            }

            foreach (int size in new[] { 20, 60, 120 })
            {
                foreach (int layers in new[] { 3, 6, 12 })
                {
                    specs.Add(new GraphSpec($@"layered-{size}-{layers}", LayeredCore(size, layers, seed: (uint)(size * 31 + layers), uniformDuration: false)));
                    specs.Add(new GraphSpec($@"layered-tied-{size}-{layers}", LayeredCore(size, layers, seed: (uint)(size * 17 + layers), uniformDuration: true)));
                }
            }

            foreach (int size in new[] { 15, 45, 100 })
            {
                foreach (int density in new[] { 10, 30, 60 })
                {
                    specs.Add(new GraphSpec($@"random-{size}-{density}", RandomDag(size, density, seed: (uint)(size * 13 + density))));
                }
            }

            foreach (int size in new[] { 10, 35, 80 })
            {
                specs.Add(new GraphSpec($@"disconnected-{size}", Disconnected(size, seed: (uint)(size * 7))));
                specs.Add(new GraphSpec($@"zero-durations-{size}", WithZeroDurations(size, seed: (uint)(size * 11))));
                specs.Add(new GraphSpec($@"constrained-{size}", WithTimeConstraints(size, seed: (uint)(size * 19))));
            }

            return specs;
        }

        #region Shapes

        // 1 -> 2 -> 3 -> ... : maximum depth, minimum width.
        private static List<ActivitySpec> Chain(int size, bool uniformDuration)
        {
            var activities = new List<ActivitySpec>();
            for (int id = 1; id <= size; id++)
            {
                int duration = uniformDuration ? 5 : 1 + (id % 9);
                activities.Add(new ActivitySpec(id, duration, id == 1 ? [] : [id - 1]));
            }
            return activities;
        }

        // One root that everything else depends on: minimum depth, maximum width, and
        // every dependent shares the same slack, so the whole graph is one large tie.
        private static List<ActivitySpec> FanOut(int size)
        {
            var activities = new List<ActivitySpec> { new ActivitySpec(1, 5, []) };
            for (int id = 2; id <= size; id++)
            {
                activities.Add(new ActivitySpec(id, 5, [1]));
            }
            return activities;
        }

        // Many independent activities converging on a single sink.
        private static List<ActivitySpec> FanIn(int size)
        {
            var activities = new List<ActivitySpec>();
            var sources = new HashSet<int>();
            for (int id = 1; id < size; id++)
            {
                activities.Add(new ActivitySpec(id, 1 + (id % 7), []));
                sources.Add(id);
            }
            activities.Add(new ActivitySpec(size, 4, sources));
            return activities;
        }

        // Repeated split-and-join: the two middle activities of each diamond tie on slack.
        private static List<ActivitySpec> Diamonds(int size)
        {
            var activities = new List<ActivitySpec>();
            int id = 1;
            int previousJoin = 0;
            while (id + 3 <= size)
            {
                int split = id++;
                int left = id++;
                int right = id++;
                int join = id++;
                activities.Add(new ActivitySpec(split, 2, previousJoin == 0 ? [] : [previousJoin]));
                activities.Add(new ActivitySpec(left, 3, [split]));
                activities.Add(new ActivitySpec(right, 3, [split]));
                activities.Add(new ActivitySpec(join, 2, [left, right]));
                previousJoin = join;
            }
            while (id <= size)
            {
                activities.Add(new ActivitySpec(id, 2, previousJoin == 0 ? [] : [previousJoin]));
                previousJoin = id;
                id++;
            }
            return activities;
        }

        // Layered DAG: each activity depends on up to two activities in the layer above.
        // With uniformDuration every duration is identical so that slack ties are
        // widespread - the case most likely to expose an ordering change.
        private static List<ActivitySpec> LayeredCore(int size, int layers, uint seed, bool uniformDuration)
        {
            var activities = new List<ActivitySpec>();
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
                activities.Add(new ActivitySpec(id, duration, dependencies));
            }
            return activities;
        }

        // Every activity may depend on any lower-numbered activity, which keeps the graph
        // acyclic while varying density.
        private static List<ActivitySpec> RandomDag(int size, int densityPercent, uint seed)
        {
            var activities = new List<ActivitySpec>();
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
                activities.Add(new ActivitySpec(id, rng.Next(1, 8), dependencies));
            }
            return activities;
        }

        // Several independent components plus isolated activities, so the calculation has
        // to cope with more than one critical path at a time.
        private static List<ActivitySpec> Disconnected(int size, uint seed)
        {
            var activities = new List<ActivitySpec>();
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
                activities.Add(new ActivitySpec(id, rng.Next(1, 8), dependencies));
                if (rng.NextChance(25))
                {
                    componentStart = id + 1;
                }
            }
            return activities;
        }

        // Zero-duration activities count as dummies, so they never enter the priority
        // list - but they still carry dependencies through the graph.
        private static List<ActivitySpec> WithZeroDurations(int size, uint seed)
        {
            var activities = new List<ActivitySpec>();
            var rng = new DeterministicRandom(seed);
            for (int id = 1; id <= size; id++)
            {
                var dependencies = new HashSet<int>();
                if (id > 1)
                {
                    dependencies.Add(rng.Next(1, id));
                }
                int duration = rng.NextChance(30) ? 0 : rng.Next(1, 8);
                activities.Add(new ActivitySpec(id, duration, dependencies));
            }
            return activities;
        }

        // Time constraints alter earliest start and latest finish times, which feed the
        // slack values the selection is based on. The constraints are kept mutually
        // consistent so that the graph still compiles.
        private static List<ActivitySpec> WithTimeConstraints(int size, uint seed)
        {
            var activities = new List<ActivitySpec>();
            var rng = new DeterministicRandom(seed);
            for (int id = 1; id <= size; id++)
            {
                var dependencies = new HashSet<int>();
                if (id > 1)
                {
                    dependencies.Add(rng.Next(1, id));
                }
                var activity = new ActivitySpec(id, rng.Next(1, 8), dependencies);

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

                activities.Add(activity);
            }
            return activities;
        }

        #endregion
    }
}
