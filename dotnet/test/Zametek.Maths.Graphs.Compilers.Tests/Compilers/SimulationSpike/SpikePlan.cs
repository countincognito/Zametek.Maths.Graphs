using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Zametek.Maths.Graphs.Tests
{
    // A plan described as plain data, so that the spike can build it as many compilers as
    // it likes, give each of them its own durations, and compare what they compute.
    //
    // Three kinds of plan come out of it: the sample plan of the application (46 activities
    // and 15 resources, the largest real plan available), a layered synthetic plan of any
    // size for the scaling measurements, and the sample of the reference tool that the
    // simulation work follows (15 activities and no resources).
    internal sealed class SpikePlan
    {
        #region Records

        internal sealed class ActivityRecord
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;

            // The most likely duration, which is what the application stores.
            public int Duration { get; set; }

            public List<int> Dependencies { get; set; } = new List<int>();

            public List<int> PlanningDependencies { get; set; } = new List<int>();

            public List<int> TargetResources { get; set; } = new List<int>();

            public List<int> TargetWorkStreams { get; set; } = new List<int>();

            public int TargetResourceOperator { get; set; }

            public bool CanBeRemoved { get; set; }

            // The other two estimates. A plan that does not set them has no uncertainty.
            public int Optimistic { get; set; }

            public int Pessimistic { get; set; }
        }

        internal sealed class ResourceRecord
        {
            public int Id { get; set; }

            public bool IsExplicitTarget { get; set; }

            public bool IsInactive { get; set; }

            public int InterActivityAllocationType { get; set; }

            public List<int> InterActivityPhases { get; set; } = new List<int>();

            public int AllocationOrder { get; set; }
        }

        #endregion

        public string Description { get; set; } = string.Empty;

        // The finish time that the application stored when it compiled the plan.
        public int StoredFinishTime { get; set; }

        public List<int> WorkStreams { get; set; } = new List<int>();

        public List<ResourceRecord> Resources { get; set; } = new List<ResourceRecord>();

        public List<ActivityRecord> Activities { get; set; } = new List<ActivityRecord>();

        internal string Name { get; set; } = string.Empty;

        #region Plans

        internal static SpikePlan LoadSample()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Compilers", "TestFiles", "SamplePlan.json");
            SpikePlan plan = JsonSerializer.Deserialize<SpikePlan>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            plan.Name = "sample-46";
            plan.SetEstimatesFromMostLikely(optimisticFactor: 0.75, pessimisticFactor: 1.6);
            return plan;
        }

        // The same layered graph that CompileScalingTests uses, so that the numbers of the
        // two can be read side by side. The resources are not explicit targets and the
        // activities name none, which is the plain pool the scaling tests measure.
        internal static SpikePlan Layered(int size, int layers, int resourceCount)
        {
            var plan = new SpikePlan { Name = $@"layered-{size}" };
            int perLayer = Math.Max(1, size / layers);
            for (int id = 1; id <= size; id++)
            {
                int layer = (id - 1) / perLayer;
                var record = new ActivityRecord { Id = id, Name = id.ToString(), Duration = 1 + (id % 9) };
                if (layer > 0)
                {
                    int previousStart = ((layer - 1) * perLayer) + 1;
                    int previousEnd = Math.Min(layer * perLayer, size);
                    if (previousEnd >= previousStart)
                    {
                        record.Dependencies.Add(previousStart + ((id * 7) % (previousEnd - previousStart + 1)));
                        record.Dependencies.Add(previousStart + ((id * 13) % (previousEnd - previousStart + 1)));
                    }
                }
                plan.Activities.Add(record);
            }
            for (int id = 1; id <= resourceCount; id++)
            {
                plan.Resources.Add(new ResourceRecord { Id = id });
            }
            plan.SetEstimatesFromMostLikely(optimisticFactor: 0.75, pessimisticFactor: 1.6);
            return plan;
        }

        // The sample of the reference tool: Start, the activities A to O, and Finish.
        // Ids are 1 (Start), 2 to 16 (A to O) and 17 (Finish). No resources.
        internal static SpikePlan ReferenceSample()
        {
            var plan = new SpikePlan { Name = "reference-15" };
            string[] letters = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O" };
            int[][] estimates =
            {
                new[] { 2, 4, 6 }, new[] { 4, 5, 12 }, new[] { 3, 5, 7 }, new[] { 7, 10, 13 }, new[] { 11, 12, 13 },
                new[] { 5, 6, 7 }, new[] { 9, 10, 11 }, new[] { 5, 7, 9 }, new[] { 7, 9, 11 }, new[] { 1, 5, 9 },
                new[] { 15, 17, 19 }, new[] { 6, 8, 10 }, new[] { 9, 10, 17 }, new[] { 12, 14, 16 }, new[] { 16, 17, 18 },
            };
            string[] predecessors =
            {
                "Start", "Start", "B", "A", "A", "B,D", "C", "C,F", "D", "E", "G", "H", "I,J", "G", "L,M",
            };

            int IdOf(string name) =>
                name == "Start" ? 1 : name == "Finish" ? 17 : Array.IndexOf(letters, name) + 2;

            plan.Activities.Add(new ActivityRecord { Id = 1, Name = "Start" });
            for (int i = 0; i < letters.Length; i++)
            {
                var record = new ActivityRecord
                {
                    Id = IdOf(letters[i]),
                    Name = letters[i],
                    Optimistic = estimates[i][0],
                    Duration = estimates[i][1],
                    Pessimistic = estimates[i][2],
                };
                foreach (string predecessor in predecessors[i].Split(','))
                {
                    record.Dependencies.Add(IdOf(predecessor));
                }
                plan.Activities.Add(record);
            }
            var finish = new ActivityRecord { Id = 17, Name = "Finish" };
            foreach (string predecessor in new[] { "K", "N", "O" })
            {
                finish.Dependencies.Add(IdOf(predecessor));
            }
            plan.Activities.Add(finish);
            return plan;
        }

        // For the plans that carry only a most likely duration. The factors are an assumption
        // made for the measurements and nothing more: the cost of an iteration does not
        // depend on how wide the estimates are.
        internal void SetEstimatesFromMostLikely(double optimisticFactor, double pessimisticFactor)
        {
            foreach (ActivityRecord activity in Activities)
            {
                activity.Optimistic = (int)Math.Round(activity.Duration * optimisticFactor, MidpointRounding.AwayFromZero);
                activity.Pessimistic = (int)Math.Round(activity.Duration * pessimisticFactor, MidpointRounding.AwayFromZero);
            }
        }

        #endregion

        #region Building

        internal int[] MostLikelyDurations() => Activities.Select(x => x.Duration).ToArray();

        internal DependentActivity<int, int, int> NewActivity(ActivityRecord record, int duration) =>
            new DependentActivity<int, int, int>(
                record.Id,
                record.Name,
                string.Empty,
                record.TargetWorkStreams,
                record.TargetResources,
                record.Dependencies,
                record.PlanningDependencies,
                Array.Empty<int>(),
                Array.Empty<int>(),
                (LogicalOperator)record.TargetResourceOperator,
                Array.Empty<int>(),
                record.CanBeRemoved,
                false,
                false,
                false,
                duration,
                null,
                null,
                null,
                null,
                null,
                null);

        // A new compiler holding a new copy of every activity, with the given durations
        // (in the order of Activities).
        internal VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> NewCompiler(IReadOnlyList<int> durations)
        {
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            for (int i = 0; i < Activities.Count; i++)
            {
                compiler.AddActivity(NewActivity(Activities[i], durations[i]));
            }
            return compiler;
        }

        internal List<IResource<int, int>> NewResources(bool useResources) =>
            useResources
                ? Resources
                    .OrderBy(x => x.Id)
                    .Select(x => (IResource<int, int>)new Resource<int, int>(
                        x.Id,
                        string.Empty,
                        x.IsExplicitTarget,
                        x.IsInactive,
                        (InterActivityAllocationType)x.InterActivityAllocationType,
                        1.0,
                        1.0,
                        x.AllocationOrder,
                        x.InterActivityPhases))
                    .ToList()
                : new List<IResource<int, int>>();

        internal List<IWorkStream<int>> NewWorkStreams() =>
            WorkStreams.Select(x => (IWorkStream<int>)new WorkStream<int>(x, string.Empty, true)).ToList();

        // What a full compile says about the plan with these durations, on a compiler made
        // for the purpose. This is the oracle the lean path is held to.
        internal (int Finish, bool[] Critical, int ErrorCount) CompileOracle(int[] durations, bool useResources)
        {
            VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler = NewCompiler(durations);
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile(NewResources(useResources), NewWorkStreams(), CancellationToken.None);

            int errors = compilation.CompilationErrors.Count();
            var critical = new bool[Activities.Count];
            Dictionary<int, IDependentActivity<int, int, int>> byId = compilation.DependentActivities.ToDictionary(x => x.Id);
            if (errors == 0)
            {
                for (int i = 0; i < Activities.Count; i++)
                {
                    critical[i] = byId[Activities[i].Id].IsCritical;
                }
            }
            return (errors == 0 ? compiler.FinishTime : -1, critical, errors);
        }

        #endregion
    }
}
