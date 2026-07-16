using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Compact ancestor representation used by the transitive reducers.
    //
    // The problem this solves: transitive reduction needs, for every node, the set of
    // all the nodes that come before it (its "ancestors"). Storing those sets as
    // HashSet<T> costs tens of bytes per entry, and in a graph of M nodes each node can
    // have up to M ancestors - so deep graphs ran out of memory long before the
    // algorithms became slow.
    //
    // The fix is to store each ancestor set as a *bitset* instead: one single bit per
    // potential ancestor.
    //
    // Dense indexes
    // -------------
    // Node IDs can be any value type (ints with gaps, negative numbers, and so on), so
    // an ID cannot be used directly as a position in an array. Instead, every node
    // reachable from an end node is given a "dense index": the first node discovered
    // gets index 0, the next gets 1, and so on, with no gaps. Two lookups tie the
    // worlds together - m_IndexLookup maps a node ID to its dense index, and m_Ids maps
    // a dense index back to its node ID. The bitsets only ever speak in dense indexes:
    // bit 5 always means "the node whose dense index is 5".
    //
    // Bitsets
    // -------
    // A bitset here is an array of 64-bit unsigned integers (ulong "words"). Bit
    // positions run through word 0 first (bits 0-63), then word 1 (bits 64-127), and
    // so on. For example:
    //
    //   dense index 70  ->  word 1 (because 70 / 64 = 1), bit 6 (because 70 % 64 = 6)
    //
    // With that layout, set operations become simple integer arithmetic:
    //   - "is node X in the set?"  -> test a single bit;
    //   - "merge set B into set A" -> bitwise-OR each word of B into A, because a given
    //     bit position means the same node in every bitset of the same graph.
    //
    // For M nodes the whole structure costs about M * M / 8 bytes (M bitsets of M bits
    // each), versus M * M HashSet entries at tens of bytes each - roughly two hundred
    // times smaller, which is the difference between a 20k-deep graph reducing
    // comfortably and exhausting memory.
    //
    // The dictionary-of-hashsets form remains the public contract
    // (GetAncestorNodesLookup and IDummyEdgeOrchestrator); ToDictionary() materialises
    // it on demand from the bits.
    internal sealed class AncestorBitSets<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Constants

        // A ulong word holds 64 bits, so each word tracks membership for 64 nodes.
        private const int c_BitsPerWord = 64;

        // Shifting an index right by 6 places is the same as dividing it by 64
        // (because 64 = 2 to the power 6). It converts a dense node index into the
        // position of the word that holds its bit.
        private const int c_WordIndexShift = 6;

        // Masking an index with 63 (binary 111111) keeps only the low 6 bits, which is
        // the same as taking the remainder of dividing by 64. It converts a dense node
        // index into the bit position within its word.
        private const int c_BitIndexMask = c_BitsPerWord - 1;

        #endregion

        #region Fields

        // Node ID -> dense index (which bit position represents that node).
        private readonly Dictionary<T, int> m_IndexLookup;

        // Dense index -> node ID (the reverse of m_IndexLookup).
        private readonly T[] m_Ids;

        // One bitset per node, indexed by the node's dense index: m_AncestorWords[i]
        // is the ancestor set of the node whose dense index is i.
        private readonly ulong[][] m_AncestorWords;

        #endregion

        #region Ctor

        internal AncestorBitSets(
            Dictionary<T, int> indexLookup,
            T[] ids,
            ulong[][] ancestorWords,
            int wordCount)
        {
            m_IndexLookup = indexLookup ?? throw new ArgumentNullException(nameof(indexLookup));
            m_Ids = ids ?? throw new ArgumentNullException(nameof(ids));
            m_AncestorWords = ancestorWords ?? throw new ArgumentNullException(nameof(ancestorWords));
            WordCount = wordCount;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The number of ulong words in each bitset (and in scratch bitsets).
        /// </summary>
        internal int WordCount { get; }

        #endregion

        #region Static Helpers

        // All of the bit arithmetic lives in this class; callers (including the
        // calculator that builds the raw word arrays) go through these helpers.

        /// <summary>
        /// The number of words needed to give each of the given nodes its own bit.
        /// </summary>
        internal static int WordCountFor(int nodeCount)
        {
            // This is nodeCount / 64 rounded *up*, so the last few nodes still get a
            // bit: 64 nodes fit in 1 word, but 65 nodes need 2.
            return (nodeCount + c_BitsPerWord - 1) / c_BitsPerWord;
        }

        /// <summary>
        /// Switches on the bit for the given dense node index in a raw word array.
        /// </summary>
        internal static void SetBit(ulong[] words, int denseIndex)
        {
            // OR-ing a word that has only one bit set switches that bit on while
            // leaving every other bit as it was.
            words[WordIndexOf(denseIndex)] |= BitMaskOf(denseIndex);
        }

        /// <summary>
        /// Clears a scratch bitset for reuse.
        /// </summary>
        internal static void ClearScratch(ulong[] scratch) => Array.Clear(scratch, 0, scratch.Length);

        // The position of the word that holds this dense index's bit (index / 64).
        private static int WordIndexOf(int denseIndex) => denseIndex >> c_WordIndexShift;

        // A word with only this dense index's bit switched on (bit position index % 64).
        private static ulong BitMaskOf(int denseIndex) => 1UL << (denseIndex & c_BitIndexMask);

        #endregion

        #region Internal Methods

        /// <summary>
        /// Creates a scratch bitset for accumulating unions of ancestor sets.
        /// </summary>
        internal ulong[] CreateScratch() => new ulong[WordCount];

        // Throws KeyNotFoundException for an unknown node id - parity with indexing
        // into the dictionary form.
        /// <summary>
        /// Unions the ancestor set of the given node into the scratch bitset.
        /// </summary>
        internal void UnionAncestorsInto(ulong[] scratch, T nodeId)
        {
            ulong[] words = m_AncestorWords[m_IndexLookup[nodeId]];
            // OR-ing every word of the node's ancestor set into the scratch bitset
            // merges the two sets: a bit ends up on in scratch if it was on in either.
            for (int i = 0; i < words.Length; i++)
            {
                scratch[i] |= words[i];
            }
        }

        // An unknown node id is simply not a member - parity with HashSet.Contains.
        /// <summary>
        /// Whether the given node id is present in the scratch bitset.
        /// </summary>
        internal bool ScratchContains(ulong[] scratch, T nodeId)
        {
            if (!m_IndexLookup.TryGetValue(nodeId, out int index))
            {
                return false;
            }
            // Pick out the single word that holds this node's bit and test that bit:
            // AND-ing with the node's one-bit mask leaves zero unless the bit is on.
            return (scratch[WordIndexOf(index)] & BitMaskOf(index)) != 0;
        }

        /// <summary>
        /// Materialises the dictionary-of-hashsets form (the public lookup contract).
        /// </summary>
        internal Dictionary<T, HashSet<T>> ToDictionary()
        {
            var output = new Dictionary<T, HashSet<T>>(m_Ids.Length);
            for (int nodeIndex = 0; nodeIndex < m_Ids.Length; nodeIndex++)
            {
                var ancestorSet = new HashSet<T>();
                ulong[] words = m_AncestorWords[nodeIndex];
                for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
                {
                    // Walk the bits of this word from lowest to highest: test the
                    // lowest bit, then shift the word right by one so the next bit
                    // becomes the lowest. The loop stops as soon as no set bits
                    // remain, so runs of empty high bits cost nothing.
                    ulong word = words[wordIndex];
                    // The dense index that this word's lowest bit represents.
                    int denseIndex = wordIndex * c_BitsPerWord;
                    while (word != 0)
                    {
                        if ((word & 1UL) != 0)
                        {
                            // The bit is on, so the node with this dense index is an
                            // ancestor - translate the index back into a node ID.
                            ancestorSet.Add(m_Ids[denseIndex]);
                        }
                        word >>= 1;
                        denseIndex++;
                    }
                }
                output.Add(m_Ids[nodeIndex], ancestorSet);
            }
            return output;
        }

        #endregion
    }
}
