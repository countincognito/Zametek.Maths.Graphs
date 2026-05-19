using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Shared constraint validation logic used by both ArrowGraphBuilder and VertexGraphBuilder.
    // Both builders have identical pre- and post-compilation constraint checks, so they live here.
    internal static class ConstraintChecker<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        internal static List<IInvalidConstraint<T>> FindInvalidPreCompilationConstraints(
            List<IActivity<T, TResourceId, TWorkStreamId>> activities)
        {
            var output = new List<IInvalidConstraint<T>>();

            foreach (IActivity<T, TResourceId, TWorkStreamId> activity in activities)
            {
                if (activity.MinimumFreeSlack.HasValue && activity.MaximumLatestFinishTime.HasValue)
                {
                    output.Add(new InvalidConstraint<T>(
                        activity.Id,
                        Properties.Resources.Message_CannotSetMinimumFreeSlackAndMaximumLatestFinishTime));
                    continue;
                }

                if (activity.MinimumEarliestStartTime.HasValue
                    && activity.MaximumLatestFinishTime.HasValue
                    && (activity.MinimumEarliestStartTime.Value + activity.Duration) > activity.MaximumLatestFinishTime.Value)
                {
                    output.Add(new InvalidConstraint<T>(
                        activity.Id,
                        Properties.Resources.Message_MinimumEarliestStartTimePlusDurationMustBeGreaterThanMaximumLatestFinishTime));
                }
            }

            return output;
        }

        internal static List<IInvalidConstraint<T>> FindInvalidPostCompilationConstraints(
            List<IActivity<T, TResourceId, TWorkStreamId>> activities)
        {
            var output = new List<IInvalidConstraint<T>>();

            foreach (IActivity<T, TResourceId, TWorkStreamId> activity in activities)
            {
                CheckEarlyTimes(activity, output);
                CheckLateTimes(activity, output);
                CheckStartTimeOrder(activity, output);
                CheckFinishTimeOrder(activity, output);
                CheckEarliestStartConstraint(activity, output);
                CheckLatestFinishConstraint(activity, output);
                CheckFreeSlackConstraint(activity, output);
            }

            return output;
        }

        private static void CheckEarlyTimes(
            IActivity<T, TResourceId, TWorkStreamId> activity,
            List<IInvalidConstraint<T>> output)
        {
            if (!activity.EarliestStartTime.HasValue || !activity.EarliestFinishTime.HasValue)
            {
                return;
            }

            if (activity.EarliestStartTime < 0)
            {
                output.Add(new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_EarliestStartTimeLessThanZero));
            }

            if (activity.EarliestFinishTime < 0)
            {
                output.Add(new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_EarliestFinishTimeLessThanZero));
            }
        }

        private static void CheckLateTimes(
            IActivity<T, TResourceId, TWorkStreamId> activity,
            List<IInvalidConstraint<T>> output)
        {
            if (!activity.LatestStartTime.HasValue || !activity.LatestFinishTime.HasValue)
            {
                return;
            }

            if (activity.LatestStartTime < 0)
            {
                output.Add(new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_LatestStartTimeLessThanZero));
            }

            if (activity.LatestFinishTime < 0)
            {
                output.Add(new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_LatestFinishTimeLessThanZero));
            }
        }

        private static void CheckStartTimeOrder(
            IActivity<T, TResourceId, TWorkStreamId> activity,
            List<IInvalidConstraint<T>> output)
        {
            if (!activity.EarliestStartTime.HasValue || !activity.LatestStartTime.HasValue)
            {
                return;
            }

            if (activity.LatestStartTime < activity.EarliestStartTime)
            {
                output.Add(new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_LatestStartTimeLessThanEarliestStartTime));
            }
        }

        private static void CheckFinishTimeOrder(
            IActivity<T, TResourceId, TWorkStreamId> activity,
            List<IInvalidConstraint<T>> output)
        {
            if (!activity.EarliestFinishTime.HasValue || !activity.LatestFinishTime.HasValue)
            {
                return;
            }

            if (activity.LatestFinishTime < activity.EarliestFinishTime)
            {
                output.Add(new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_LatestFinishTimeLessThanEarliestFinishTime));
            }
        }

        private static void CheckEarliestStartConstraint(
            IActivity<T, TResourceId, TWorkStreamId> activity,
            List<IInvalidConstraint<T>> output)
        {
            if (!activity.EarliestStartTime.HasValue || !activity.MinimumEarliestStartTime.HasValue)
            {
                return;
            }

            if (activity.EarliestStartTime < activity.MinimumEarliestStartTime)
            {
                output.Add(new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_EarliestStartTimeLessThanMinimumEarliestStartTime));
            }
        }

        private static void CheckLatestFinishConstraint(
            IActivity<T, TResourceId, TWorkStreamId> activity,
            List<IInvalidConstraint<T>> output)
        {
            if (!activity.LatestFinishTime.HasValue || !activity.MaximumLatestFinishTime.HasValue)
            {
                return;
            }

            if (activity.LatestFinishTime > activity.MaximumLatestFinishTime)
            {
                output.Add(new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_LatestFinishTimeMoreThanMaximumLatestFinishTime));
            }
        }

        private static void CheckFreeSlackConstraint(
            IActivity<T, TResourceId, TWorkStreamId> activity,
            List<IInvalidConstraint<T>> output)
        {
            if (!activity.FreeSlack.HasValue || !activity.MinimumFreeSlack.HasValue)
            {
                return;
            }

            if (activity.FreeSlack < activity.MinimumFreeSlack)
            {
                output.Add(new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_FreeSlackLessThanMinimumFreeSlack));
            }
        }
    }
}
