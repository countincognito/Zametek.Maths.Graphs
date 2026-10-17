using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Reads the critical activities and the critical paths of a schedule that
    // SimulationIterationRunner has just made.
    //
    // A critical path is a chain of activities that sets the finish time. Each link of the chain
    // is a tight link: the predecessor is critical, and it finishes at the moment that the
    // activity starts. A predecessor is an activity that the plan names (a dependency or a
    // planning dependency), or an activity that the resource allocation put before it. The chain
    // starts at an activity with no tight link and it ends at a critical activity that finishes
    // at the finish time.
    //
    // The tracer walks back from the end, and so it meets no dead end: each critical activity that
    // starts after zero has a tight predecessor that is critical, because it has no float. The
    // order of the walk is fixed (the activities and their predecessors are taken in the order of
    // the plan, and not in the order in which a set happens to enumerate them), so that the Rust
    // port finds the same paths in the same order.
    //
    // A path is held as a list of numbers, (activity number << 2) | link, with the activities that
    // take time only. See CriticalPath for what the link of a step means.
    internal sealed class CriticalPathTracer<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        // The walk is limited to this many complete walks for each path that it may record, so
        // that a plan with very many tied paths that all look alike (they differ only in
        // milestones) cannot make an iteration take for ever.
        private const int c_WalkLimitPerPath = 16;

        private readonly IReadOnlyList<IDependentActivity<T, TResourceId, TWorkStreamId>> m_Activities;
        private readonly Dictionary<T, int> m_IndexById;
        private readonly bool[] m_Critical;
        private readonly int[] m_Start;
        private readonly int[] m_Finish;

        // The state of the walk: one frame for each activity of the chain that is being built,
        // from the end of the chain (frame 0) to its latest start.
        private int[] m_FrameNode = new int[16];
        private int[][] m_FramePredecessors = new int[16][];
        private int[] m_FrameNext = new int[16];
        private bool[] m_FrameResource = new bool[16];

        internal CriticalPathTracer(
            IReadOnlyList<IDependentActivity<T, TResourceId, TWorkStreamId>> activities,
            Dictionary<T, int> indexById)
        {
            m_Activities = activities;
            m_IndexById = indexById;
            m_Critical = new bool[activities.Count];
            m_Start = new int[activities.Count];
            m_Finish = new int[activities.Count];
        }

        // Sets the bit of each activity that is critical. The bits of an iteration are the words
        // that start at the offset.
        internal void FillCriticalBits(ulong[] bits, int offset)
        {
            for (int i = 0; i < m_Activities.Count; i++)
            {
                if (m_Activities[i].IsCritical)
                {
                    bits[offset + (i >> 6)] |= 1UL << (i & 63);
                }
            }
        }

        // Finds the critical paths, and adds each one that is new to the list. Returns whether the
        // list is complete: it is not when there are more paths than the maximum, or when the walk
        // reached its limit.
        internal bool Trace(int finishTime, int[] durations, int maximumPaths, List<int[]> paths)
        {
            for (int i = 0; i < m_Activities.Count; i++)
            {
                IDependentActivity<T, TResourceId, TWorkStreamId> activity = m_Activities[i];
                m_Critical[i] = activity.IsCritical;
                m_Start[i] = activity.EarliestStartTime.GetValueOrDefault();
                m_Finish[i] = activity.EarliestFinishTime.GetValueOrDefault();
            }

            long walkBudget = (long)maximumPaths * c_WalkLimitPerPath;
            for (int sink = 0; sink < m_Activities.Count; sink++)
            {
                if (!m_Critical[sink] || m_Finish[sink] != finishTime)
                {
                    continue;
                }

                int depth = 0;
                Push(depth, sink);
                while (depth >= 0)
                {
                    int[] predecessors = m_FramePredecessors[depth];
                    if (predecessors.Length == 0)
                    {
                        // The start of a chain: the chain is complete.
                        if (--walkBudget < 0)
                        {
                            return false;
                        }
                        int[] path = Encode(depth, durations);
                        if (path.Length > 0 && !Contains(paths, path))
                        {
                            if (paths.Count >= maximumPaths)
                            {
                                return false;
                            }
                            paths.Add(path);
                        }
                        depth--;
                    }
                    else if (m_FrameNext[depth] >= predecessors.Length)
                    {
                        depth--;
                    }
                    else
                    {
                        int next = predecessors[m_FrameNext[depth]++];
                        m_FrameResource[depth] = (next & 1) != 0;
                        depth++;
                        Push(depth, next >> 1);
                    }
                }
            }
            return true;
        }

        private void Push(int depth, int node)
        {
            if (depth == m_FrameNode.Length)
            {
                Array.Resize(ref m_FrameNode, depth * 2);
                Array.Resize(ref m_FramePredecessors, depth * 2);
                Array.Resize(ref m_FrameNext, depth * 2);
                Array.Resize(ref m_FrameResource, depth * 2);
            }
            m_FrameNode[depth] = node;
            m_FramePredecessors[depth] = TightPredecessors(node);
            m_FrameNext[depth] = 0;
            m_FrameResource[depth] = false;
        }

        // The tight predecessors of a critical activity, as (predecessor number << 1) | 1 for a
        // link that exists only because of a resource, and as predecessor number << 1 for a link of
        // the plan. They are sorted by the number of the predecessor.
        private int[] TightPredecessors(int node)
        {
            IDependentActivity<T, TResourceId, TWorkStreamId> activity = m_Activities[node];
            var found = new List<int>(4);

            foreach (T id in activity.Dependencies)
            {
                Consider(node, id, false, found);
            }
            foreach (T id in activity.PlanningDependencies)
            {
                if (!activity.Dependencies.Contains(id))
                {
                    Consider(node, id, false, found);
                }
            }
            foreach (T id in activity.ResourceDependencies)
            {
                if (!activity.Dependencies.Contains(id) && !activity.PlanningDependencies.Contains(id))
                {
                    Consider(node, id, true, found);
                }
            }

            found.Sort();
            return found.ToArray();
        }

        private void Consider(int node, T predecessorId, bool resource, List<int> found)
        {
            if (m_IndexById.TryGetValue(predecessorId, out int predecessor)
                && m_Critical[predecessor]
                && m_Finish[predecessor] == m_Start[node])
            {
                found.Add((predecessor << 1) | (resource ? 1 : 0));
            }
        }

        // The chain in frames 0 to depth, as a path in the order of the schedule: from the frame
        // at the depth (the start of the chain) back to frame 0 (its end).
        private int[] Encode(int depth, int[] durations)
        {
            var steps = new List<int>(depth + 1);
            bool sawResource = false;
            bool first = true;
            for (int k = depth; k >= 0; k--)
            {
                int node = m_FrameNode[k];
                if (k < depth && m_FrameResource[k])
                {
                    sawResource = true;
                }
                if (durations[node] == 0)
                {
                    continue;
                }

                CriticalPathLink link = first
                    ? CriticalPathLink.None
                    : (sawResource ? CriticalPathLink.Resource : CriticalPathLink.Dependency);
                steps.Add((node << 2) | (int)link);
                first = false;
                sawResource = false;
            }
            return steps.ToArray();
        }

        private static bool Contains(List<int[]> paths, int[] path)
        {
            foreach (int[] existing in paths)
            {
                if (existing.AsSpan().SequenceEqual(path))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
