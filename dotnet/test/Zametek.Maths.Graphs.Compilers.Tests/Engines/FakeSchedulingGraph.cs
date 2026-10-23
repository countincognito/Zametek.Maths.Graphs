using System.Collections.Generic;

namespace Zametek.Maths.Graphs.Tests
{
    // Test double for the read-only view of a graph that the resource scheduler works on. It holds the
    // activities that a test gives it, and it answers from them, so that a test supplies just what it needs.
    // A test that needs the graph to do something more, when it is asked, derives from this class.
    internal class FakeSchedulingGraph
        : IResourceSchedulingGraph<int, int, int>
    {
        private readonly Dictionary<int, IActivity<int, int, int>> m_ActivitiesById = [];
        private readonly List<IActivity<int, int, int>> m_Activities;
        private readonly Dictionary<int, List<int>> m_StrongDependencies = [];

        public FakeSchedulingGraph(params IActivity<int, int, int>[] activities)
        {
            m_Activities = [.. activities];
            foreach (IActivity<int, int, int> activity in activities)
            {
                m_ActivitiesById.Add(activity.Id, activity);
            }
        }

        // Says that the activity has these strong dependencies. A dependency need not be an activity of the graph.
        public FakeSchedulingGraph WithStrongDependencies(int activityId, params int[] dependencyIds)
        {
            m_StrongDependencies[activityId] = [.. dependencyIds];
            return this;
        }

        public virtual IActivity<int, int, int> Activity(int id) =>
            m_ActivitiesById.TryGetValue(id, out IActivity<int, int, int> activity)
                ? activity
                : throw new KeyNotFoundException($@"The graph has no activity {id}.");

        public List<int> StrongActivityDependencyIds(int id) =>
            m_StrongDependencies.TryGetValue(id, out List<int> dependencyIds) ? [.. dependencyIds] : [];

        public List<IActivity<int, int, int>> CloneActivities() => [.. m_Activities];
    }
}
