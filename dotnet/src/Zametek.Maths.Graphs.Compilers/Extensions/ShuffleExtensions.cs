using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Puts a list in a random order. The builders use it when ShuffleProcessingOrder is on, which the
    // tests use to show that a result does not depend on the order in which the graph is walked.
    internal static class ShuffleExtensions
    {
        // One generator for all the lists. It is shared by many threads, and a Random is not safe for
        // that, so every shuffle holds the lock. A shuffle is a testing aid, and the lock costs nothing there.
        private static readonly Random s_Random = new Random();
        private static readonly object s_Lock = new object();

        // The shuffle of Fisher and Yates: every order of the list is as likely as every other.
        internal static void Shuffle<T>(this IList<T> items)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            lock (s_Lock)
            {
                for (int i = items.Count - 1; i > 0; i--)
                {
                    int j = s_Random.Next(i + 1);
                    (items[i], items[j]) = (items[j], items[i]);
                }
            }
        }
    }
}
