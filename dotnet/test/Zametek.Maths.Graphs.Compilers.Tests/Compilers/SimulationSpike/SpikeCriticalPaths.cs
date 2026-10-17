using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Zametek.Maths.Graphs.Tests
{
    // The critical-path calculation of the reference tool, on arrays, and the way it names
    // a critical path. It has no resources and no clever data structures, so that the
    // numbers it gives can be compared with the published numbers of the reference tool.
    //
    // The reference tool works in numbers with two decimals, and it calls an activity
    // critical when its total float is exactly zero. This class does the same with whole
    // numbers of hundredths, which keeps every sum exact and makes ties between paths
    // happen as often as they do in the reference tool.
    internal sealed class SpikeCriticalPaths
    {
        private readonly int[][] m_Predecessors;
        private readonly int[][] m_Successors;
        private readonly int[] m_Order;
        private readonly int[] m_EarliestFinish;
        private readonly int[] m_LatestFinish;

        internal SpikeCriticalPaths(int[][] predecessors)
        {
            int count = predecessors.Length;
            m_Predecessors = predecessors;
            var successors = new List<int>[count];
            for (int i = 0; i < count; i++)
            {
                successors[i] = new List<int>();
            }
            for (int i = 0; i < count; i++)
            {
                foreach (int p in predecessors[i])
                {
                    successors[p].Add(i);
                }
            }
            m_Successors = successors.Select(x => x.ToArray()).ToArray();

            // Kahn's algorithm.
            var inDegree = predecessors.Select(x => x.Length).ToArray();
            var order = new List<int>(count);
            var queue = new Queue<int>();
            for (int i = 0; i < count; i++)
            {
                if (inDegree[i] == 0)
                {
                    queue.Enqueue(i);
                }
            }
            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                order.Add(node);
                foreach (int s in m_Successors[node])
                {
                    if (--inDegree[s] == 0)
                    {
                        queue.Enqueue(s);
                    }
                }
            }
            if (order.Count != count)
            {
                throw new InvalidOperationException("The plan has a cycle.");
            }
            m_Order = order.ToArray();
            m_EarliestFinish = new int[count];
            m_LatestFinish = new int[count];
        }

        internal int Count => m_Predecessors.Length;

        // Fills critical with true for each activity whose total float is zero, and returns
        // the finish time of the project. Not safe for use by two threads at once.
        internal int Calculate(int[] durations, bool[] critical)
        {
            int finish = 0;
            foreach (int i in m_Order)
            {
                int start = 0;
                foreach (int p in m_Predecessors[i])
                {
                    if (m_EarliestFinish[p] > start)
                    {
                        start = m_EarliestFinish[p];
                    }
                }
                m_EarliestFinish[i] = start + durations[i];
                if (m_EarliestFinish[i] > finish)
                {
                    finish = m_EarliestFinish[i];
                }
            }
            for (int k = m_Order.Length - 1; k >= 0; k--)
            {
                int i = m_Order[k];
                int latest = finish;
                foreach (int s in m_Successors[i])
                {
                    int latestStart = m_LatestFinish[s] - durations[s];
                    if (latestStart < latest)
                    {
                        latest = latestStart;
                    }
                }
                m_LatestFinish[i] = latest;
                critical[i] = latest == m_EarliestFinish[i];
            }
            return finish;
        }

        // Names the critical path in the way of the reference tool. When exactly one path
        // runs through critical activities only, from an activity with no critical
        // predecessor to one with no critical successor, the name lists the activities of
        // that path that have a duration. When there are several, the name is "Multiple".
        // A plan with no critical path has the empty name.
        internal static string Label(int[][] predecessors, bool[] critical, string[] names, int[] durations)
        {
            int count = predecessors.Length;
            var hasCriticalSuccessor = new bool[count];
            for (int i = 0; i < count; i++)
            {
                if (!critical[i])
                {
                    continue;
                }
                foreach (int p in predecessors[i])
                {
                    if (critical[p])
                    {
                        hasCriticalSuccessor[p] = true;
                    }
                }
            }

            // The number of paths that end at a node, kept at two because the label only
            // needs to know whether there is none, one or many. The order of the ids in the
            // plan is not a topological order in general, so this visits by recursion with
            // a memo.
            var paths = new int[count];
            var visited = new bool[count];
            int PathsTo(int node)
            {
                if (visited[node])
                {
                    return paths[node];
                }
                int sum = 0;
                bool anyCriticalPredecessor = false;
                foreach (int p in predecessors[node])
                {
                    if (critical[p])
                    {
                        anyCriticalPredecessor = true;
                        sum = Math.Min(2, sum + PathsTo(p));
                    }
                }
                visited[node] = true;
                paths[node] = anyCriticalPredecessor ? sum : 1;
                return paths[node];
            }

            int total = 0;
            int sink = -1;
            for (int i = 0; i < count; i++)
            {
                if (critical[i] && !hasCriticalSuccessor[i])
                {
                    total = Math.Min(2, total + PathsTo(i));
                    sink = i;
                }
            }

            if (total == 0)
            {
                return string.Empty;
            }
            if (total > 1)
            {
                return "Multiple";
            }

            var chain = new List<int>();
            int current = sink;
            while (true)
            {
                chain.Add(current);
                int next = -1;
                foreach (int p in predecessors[current])
                {
                    if (critical[p])
                    {
                        next = p;
                        break;
                    }
                }
                if (next < 0)
                {
                    break;
                }
                current = next;
            }
            chain.Reverse();

            var builder = new StringBuilder();
            foreach (int node in chain)
            {
                if (durations[node] == 0)
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append('>');
                }
                builder.Append(names[node]);
            }
            return builder.ToString();
        }
    }
}
