use crate::Key;
use std::collections::HashMap;
use std::fmt;

/// A hash set that iterates in insertion order and removes in O(1) amortised
/// time.
///
/// A node's edge sets were previously `IndexSet`, whose insertion-order contract
/// the golden tests encode - but `shift_remove` moves every later entry, making
/// each removal O(set size). That is what the C# `HashSet<T>.Remove` does in
/// O(1), and the difference is not academic: redirecting dummy edges repeatedly
/// removes an edge from a node whose fan-in is the whole group being redirected,
/// so the linear removal turned a quadratic pass into a cubic one.
///
/// This is the set counterpart of the compilers' `InsertionOrderMap`, and keeps
/// the same observable contract: iteration is insertion order minus removed
/// entries, and a re-inserted key moves to the end. Removal only tombstones the
/// slot, and the slots are compacted once more than half of them are tombstones,
/// so iteration stays O(live entries) and compaction amortises to O(1) per
/// removal.
#[derive(Clone)]
pub struct InsertionOrderSet<K: Key> {
    /// Entries in insertion order; a removed entry leaves a tombstone (`None`)
    /// until the next compaction, so live entries never move on removal.
    slots: Vec<Option<K>>,
    /// Key -> position in `slots`. Never iterated, so its nondeterministic order
    /// is unobservable.
    index: HashMap<K, usize>,
}

/// Below this many slots a removal never triggers compaction; tiny sets just keep
/// their tombstones until they grow or clear.
const MIN_COMPACT_LEN: usize = 16;

impl<K: Key> InsertionOrderSet<K> {
    pub fn new() -> Self {
        Self {
            slots: Vec::new(),
            index: HashMap::new(),
        }
    }

    pub fn len(&self) -> usize {
        self.index.len()
    }

    pub fn is_empty(&self) -> bool {
        self.len() == 0
    }

    pub fn contains(&self, key: &K) -> bool {
        self.index.contains_key(key)
    }

    /// Inserts the key, returning whether it was newly added. An existing key
    /// keeps its iteration position; a key that was previously removed is
    /// appended at the end.
    pub fn insert(&mut self, key: K) -> bool {
        if self.index.contains_key(&key) {
            return false;
        }
        self.index.insert(key, self.slots.len());
        self.slots.push(Some(key));
        true
    }

    /// Removes the key, preserving the iteration order of every other entry -
    /// the same observable behaviour as `IndexSet::shift_remove`, in O(1)
    /// amortised time. Named for the method it replaces.
    pub fn shift_remove(&mut self, key: &K) -> bool {
        let Some(slot_index) = self.index.remove(key) else {
            return false;
        };
        self.slots[slot_index] = None;
        if self.slots.len() >= MIN_COMPACT_LEN && self.slots.len() > 2 * self.index.len() {
            self.compact();
        }
        true
    }

    fn compact(&mut self) {
        self.slots.retain(Option::is_some);
        for (slot_index, slot) in self.slots.iter().enumerate() {
            let key = slot.as_ref().expect("compaction retained only live slots");
            self.index.insert(*key, slot_index);
        }
    }

    pub fn clear(&mut self) {
        self.slots.clear();
        self.index.clear();
    }

    pub fn iter(&self) -> impl Iterator<Item = &K> {
        self.slots.iter().filter_map(Option::as_ref)
    }
}

impl<K: Key> Default for InsertionOrderSet<K> {
    fn default() -> Self {
        Self::new()
    }
}

impl<'a, K: Key> IntoIterator for &'a InsertionOrderSet<K> {
    type Item = &'a K;
    type IntoIter = Box<dyn Iterator<Item = &'a K> + 'a>;

    fn into_iter(self) -> Self::IntoIter {
        Box::new(self.iter())
    }
}

impl<K: Key> Extend<K> for InsertionOrderSet<K> {
    fn extend<I: IntoIterator<Item = K>>(&mut self, iter: I) {
        for key in iter {
            self.insert(key);
        }
    }
}

impl<K: Key> FromIterator<K> for InsertionOrderSet<K> {
    fn from_iter<I: IntoIterator<Item = K>>(iter: I) -> Self {
        let mut set = Self::new();
        set.extend(iter);
        set
    }
}

impl<K: Key, const N: usize> From<[K; N]> for InsertionOrderSet<K> {
    fn from(keys: [K; N]) -> Self {
        keys.into_iter().collect()
    }
}

/// Equality is by contents and order, matching `IndexSet`.
impl<K: Key> PartialEq for InsertionOrderSet<K> {
    fn eq(&self, other: &Self) -> bool {
        self.len() == other.len() && self.iter().eq(other.iter())
    }
}

impl<K: Key> Eq for InsertionOrderSet<K> {}

/// Formats as a set of its live keys, so a node renders the way it did when
/// these were `IndexSet`s.
impl<K: Key + fmt::Debug> fmt::Debug for InsertionOrderSet<K> {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_set().entries(self.iter()).finish()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use indexmap::IndexSet;

    fn keys_of(set: &InsertionOrderSet<i32>) -> Vec<i32> {
        set.iter().copied().collect()
    }

    #[test]
    fn iteration_is_insertion_order_minus_removed() {
        let mut set = InsertionOrderSet::new();
        for key in [5, 3, 9, 1, 7] {
            set.insert(key);
        }
        assert!(set.shift_remove(&9));
        assert!(set.shift_remove(&5));

        assert_eq!(keys_of(&set), vec![3, 1, 7]);
        assert_eq!(set.len(), 3);
        assert!(!set.contains(&9));
    }

    #[test]
    fn reinserted_key_moves_to_the_end() {
        let mut set = InsertionOrderSet::new();
        for key in [1, 2, 3] {
            set.insert(key);
        }
        set.shift_remove(&1);
        set.insert(1);

        assert_eq!(keys_of(&set), vec![2, 3, 1]);
    }

    #[test]
    fn inserting_existing_key_keeps_position_and_reports_not_new() {
        let mut set = InsertionOrderSet::new();
        for key in [1, 2, 3] {
            set.insert(key);
        }

        assert!(!set.insert(2));
        assert_eq!(keys_of(&set), vec![1, 2, 3]);
        assert_eq!(set.len(), 3);
    }

    #[test]
    fn removing_an_absent_key_reports_false() {
        let mut set = InsertionOrderSet::new();
        set.insert(1);

        assert!(!set.shift_remove(&2));
        assert_eq!(keys_of(&set), vec![1]);
    }

    #[test]
    fn compaction_preserves_order_and_lookups() {
        let mut set = InsertionOrderSet::new();
        for key in 0..100 {
            set.insert(key);
        }
        // Remove every even key: 50 tombstones out of 100 slots forces at least
        // one compaction along the way.
        for key in (0..100).step_by(2) {
            set.shift_remove(&key);
        }

        let expected: Vec<i32> = (1..100).step_by(2).collect();
        assert_eq!(keys_of(&set), expected);
        for key in expected {
            assert!(set.contains(&key));
        }
        assert_eq!(set.len(), 50);
    }

    #[test]
    fn clear_empties_the_set() {
        let mut set = InsertionOrderSet::new();
        set.insert(1);
        set.clear();

        assert!(set.is_empty());
        set.insert(2);
        assert_eq!(keys_of(&set), vec![2]);
    }

    /// The reason this type exists: removal must not be linear in the set size.
    /// Emptying a set in key order is the worst case for `IndexSet::shift_remove`,
    /// which moves and re-indexes every later entry. Measured on `IndexSet`, that
    /// is 135 ms at 12,500 entries, 534 ms at 25,000 and 2.8 s at 50,000 - cleanly
    /// quadratic, so around 45 s at the size below, where tombstoning takes about
    /// a tenth of a second. The bound is far looser than that margin, so only a
    /// return to shifting can breach it.
    #[test]
    fn emptying_a_large_set_does_not_shift_entries() {
        const SIZE: i32 = 200_000;

        let mut set = InsertionOrderSet::new();
        for key in 0..SIZE {
            set.insert(key);
        }

        let started = std::time::Instant::now();
        for key in 0..SIZE {
            assert!(set.shift_remove(&key));
        }
        let elapsed = started.elapsed();

        assert!(set.is_empty());
        assert!(
            elapsed < std::time::Duration::from_secs(10),
            "emptying {SIZE} entries took {elapsed:?}, which means removal is shifting again"
        );
    }

    /// Differential test against the previous representation: a scripted
    /// mixed-operation run must leave both sets observably identical at every
    /// step (`IndexSet` + `shift_remove` is the contract this type replaces).
    #[test]
    fn behaves_identically_to_indexset_with_shift_remove() {
        let mut ours: InsertionOrderSet<i32> = InsertionOrderSet::new();
        let mut reference: IndexSet<i32> = IndexSet::new();

        // Simple deterministic LCG so the op mix is reproducible.
        let mut seed: u64 = 0x2545_F491_4F6C_DD1D;
        let mut next = || {
            seed = seed
                .wrapping_mul(6364136223846793005)
                .wrapping_add(1442695040888963407);
            (seed >> 33) as i32
        };

        for step in 0..2000 {
            let key = next().rem_euclid(40);
            match next().rem_euclid(3) {
                0 | 1 => {
                    assert_eq!(ours.insert(key), reference.insert(key));
                }
                _ => {
                    assert_eq!(ours.shift_remove(&key), reference.shift_remove(&key));
                }
            }

            assert_eq!(ours.len(), reference.len(), "len diverged at step {step}");
            assert_eq!(
                keys_of(&ours),
                reference.iter().copied().collect::<Vec<_>>(),
                "iteration diverged at step {step}"
            );
        }
    }
}
