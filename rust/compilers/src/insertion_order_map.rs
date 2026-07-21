use std::collections::HashMap;
use std::hash::Hash;

/// A hash map that iterates in insertion order and removes in O(1) amortised
/// time.
///
/// The graph states previously used `IndexMap` with `shift_remove`, whose
/// insertion-order contract the golden tests encode - but `shift_remove` moves
/// every later entry, making each removal O(map size). That made mass dummy-edge
/// removal (deep arrow transitive reduction, `clean_up_edges` on export)
/// quadratic, where the C# `Dictionary.Remove` is O(1). This map keeps the exact
/// same observable contract - iteration is insertion order minus removed
/// entries, a re-inserted key moves to the end, an existing key keeps its
/// position on value replacement - while removal only tombstones the slot.
/// Structurally it is the Rust rendition of what the C# `Dictionary` actually
/// does (an entry array walked in slot order, holes skipped), minus the
/// free-slot reuse the port already deliberately diverged from.
///
/// A removal compacts the slots once more than half of them are tombstones, so
/// iteration stays O(live entries) and the compaction cost amortises to O(1)
/// per removal.
pub(crate) struct InsertionOrderMap<K, V> {
    /// Entries in insertion order; a removed entry leaves a tombstone (`None`)
    /// until the next compaction, so live entries never move on removal.
    slots: Vec<Option<(K, V)>>,
    /// Key -> position in `slots`. Never iterated, so its nondeterministic
    /// order is unobservable.
    index: HashMap<K, usize>,
}

/// Below this many slots a removal never triggers compaction; tiny maps just
/// keep their tombstones until they grow or clear.
const MIN_COMPACT_LEN: usize = 16;

impl<K: Copy + Eq + Hash, V> InsertionOrderMap<K, V> {
    pub(crate) fn new() -> Self {
        Self {
            slots: Vec::new(),
            index: HashMap::new(),
        }
    }

    pub(crate) fn len(&self) -> usize {
        self.index.len()
    }

    pub(crate) fn is_empty(&self) -> bool {
        self.len() == 0
    }

    pub(crate) fn contains_key(&self, key: &K) -> bool {
        self.index.contains_key(key)
    }

    pub(crate) fn get(&self, key: &K) -> Option<&V> {
        self.index.get(key).map(|&slot_index| {
            let (_, value) = self.slots[slot_index]
                .as_ref()
                .expect("indexed slot must be live");
            value
        })
    }

    pub(crate) fn get_mut(&mut self, key: &K) -> Option<&mut V> {
        self.index.get(key).map(|&slot_index| {
            let (_, value) = self.slots[slot_index]
                .as_mut()
                .expect("indexed slot must be live");
            value
        })
    }

    /// Inserts the value, returning the previous value for the key if there was
    /// one. An existing key keeps its iteration position; a new key (including
    /// a key that was previously removed) is appended at the end.
    pub(crate) fn insert(&mut self, key: K, value: V) -> Option<V> {
        match self.index.get(&key) {
            Some(&slot_index) => {
                let slot = self.slots[slot_index]
                    .as_mut()
                    .expect("indexed slot must be live");
                Some(std::mem::replace(&mut slot.1, value))
            }
            None => {
                self.index.insert(key, self.slots.len());
                self.slots.push(Some((key, value)));
                None
            }
        }
    }

    /// Removes the entry, preserving the iteration order of every other entry -
    /// the same observable behaviour as `IndexMap::shift_remove`, in O(1)
    /// amortised time.
    pub(crate) fn remove(&mut self, key: &K) -> Option<V> {
        let slot_index = self.index.remove(key)?;
        let (_, value) = self.slots[slot_index]
            .take()
            .expect("indexed slot must be live");
        if self.slots.len() >= MIN_COMPACT_LEN && self.slots.len() > 2 * self.index.len() {
            self.compact();
        }
        Some(value)
    }

    fn compact(&mut self) {
        self.slots.retain(Option::is_some);
        for (slot_index, slot) in self.slots.iter().enumerate() {
            let (key, _) = slot.as_ref().expect("compaction retained only live slots");
            self.index.insert(*key, slot_index);
        }
    }

    pub(crate) fn keys(&self) -> impl Iterator<Item = &K> {
        self.slots
            .iter()
            .filter_map(|slot| slot.as_ref().map(|(key, _)| key))
    }

    pub(crate) fn values(&self) -> impl Iterator<Item = &V> {
        self.slots
            .iter()
            .filter_map(|slot| slot.as_ref().map(|(_, value)| value))
    }

    pub(crate) fn values_mut(&mut self) -> impl Iterator<Item = &mut V> {
        self.slots
            .iter_mut()
            .filter_map(|slot| slot.as_mut().map(|(_, value)| value))
    }

    pub(crate) fn clear(&mut self) {
        self.slots.clear();
        self.index.clear();
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use indexmap::IndexMap;

    fn keys_of(map: &InsertionOrderMap<i32, i32>) -> Vec<i32> {
        map.keys().copied().collect()
    }

    #[test]
    fn iteration_is_insertion_order_minus_removed() {
        let mut map = InsertionOrderMap::new();
        for key in [5, 3, 9, 1, 7] {
            map.insert(key, key * 10);
        }
        map.remove(&9);
        map.remove(&5);

        assert_eq!(keys_of(&map), vec![3, 1, 7]);
        assert_eq!(map.len(), 3);
        assert!(!map.contains_key(&9));
        assert_eq!(map.get(&1), Some(&10));
    }

    #[test]
    fn reinserted_key_moves_to_the_end() {
        let mut map = InsertionOrderMap::new();
        for key in [1, 2, 3] {
            map.insert(key, key);
        }
        map.remove(&1);
        map.insert(1, 100);

        assert_eq!(keys_of(&map), vec![2, 3, 1]);
        assert_eq!(map.get(&1), Some(&100));
    }

    #[test]
    fn inserting_existing_key_keeps_position_and_replaces_value() {
        let mut map = InsertionOrderMap::new();
        for key in [1, 2, 3] {
            map.insert(key, key);
        }
        let previous = map.insert(2, 200);

        assert_eq!(previous, Some(2));
        assert_eq!(keys_of(&map), vec![1, 2, 3]);
        assert_eq!(map.get(&2), Some(&200));
        assert_eq!(map.len(), 3);
    }

    #[test]
    fn values_mut_mutates_in_iteration_order() {
        let mut map = InsertionOrderMap::new();
        for key in [4, 2, 8] {
            map.insert(key, 0);
        }
        map.remove(&2);
        for (position, value) in map.values_mut().enumerate() {
            *value = position as i32;
        }

        assert_eq!(map.values().copied().collect::<Vec<_>>(), vec![0, 1]);
        assert_eq!(map.get(&8), Some(&1));
    }

    #[test]
    fn compaction_preserves_order_and_lookups() {
        let mut map = InsertionOrderMap::new();
        for key in 0..100 {
            map.insert(key, key * 2);
        }
        // Remove every even key: 50 tombstones out of 100 slots forces at least
        // one compaction along the way.
        for key in (0..100).step_by(2) {
            map.remove(&key);
        }

        let expected: Vec<i32> = (1..100).step_by(2).collect();
        assert_eq!(keys_of(&map), expected);
        for key in expected {
            assert_eq!(map.get(&key), Some(&(key * 2)));
        }
        assert_eq!(map.len(), 50);
    }

    #[test]
    fn clear_empties_the_map() {
        let mut map = InsertionOrderMap::new();
        map.insert(1, 1);
        map.clear();

        assert!(map.is_empty());
        assert_eq!(keys_of(&map), Vec::<i32>::new());
        map.insert(2, 2);
        assert_eq!(keys_of(&map), vec![2]);
    }

    /// Differential test against the previous representation: a scripted
    /// mixed-operation run must leave both maps observably identical at every
    /// step (`IndexMap` + `shift_remove` is the contract this type replaces).
    #[test]
    fn behaves_identically_to_indexmap_with_shift_remove() {
        let mut ours: InsertionOrderMap<i32, i32> = InsertionOrderMap::new();
        let mut reference: IndexMap<i32, i32> = IndexMap::new();

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
            let value = next().rem_euclid(1000);
            match next().rem_euclid(3) {
                0 | 1 => {
                    assert_eq!(ours.insert(key, value), reference.insert(key, value));
                }
                _ => {
                    assert_eq!(ours.remove(&key), reference.shift_remove(&key));
                }
            }

            assert_eq!(ours.len(), reference.len(), "len diverged at step {step}");
            let our_entries: Vec<(i32, i32)> =
                ours.keys().copied().zip(ours.values().copied()).collect();
            let reference_entries: Vec<(i32, i32)> =
                reference.iter().map(|(k, v)| (*k, *v)).collect();
            assert_eq!(
                our_entries, reference_entries,
                "iteration diverged at step {step}"
            );
        }
    }
}
