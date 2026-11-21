using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace Zametek.Maths.Graphs.Tests
{
    // Random plans for the comparison of a whole compile between the two languages. The Rust port has the same generator,
    // value for value (rust/compilers/tests/compile_parity_corpus/mod.rs), and so the two languages build the same plan
    // for the same category and index. Each compiles its plan, writes the result as one canonical text, and takes a hash
    // of that text. The hashes are in one baseline file, which C# writes and both check (CompileParityTests).
    //
    // The plans come from the xorshift generator of CorpusShapes, not from System.Random, which Rust cannot reproduce.
    //
    // A change here needs the same change in the Rust file, and a new baseline. The order of the calls to the generator
    // is part of the format: one call more or less on one side gives different plans.
    internal static class CompileParityCorpus
    {
        // Plans with 2 to 4 resources and targets, and no constraints.
        internal const string Resources = @"resources";

        // The same, with constraints (a minimum EST, a maximum LFT or a minimum free slack) and milestones.
        internal const string ResourcesConstrained = @"resources-constrained";

        // The activities of 'resources' with no targets, compiled with no resources.
        internal const string NoResources = @"no-resources";

        // The activities of 'resources-constrained' with no targets, compiled with no resources.
        internal const string NoResourcesConstrained = @"no-resources-constrained";

        // In the order of the baseline file.
        internal static readonly string[] Categories = [Resources, ResourcesConstrained, NoResources, NoResourcesConstrained];

        #region Spec types

        internal sealed class ResourceSpec
        {
            internal ResourceSpec(int id, bool isExplicitTarget, int allocationOrder)
            {
                Id = id;
                IsExplicitTarget = isExplicitTarget;
                AllocationOrder = allocationOrder;
            }

            internal int Id { get; }

            internal bool IsExplicitTarget { get; }

            internal int AllocationOrder { get; }
        }

        internal sealed class ActivitySpec
        {
            internal ActivitySpec(int id, int duration, List<int> dependencies, List<int> targetResources, LogicalOperator targetResourceOperator)
            {
                Id = id;
                Duration = duration;
                Dependencies = dependencies;
                TargetResources = targetResources;
                TargetResourceOperator = targetResourceOperator;
            }

            internal int Id { get; }

            internal int Duration { get; }

            // In ascending order.
            internal List<int> Dependencies { get; }

            // In the order in which they are added to the activity. The order is part of the plan: the two languages
            // must add the targets in the same order.
            internal List<int> TargetResources { get; }

            internal LogicalOperator TargetResourceOperator { get; }

            // At most one of the three constraints is set.
            internal int? MinimumEarliestStartTime { get; set; }

            internal int? MaximumLatestFinishTime { get; set; }

            internal int? MinimumFreeSlack { get; set; }
        }

        internal sealed class PlanSpec
        {
            internal PlanSpec(string category, int index, List<ResourceSpec> resources, List<ActivitySpec> activities)
            {
                Category = category;
                Index = index;
                Resources = resources;
                Activities = activities;
            }

            internal string Category { get; }

            internal int Index { get; }

            // Empty for the categories with no resources. The compile gets them in this order.
            internal List<ResourceSpec> Resources { get; }

            // In ID order. The compile gets them in this order.
            internal List<ActivitySpec> Activities { get; }
        }

        // The result of the compile of one plan.
        internal sealed class CompileResult
        {
            internal CompileResult(int finishTime, string errorCodes, string text)
            {
                FinishTime = finishTime;
                ErrorCodes = errorCodes;
                Text = text;
                Hash = Fnv1a64(text);
            }

            internal int FinishTime { get; }

            // The error codes in order, with a comma between them, or '-' for none.
            internal string ErrorCodes { get; }

            // The canonical text of the whole result.
            internal string Text { get; }

            // The 64-bit FNV-1a hash of the text.
            internal ulong Hash { get; }
        }

        #endregion

        #region Generator

        // The plan of one category and index. The same call gives the same plan each time, in both languages.
        internal static PlanSpec Generate(string category, int index)
        {
            bool withResources = category == Resources || category == ResourcesConstrained;
            bool constrained = category == ResourcesConstrained || category == NoResourcesConstrained;
            if (!withResources && category != NoResources && category != NoResourcesConstrained)
            {
                throw new ArgumentException($@"Unknown category: {category}", nameof(category));
            }

            // The multiplier is odd, and so each index has its own seed. The plans with constraints have other seeds than
            // the plans without. The first values of an xorshift generator are much alike for seeds that are much alike,
            // and so the first eight are thrown away.
            uint seed = unchecked((((uint)index + 1u) * 2654435761u) + (constrained ? 0x5BD1E995u : 0u));
            var rng = new CorpusShapes.DeterministicRandom(seed);
            for (int i = 0; i < 8; i++)
            {
                rng.Next(2);
            }

            // 2 to 4 resources. Each is an explicit target or not, and the allocation orders can tie.
            int resourceCount = rng.Next(2, 5);
            var resources = new List<ResourceSpec>();
            bool anyOpenResource = false;
            for (int id = 1; id <= resourceCount; id++)
            {
                bool isExplicitTarget = rng.Next(2) == 0;
                int allocationOrder = rng.Next(resourceCount);
                resources.Add(new ResourceSpec(id, isExplicitTarget, allocationOrder));
                anyOpenResource |= !isExplicitTarget;
            }

            // 4 to 30 activities. Each earlier activity is a dependency with a chance of 2 in the count, and of 0.3 at most.
            int count = rng.Next(4, 31);
            int dependencyPerMille = Math.Min(300, 2000 / count);
            var activities = new List<ActivitySpec>();
            for (int id = 1; id <= count; id++)
            {
                var dependencies = new List<int>();
                for (int candidate = 1; candidate < id; candidate++)
                {
                    if (rng.Next(1000) < dependencyPerMille)
                    {
                        dependencies.Add(candidate);
                    }
                }

                int duration = rng.Next(1, 6);

                // About 35% no targets, 25% one target, 30% two or more targets with OR, and 10% two targets with AND or
                // ACTIVE_AND. An activity with no targets needs a resource that is not an explicit target. When the plan
                // has none, the activity gets one target, and so the compile never refuses the plan with P0040.
                var targetResources = new List<int>();
                LogicalOperator targetResourceOperator = LogicalOperator.AND;
                int kind = rng.Next(100);
                if (kind < 35 && anyOpenResource)
                {
                    // No targets.
                }
                else if (kind < 60)
                {
                    targetResources.Add(rng.Next(1, resourceCount + 1));
                    // With one target the operator changes nothing in the plan, but the scheduler moves only an activity
                    // with OR to another resource.
                    targetResourceOperator = rng.Next(4) == 0 ? LogicalOperator.AND : LogicalOperator.OR;
                }
                else if (kind < 90)
                {
                    int targetCount = rng.Next(2, resourceCount + 1);
                    targetResources = PickResources(rng, resourceCount, targetCount);
                    targetResourceOperator = LogicalOperator.OR;
                }
                else
                {
                    targetResources = PickResources(rng, resourceCount, 2);
                    targetResourceOperator = rng.Next(3) == 0 ? LogicalOperator.ACTIVE_AND : LogicalOperator.AND;
                }

                int? minimumEarliestStartTime = null;
                int? maximumLatestFinishTime = null;
                int? minimumFreeSlack = null;
                if (constrained)
                {
                    // About one activity in fifteen is a milestone.
                    if (rng.Next(15) == 0)
                    {
                        duration = 0;
                    }
                    // About one activity in seven has one constraint. Never a minimum free slack with a maximum LFT: the
                    // compile refuses that before it starts (P0030).
                    if (rng.Next(7) == 0)
                    {
                        int constraint = rng.Next(3);
                        if (constraint == 0)
                        {
                            minimumEarliestStartTime = rng.Next(1, 10);
                        }
                        else if (constraint == 1)
                        {
                            maximumLatestFinishTime = rng.Next(5, 60);
                        }
                        else
                        {
                            minimumFreeSlack = rng.Next(1, 4);
                        }
                    }
                }

                // The categories with no resources have the same activities with no targets.
                if (!withResources)
                {
                    targetResources = [];
                    targetResourceOperator = LogicalOperator.AND;
                }

                activities.Add(new ActivitySpec(id, duration, dependencies, targetResources, targetResourceOperator)
                {
                    MinimumEarliestStartTime = minimumEarliestStartTime,
                    MaximumLatestFinishTime = maximumLatestFinishTime,
                    MinimumFreeSlack = minimumFreeSlack,
                });
            }

            return new PlanSpec(category, index, withResources ? resources : [], activities);
        }

        // 'take' different resources of 1 to resourceCount, in a random order: the first steps of a Fisher-Yates shuffle.
        private static List<int> PickResources(CorpusShapes.DeterministicRandom rng, int resourceCount, int take)
        {
            int[] pool = [.. Enumerable.Range(1, resourceCount)];
            for (int i = 0; i < take; i++)
            {
                int j = rng.Next(i, resourceCount);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            return [.. pool.Take(take)];
        }

        #endregion

        #region Text

        // The plan as text, the same in both languages. A test that fails prints it, so that the plans of the two sides
        // can be put next to each other.
        internal static string FormatPlan(PlanSpec plan)
        {
            var output = new StringBuilder();
            output.Append(@"plan ").Append(plan.Category).Append(' ').Append(Number(plan.Index)).Append('\n');
            foreach (ResourceSpec resource in plan.Resources)
            {
                output.Append(@"resource ").Append(Number(resource.Id))
                    .Append(@" explicit-target=").Append(resource.IsExplicitTarget ? @"true" : @"false")
                    .Append(@" allocation-order=").Append(Number(resource.AllocationOrder))
                    .Append('\n');
            }
            foreach (ActivitySpec activity in plan.Activities)
            {
                output.Append(@"activity ").Append(Number(activity.Id))
                    .Append(@" duration=").Append(Number(activity.Duration))
                    .Append(@" dependencies=").Append(Numbers(activity.Dependencies))
                    .Append(@" targets=").Append(Numbers(activity.TargetResources))
                    .Append(@" operator=").Append(OperatorName(activity.TargetResourceOperator))
                    .Append(@" min-est=").Append(Number(activity.MinimumEarliestStartTime))
                    .Append(@" max-lft=").Append(Number(activity.MaximumLatestFinishTime))
                    .Append(@" min-free-slack=").Append(Number(activity.MinimumFreeSlack))
                    .Append('\n');
            }
            return output.ToString();
        }

        private static string OperatorName(LogicalOperator logicalOperator)
        {
            return logicalOperator switch
            {
                LogicalOperator.AND => @"AND",
                LogicalOperator.OR => @"OR",
                LogicalOperator.ACTIVE_AND => @"ACTIVE_AND",
                _ => throw new ArgumentOutOfRangeException(nameof(logicalOperator)),
            };
        }

        // The invariant culture: a culture can have another minus sign, and a slack can be negative.
        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        // '-' for a missing value.
        private static string Number(int? value) => value.HasValue ? Number(value.GetValueOrDefault()) : @"-";

        private static string Numbers(IEnumerable<int> values) => $@"[{string.Join(@",", values.Select(Number))}]";

        private static string SortedNumbers(IEnumerable<int> values) => Numbers(values.OrderBy(x => x));

        #endregion

        #region Compile

        // Compiles the plan, and writes the whole result as one canonical text. The Rust port writes the same text for the
        // same result, character for character:
        //
        //   errors C0010                     the error codes in order, with a comma between them; '-' for none
        //   error C0010                      then each error: its code, and each line of its message after '| '
        //   | Invalid activity constraints:
        //   | 3 -> LatestFinishTime cannot be more than MaximumLatestFinishTime
        //   |
        //   finish 12                        the finish time of the compiler
        //   activity 1 est=0 eft=3 lst=0 lft=3 free-slack=0 total-slack=0 allocated=[1] resource-dependencies=[] successors=[2,3]
        //   schedule 2 finish=12 activities=[1:0:3,4:3:5]
        //
        // The activities are in ID order, and their three lists are sorted. The schedules are in the order of the result:
        // the ID of the resource ('-' for a lane with no resource), the finish time of the schedule, and each scheduled
        // activity in the order of the result as id:start:finish. A missing value is '-'. Each line ends with '\n'.
        //
        // The line endings of an error message are made '\n', and nothing else of it changes: C# ends each line of a
        // message with Environment.NewLine, which is "\r\n" on Windows, and Rust ends it with "\n".
        internal static CompileResult Compile(PlanSpec plan, CancellationToken cancellationToken)
        {
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            foreach (ActivitySpec spec in plan.Activities)
            {
                var activity = new DependentActivity<int, int, int>(spec.Id, spec.Duration, spec.Dependencies)
                {
                    TargetResourceOperator = spec.TargetResourceOperator,
                    MinimumEarliestStartTime = spec.MinimumEarliestStartTime,
                    MaximumLatestFinishTime = spec.MaximumLatestFinishTime,
                    MinimumFreeSlack = spec.MinimumFreeSlack,
                };
                foreach (int targetResource in spec.TargetResources)
                {
                    activity.TargetResources.Add(targetResource);
                }
                compiler.AddActivity(activity);
            }

            List<IResource<int, int>> resources = [.. plan.Resources.Select(x => (IResource<int, int>)new Resource<int, int>(
                x.Id, $@"R{Number(x.Id)}", x.IsExplicitTarget, false, InterActivityAllocationType.Direct, 1.0, 1.0, x.AllocationOrder, []))];

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = compiler.Compile(resources, cancellationToken);

            List<IGraphCompilationError> errors = [.. compilation.CompilationErrors];
            string errorCodes = errors.Count == 0 ? @"-" : string.Join(@",", errors.Select(x => x.ErrorCode.ToString()));

            var output = new StringBuilder();
            output.Append(@"errors ").Append(errorCodes).Append('\n');
            foreach (IGraphCompilationError error in errors)
            {
                output.Append(@"error ").Append(error.ErrorCode.ToString()).Append('\n');
                string message = error.ErrorMessage.Replace("\r\n", "\n", StringComparison.Ordinal);
                foreach (string line in message.Split('\n'))
                {
                    output.Append(@"| ").Append(line).Append('\n');
                }
            }

            output.Append(@"finish ").Append(Number(compiler.FinishTime)).Append('\n');

            foreach (IDependentActivity<int, int, int> activity in compilation.DependentActivities.OrderBy(x => x.Id))
            {
                output.Append(@"activity ").Append(Number(activity.Id))
                    .Append(@" est=").Append(Number(activity.EarliestStartTime))
                    .Append(@" eft=").Append(Number(activity.EarliestFinishTime))
                    .Append(@" lst=").Append(Number(activity.LatestStartTime))
                    .Append(@" lft=").Append(Number(activity.LatestFinishTime))
                    .Append(@" free-slack=").Append(Number(activity.FreeSlack))
                    .Append(@" total-slack=").Append(Number(activity.TotalSlack))
                    .Append(@" allocated=").Append(SortedNumbers(activity.AllocatedToResources))
                    .Append(@" resource-dependencies=").Append(SortedNumbers(activity.ResourceDependencies))
                    .Append(@" successors=").Append(SortedNumbers(activity.Successors))
                    .Append('\n');
            }

            foreach (IResourceSchedule<int, int, int> schedule in compilation.ResourceSchedules)
            {
                output.Append(@"schedule ").Append(schedule.Resource is null ? @"-" : Number(schedule.Resource.Id))
                    .Append(@" finish=").Append(Number(schedule.FinishTime))
                    .Append(@" activities=[")
                    .Append(string.Join(@",", schedule.ScheduledActivities.Select(x => $@"{Number(x.Id)}:{Number(x.StartTime)}:{Number(x.FinishTime)}")))
                    .Append(@"]")
                    .Append('\n');
            }

            return new CompileResult(compiler.FinishTime, errorCodes, output.ToString());
        }

        // The 64-bit FNV-1a hash of the UTF-8 bytes of the text. Written out here, and in the same way in the Rust port:
        // a hash of a library can change, and then the baseline is no longer valid.
        internal static ulong Fnv1a64(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte value in Encoding.UTF8.GetBytes(text))
            {
                hash ^= value;
                hash = unchecked(hash * 1099511628211UL);
            }
            return hash;
        }

        // One line of the baseline file: category, index, finish time, error codes and hash, with one space between them.
        internal static string BaselineLine(PlanSpec plan, CompileResult result)
        {
            return $@"{plan.Category} {Number(plan.Index)} {Number(result.FinishTime)} {result.ErrorCodes} {result.Hash.ToString(@"x16", CultureInfo.InvariantCulture)}";
        }

        #endregion
    }
}
