using System.Collections.Generic;

namespace Zametek.Maths.Graphs.Tests
{
    public class DummyDependentActivity
        : DependentActivity<int, int, int>
    {
        public DummyDependentActivity(int id, string name, string notes, IEnumerable<int> targetWorkStreams, IEnumerable<int> targetResources, IEnumerable<int> dependencies, IEnumerable<int> resourceDependencies, LogicalOperator targetLogicalOperator, IEnumerable<int> allocatedToResources, bool canBeRemoved, bool hasNoCost, int duration, int? freeSlack, int? earliestStartTime, int? latestFinishTime, int? minimumFreeSlack, int? minimumEarliestStartTime, int? maximumLatestFinishTime)
            : base(id, name, notes, targetWorkStreams, targetResources, dependencies, resourceDependencies, targetLogicalOperator, allocatedToResources, canBeRemoved, hasNoCost, duration, freeSlack, earliestStartTime, latestFinishTime, minimumFreeSlack, minimumEarliestStartTime, maximumLatestFinishTime)
        {
        }
    }
}
