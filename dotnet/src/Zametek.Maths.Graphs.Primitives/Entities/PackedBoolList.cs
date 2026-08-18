using System;
using System.Collections;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // A read-only list of booleans stored as individual bits.
    //
    // WHY THIS EXISTS
    //
    // A resource schedule keeps five per-time-unit allocation streams (resource, cost,
    // billing, effort and activity allocation), each holding one flag per time unit for
    // the whole length of the schedule. The obvious storage - List<bool> - is backed by
    // a bool[], and .NET stores every bool as a whole byte even though it carries only
    // one bit of information. Seven eighths of that memory is padding.
    //
    // That waste matters because these streams are the largest thing a compilation
    // retains, and they scale as (horizon x resources): five streams x one byte x
    // 100,000 time units x 1,000 resources is around 500 MB, and a compile holds two
    // full sets alive at once while it rebuilds the aligned schedules. Packing the
    // flags into bits divides all of that by eight.
    //
    // HOW THE PACKING WORDS
    //
    // The flags live in an array of 64-bit words, 64 flags per word, in order. To find
    // flag number i:
    //
    //   - which word holds it?  i / 64, written as i >> 6 (because 64 == 2^6)
    //   - which bit within it?  i % 64, written as i & 63 (the low 6 bits of i)
    //
    // Shifting and masking are used instead of division and remainder because they are
    // the same operation for powers of two and cost a single instruction each. Worked
    // example: flag 70 lives in word 1 (70 / 64 == 1) at bit 6 (70 % 64 == 6).
    //
    // Testing a flag builds a mask with a single 1 bit in the wanted position
    // (1UL << bit) and ANDs it against the word: a non-zero result means the flag is
    // set. Setting a flag ORs the same mask into the word.
    //
    // This is the same representation, and the same shift/mask idiom, used by
    // AncestorBitSets in the compilers package.
    //
    // WHAT THIS TYPE DELIBERATELY IS NOT
    //
    // It is not System.Collections.BitArray. BitArray implements only the non-generic
    // IEnumerable, so every foreach over it boxes each bool onto the heap - which would
    // trade the memory saving for an allocation on every read of every time unit. This
    // type implements IReadOnlyList<bool> and returns a struct enumerator, so iterating
    // it allocates nothing.
    /// <summary>
    /// An immutable list of booleans stored one bit per element, used for the
    /// per-time-unit allocation streams of a resource schedule.
    /// </summary>
    public sealed class PackedBoolList
        : IReadOnlyList<bool>
    {
        #region Constants

        // The number of flags stored in each word; a ulong holds 64 bits.
        private const int c_BitsPerWord = 64;

        // Dividing by 64 is the same as shifting right by 6, because 64 == 2^6.
        // Used to find which word holds a given flag.
        private const int c_WordIndexShift = 6;

        // Taking the remainder after dividing by 64 is the same as keeping the low
        // 6 bits, which is what ANDing with 63 does. Used to find which bit within
        // its word holds a given flag.
        private const int c_BitIndexMask = c_BitsPerWord - 1;

        #endregion

        #region Fields

        private readonly ulong[] m_Words;
        private readonly int m_Count;

        #endregion

        #region Ctors

        private PackedBoolList(ulong[] words, int count)
        {
            m_Words = words;
            m_Count = count;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The number of flags in the list.
        /// </summary>
        public int Count => m_Count;

        /// <summary>
        /// The flag at the given position.
        /// </summary>
        public bool this[int index]
        {
            get
            {
                if (index < 0 || index >= m_Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                // Find the word holding this flag, then test the single bit within it.
                return (m_Words[index >> c_WordIndexShift] & BitMaskOf(index)) != 0;
            }
        }

        #endregion

        #region Static Helpers

        // The number of 64-bit words needed to hold the given number of flags. This is
        // a ceiling division: 64 flags need 1 word, but 65 need 2, so the remainder has
        // to round the result up rather than truncate it.
        private static int WordCountFor(int count) => (count + c_BitsPerWord - 1) / c_BitsPerWord;

        // A word with a single 1 bit, in the position this flag occupies within its word.
        private static ulong BitMaskOf(int index) => 1UL << (index & c_BitIndexMask);

        /// <summary>
        /// Packs the given flags into a new list. The source is enumerated exactly once.
        /// </summary>
        public static PackedBoolList From(IEnumerable<bool> flags)
        {
            if (flags is null)
            {
                throw new ArgumentNullException(nameof(flags));
            }

            // When the length is known up front the word array can be sized exactly;
            // otherwise it grows by doubling, like List<T> does.
            int capacity = flags is IReadOnlyCollection<bool> knownSize ? knownSize.Count : 0;
            ulong[] words = new ulong[Math.Max(WordCountFor(capacity), 1)];
            int count = 0;

            foreach (bool flag in flags)
            {
                int wordIndex = count >> c_WordIndexShift;
                if (wordIndex >= words.Length)
                {
                    Array.Resize(ref words, words.Length * 2);
                }
                if (flag)
                {
                    // Only set bits need writing; the array starts out all zeroes,
                    // which already means "every flag false".
                    words[wordIndex] |= BitMaskOf(count);
                }
                count++;
            }

            return new PackedBoolList(words, count);
        }

        #endregion

        #region IEnumerable<bool> Members

        /// <summary>
        /// Returns a struct enumerator, so iterating this list allocates nothing.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<bool> IEnumerable<bool>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region Private Types

        /// <summary>
        /// Allocation-free enumerator over a <see cref="PackedBoolList"/>.
        /// </summary>
        public struct Enumerator
            : IEnumerator<bool>
        {
            private readonly PackedBoolList m_List;
            private int m_Index;

            internal Enumerator(PackedBoolList list)
            {
                m_List = list;
                m_Index = -1;
                Current = false;
            }

            /// <inheritdoc/>
            public bool Current { get; private set; }

            object IEnumerator.Current => Current;

            /// <inheritdoc/>
            public bool MoveNext()
            {
                int next = m_Index + 1;
                if (next >= m_List.m_Count)
                {
                    return false;
                }
                m_Index = next;
                Current = m_List[next];
                return true;
            }

            /// <inheritdoc/>
            public void Reset()
            {
                m_Index = -1;
                Current = false;
            }

            /// <inheritdoc/>
            public void Dispose()
            {
            }
        }

        #endregion
    }
}
