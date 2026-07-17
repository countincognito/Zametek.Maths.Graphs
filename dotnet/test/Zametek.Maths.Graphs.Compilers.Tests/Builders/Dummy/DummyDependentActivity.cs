using System.Collections.Generic;

namespace Zametek.Maths.Graphs.Tests
{
    public class DummyDependentActivity
        : DependentActivity<int, int, int>
    {
        public DummyDependentActivity(int id, string name, string notes, IEnumerable<int> targetWorkStreams, IEnumerable<int> targetResources, IEnumerable<int> dependencies, IEnumerable<int> planningDependencies, IEnumerable<int> resourceDependencies, IEnumerable<int> successors, LogicalOperator targetLogicalOperator, IEnumerable<int> allocatedToResources, bool canBeRemoved, bool hasNoCost, bool hasNoBilling, bool hasNoEffort, int duration, int? freeSlack, int? earliestStartTime, int? latestFinishTime, int? minimumFreeSlack, int? minimumEarliestStartTime, int? maximumLatestFinishTime)
            : base(id, name, notes, targetWorkStreams, targetResources, dependencies, planningDependencies, resourceDependencies, successors, targetLogicalOperator, allocatedToResources, canBeRemoved, hasNoCost, hasNoBilling, hasNoEffort, duration, freeSlack, earliestStartTime, latestFinishTime, minimumFreeSlack, minimumEarliestStartTime, maximumLatestFinishTime)
        {
        }
    }
}
