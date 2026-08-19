//! A read-only list of booleans stored as individual bits - the counterpart of
//! the C# `PackedBoolList`.
//!
//! # Why this exists
//!
//! A resource schedule keeps five per-time-unit allocation streams (resource,
//! cost, billing, effort and activity allocation), each holding one flag per
//! time unit for the whole length of the schedule. The obvious storage -
//! `Vec<bool>` - stores every flag as a whole byte even though it carries only
//! one bit of information. Seven eighths of that memory is padding.
//!
//! That waste matters because these streams are the largest thing a compilation
//! retains, and they scale as (horizon x resources): five streams x one byte x
//! 100,000 time units x 1,000 resources is around 500 MB, and a compile holds
//! two full sets alive at once while it rebuilds the aligned schedules. Packing
//! the flags into bits divides all of that by eight.
//!
//! # How the packing works
//!
//! The flags live in a vector of 64-bit words, 64 flags per word, in order. To
//! find flag number `i`:
//!
//! - which word holds it? `i / 64`, written as `i >> 6` (because `64 == 2^6`)
//! - which bit within it? `i % 64`, written as `i & 63` (the low 6 bits of `i`)
//!
//! Shifting and masking are used instead of division and remainder because they
//! are the same operation for powers of two and cost a single instruction each.
//! Worked example: flag 70 lives in word 1 (`70 / 64 == 1`) at bit 6
//! (`70 % 64 == 6`).
//!
//! Testing a flag builds a mask with a single 1 bit in the wanted position
//! (`1 << bit`) and ANDs it against the word: a non-zero result means the flag
//! is set. Setting a flag ORs the same mask into the word.
//!
//! This is the same representation, and the same shift/mask idiom, used by the
//! ancestor bit sets in the compilers crate.

use std::fmt;

/// The number of flags stored in each word; a `u64` holds 64 bits.
const BITS_PER_WORD: usize = 64;

/// Dividing by 64 is the same as shifting right by 6, because `64 == 2^6`.
/// Used to find which word holds a given flag.
const WORD_INDEX_SHIFT: usize = 6;

/// Taking the remainder after dividing by 64 is the same as keeping the low 6
/// bits, which is what ANDing with 63 does. Used to find which bit within its
/// word holds a given flag.
const BIT_INDEX_MASK: usize = BITS_PER_WORD - 1;

/// The number of 64-bit words needed to hold the given number of flags. This is
/// a ceiling division: 64 flags need 1 word, but 65 need 2, so the remainder has
/// to round the result up rather than truncate it.
const fn word_count_for(count: usize) -> usize {
    count.div_ceil(BITS_PER_WORD)
}

/// A word with a single 1 bit, in the position this flag occupies within its word.
const fn bit_mask_of(index: usize) -> u64 {
    1u64 << (index & BIT_INDEX_MASK)
}

/// An immutable list of booleans stored one bit per element, used for the
/// per-time-unit allocation streams of a resource schedule.
///
/// Build one by collecting an iterator of `bool`, or from a `Vec<bool>`:
///
/// ```
/// use zametek_maths_graphs_primitives::PackedBoolList;
///
/// let flags: PackedBoolList = [true, false, true].into_iter().collect();
/// assert_eq!(flags.len(), 3);
/// assert_eq!(flags.get(0), Some(true));
/// assert_eq!(flags, vec![true, false, true]);
/// ```
#[derive(Clone, Default, PartialEq, Eq, Hash)]
pub struct PackedBoolList {
    /// Exactly `word_count_for(count)` words, so that two lists holding the same
    /// flags always compare equal word for word.
    words: Vec<u64>,
    count: usize,
}

impl PackedBoolList {
    /// Creates an empty list.
    pub fn new() -> Self {
        Self {
            words: Vec::new(),
            count: 0,
        }
    }

    /// The number of flags in the list.
    pub fn len(&self) -> usize {
        self.count
    }

    /// Whether the list holds no flags.
    pub fn is_empty(&self) -> bool {
        self.count == 0
    }

    /// The flag at the given position, or `None` if the position is past the end.
    ///
    /// There is no `Index` implementation: indexing must hand back a reference,
    /// and a bit inside a word has no address of its own to borrow.
    pub fn get(&self, index: usize) -> Option<bool> {
        if index >= self.count {
            return None;
        }
        // Find the word holding this flag, then test the single bit within it.
        Some(self.words[index >> WORD_INDEX_SHIFT] & bit_mask_of(index) != 0)
    }

    /// Iterates the flags in order.
    pub fn iter(&self) -> Iter<'_> {
        Iter {
            list: self,
            index: 0,
        }
    }
}

/// Iterator over the flags of a [`PackedBoolList`], yielding `bool` by value
/// (there is nothing to borrow - each flag is a bit inside a word).
pub struct Iter<'a> {
    list: &'a PackedBoolList,
    index: usize,
}

impl Iterator for Iter<'_> {
    type Item = bool;

    fn next(&mut self) -> Option<bool> {
        let flag = self.list.get(self.index)?;
        self.index += 1;
        Some(flag)
    }

    fn size_hint(&self) -> (usize, Option<usize>) {
        let remaining = self.list.count - self.index;
        (remaining, Some(remaining))
    }
}

impl ExactSizeIterator for Iter<'_> {}

impl<'a> IntoIterator for &'a PackedBoolList {
    type Item = bool;
    type IntoIter = Iter<'a>;

    fn into_iter(self) -> Iter<'a> {
        self.iter()
    }
}

impl FromIterator<bool> for PackedBoolList {
    fn from_iter<I: IntoIterator<Item = bool>>(flags: I) -> Self {
        let flags = flags.into_iter();
        let (lower_bound, _) = flags.size_hint();
        let mut words: Vec<u64> = Vec::with_capacity(word_count_for(lower_bound));
        let mut count = 0usize;

        for flag in flags {
            let word_index = count >> WORD_INDEX_SHIFT;
            if word_index == words.len() {
                words.push(0);
            }
            if flag {
                // Only set bits need writing; a new word starts out all zeroes,
                // which already means "every flag false".
                words[word_index] |= bit_mask_of(count);
            }
            count += 1;
        }

        Self { words, count }
    }
}

impl From<Vec<bool>> for PackedBoolList {
    fn from(flags: Vec<bool>) -> Self {
        flags.into_iter().collect()
    }
}

impl From<&[bool]> for PackedBoolList {
    fn from(flags: &[bool]) -> Self {
        flags.iter().copied().collect()
    }
}

impl From<&PackedBoolList> for Vec<bool> {
    fn from(flags: &PackedBoolList) -> Self {
        flags.iter().collect()
    }
}

/// Renders as a list of booleans rather than as its packed words, so that a
/// failing assertion reads like the flags it compares.
impl fmt::Debug for PackedBoolList {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_list().entries(self.iter()).finish()
    }
}

// Comparison against plain boolean sequences, so that callers (and tests) can
// check a packed stream against a `vec![true, false, ...]` literal directly.
impl PartialEq<[bool]> for PackedBoolList {
    fn eq(&self, other: &[bool]) -> bool {
        self.count == other.len() && self.iter().zip(other.iter()).all(|(a, b)| a == *b)
    }
}

impl PartialEq<Vec<bool>> for PackedBoolList {
    fn eq(&self, other: &Vec<bool>) -> bool {
        self == other.as_slice()
    }
}

impl PartialEq<PackedBoolList> for Vec<bool> {
    fn eq(&self, other: &PackedBoolList) -> bool {
        other == self.as_slice()
    }
}

impl PartialEq<PackedBoolList> for [bool] {
    fn eq(&self, other: &PackedBoolList) -> bool {
        other == self
    }
}
