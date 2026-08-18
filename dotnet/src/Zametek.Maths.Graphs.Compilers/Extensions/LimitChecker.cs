using System;
using System.Collections.Generic;
using System.Globalization;

namespace Zametek.Maths.Graphs
{
    // Shared domain-limit validation used by both ArrowGraphBuilder and VertexGraphBuilder.
    // Deliberately kept separate from ConstraintChecker: those checks describe constraints
    // that contradict each other and also gate the critical-path passes, whereas these
    // describe values that are simply out of range. Keeping them apart means an
    // out-of-range value is reported (as P0080) without changing how the critical-path
    // calculation behaves.
    internal static class LimitChecker<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        internal static List<string> FindLimitViolations(
            List<IActivity<T, TResourceId, TWorkStreamId>> activities,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            List<IWorkStream<TWorkStreamId>> workStreams)
        {
            var output = new List<string>();

            AddCountViolation(output, Properties.Resources.Message_Activities, activities?.Count ?? 0, GraphLimits.MaximumActivityCount);
            AddCountViolation(output, Properties.Resources.Message_Resources, filteredResources?.Count ?? 0, GraphLimits.MaximumResourceCount);
            AddCountViolation(output, Properties.Resources.Message_WorkStreams, workStreams?.Count ?? 0, GraphLimits.MaximumWorkStreamCount);

            if (activities is null)
            {
                return output;
            }

            foreach (IActivity<T, TResourceId, TWorkStreamId> activity in activities)
            {
                AddRangeViolation(output, activity.Id, nameof(activity.Duration), activity.Duration);
                AddRangeViolation(output, activity.Id, nameof(activity.MinimumEarliestStartTime), activity.MinimumEarliestStartTime);
                AddRangeViolation(output, activity.Id, nameof(activity.MaximumLatestFinishTime), activity.MaximumLatestFinishTime);
                AddRangeViolation(output, activity.Id, nameof(activity.MinimumFreeSlack), activity.MinimumFreeSlack);
            }

            return output;
        }

        private static void AddCountViolation(
            List<string> output,
            string itemDescription,
            int count,
            int maximum)
        {
            if (count > maximum)
            {
                output.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    Properties.Resources.Message_CountExceedsMaximum,
                    itemDescription,
                    count,
                    maximum));
            }
        }

        private static void AddRangeViolation(
            List<string> output,
            T activityId,
            string valueName,
            int? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            int actual = value.GetValueOrDefault();
            if (actual >= GraphLimits.MinimumTimeValue
                && actual <= GraphLimits.MaximumTimeValue)
            {
                return;
            }

            output.Add($@"{Properties.Resources.Message_Activity} {activityId} -> {string.Format(
                CultureInfo.CurrentCulture,
                Properties.Resources.Message_ValueOutsideSupportedRange,
                valueName,
                actual,
                GraphLimits.MinimumTimeValue,
                GraphLimits.MaximumTimeValue)}");
        }
    }
}
