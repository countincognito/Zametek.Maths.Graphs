use indexmap::{IndexMap, IndexSet};
use zametek_maths_graphs_primitives::{CircularDependency, Key};

/// A node-space view of a graph for the ancestor calculation - the counterpart
/// of the C# `IAncestorGraphView<T>`.
pub(crate) trait AncestorGraphView<K: Key> {
    fn end_node_ids(&self) -> Vec<K>;
    fn is_root_node(&self, node_id: K) -> bool;
    fn parent_node_ids(&self, node_id: K) -> Vec<K>;
}

const BITS_PER_WORD: usize = 64;
const WORD_INDEX_SHIFT: usize = 6;
const BIT_INDEX_MASK: usize = BITS_PER_WORD - 1;

/// Compact ancestor representation used by the transitive reducers - the
/// counterpart of the C# `AncestorBitSets<T>`.
///
/// Every node reachable from an end node is given a dense index; each node's
/// ancestor set is a bitset over those indexes (one `u64` word per 64 nodes),
/// which keeps deep graphs at roughly M*M/8 bytes instead of M*M hash-set
/// entries.
pub(crate) struct AncestorBitSets<K: Key> {
    index_lookup: IndexMap<K, usize>,
    ids: Vec<K>,
    ancestor_words: Vec<Vec<u64>>,
    word_count: usize,
}

impl<K: Key> AncestorBitSets<K> {
    pub(crate) fn word_count_for(node_count: usize) -> usize {
        node_count.div_ceil(BITS_PER_WORD)
    }

    fn word_index_of(dense_index: usize) -> usize {
        dense_index >> WORD_INDEX_SHIFT
    }

    fn bit_mask_of(dense_index: usize) -> u64 {
        1u64 << (dense_index & BIT_INDEX_MASK)
    }

    pub(crate) fn set_bit(words: &mut [u64], dense_index: usize) {
        words[Self::word_index_of(dense_index)] |= Self::bit_mask_of(dense_index);
    }

    pub(crate) fn create_scratch(&self) -> Vec<u64> {
        vec![0; self.word_count]
    }

    pub(crate) fn clear_scratch(scratch: &mut [u64]) {
        scratch.fill(0);
    }

    /// Unions the ancestor set of the given node into the scratch bitset.
    /// Panics for an unknown node id - parity with the C# dictionary indexing.
    pub(crate) fn union_ancestors_into(&self, scratch: &mut [u64], node_id: K) {
        let index = self.index_lookup[&node_id];
        for (i, word) in self.ancestor_words[index].iter().enumerate() {
            scratch[i] |= word;
        }
    }

    /// Whether the given node id is present in the scratch bitset. An unknown
    /// node id is simply not a member - parity with `HashSet::contains`.
    pub(crate) fn scratch_contains(&self, scratch: &[u64], node_id: K) -> bool {
        match self.index_lookup.get(&node_id) {
            None => false,
            Some(&index) => (scratch[Self::word_index_of(index)] & Self::bit_mask_of(index)) != 0,
        }
    }

    /// Materialises the map-of-sets form (the public lookup contract).
    pub(crate) fn to_lookup(&self) -> IndexMap<K, IndexSet<K>> {
        let mut output: IndexMap<K, IndexSet<K>> = IndexMap::with_capacity(self.ids.len());
        for (node_index, id) in self.ids.iter().enumerate() {
            let mut ancestor_set: IndexSet<K> = IndexSet::new();
            for (word_index, mut word) in
                self.ancestor_words[node_index].iter().copied().enumerate()
            {
                let mut dense_index = word_index * BITS_PER_WORD;
                while word != 0 {
                    if (word & 1) != 0 {
                        ancestor_set.insert(self.ids[dense_index]);
                    }
                    word >>= 1;
                    dense_index += 1;
                }
            }
            output.insert(*id, ancestor_set);
        }
        output
    }
}

/// Shared ancestor-node calculation for both arrow and vertex transitive
/// reducers - the counterpart of the C# `AncestorNodeCalculator`.
///
/// Returns `None` when circular dependencies are present (ancestor sets are
/// only meaningful in an acyclic graph). Both phases are iterative, so a deep
/// dependency chain cannot overflow the call stack.
pub(crate) fn get_ancestor_bit_sets<K: Key>(
    view: &impl AncestorGraphView<K>,
    circular_dependencies: &[CircularDependency<K>],
) -> Option<AncestorBitSets<K>> {
    if !circular_dependencies.is_empty() {
        return None;
    }

    let end_node_ids = view.end_node_ids();

    // Phase 1: discover every node reachable from the end nodes and assign each
    // a dense index (discovery order, no gaps).
    let mut index_lookup: IndexMap<K, usize> = IndexMap::new();
    let mut ids: Vec<K> = Vec::new();
    let mut discovery: Vec<K> = end_node_ids.clone();
    while let Some(node_id) = discovery.pop() {
        if index_lookup.contains_key(&node_id) {
            continue;
        }
        index_lookup.insert(node_id, ids.len());
        ids.push(node_id);
        if view.is_root_node(node_id) {
            continue;
        }
        for parent_node_id in view.parent_node_ids(node_id) {
            if !index_lookup.contains_key(&parent_node_id) {
                discovery.push(parent_node_id);
            }
        }
    }

    let node_count = ids.len();
    let word_count = AncestorBitSets::<K>::word_count_for(node_count);
    // One bitset per node; a slot stays None until computed (phase 2's
    // "already done" marker). Root nodes share one empty bitset.
    let mut ancestor_words: Vec<Option<Vec<u64>>> = vec![None; node_count];
    let empty_words: Vec<u64> = vec![0; word_count];

    // Phase 2: compute ancestor bitsets in "parents first" order with an
    // explicit stack (peek; push unresolved parents; resolve when all parents
    // are done).
    let mut stack: Vec<K> = end_node_ids;

    while let Some(&node_id) = stack.last() {
        let node_index = index_lookup[&node_id];

        if ancestor_words[node_index].is_some() {
            stack.pop();
            continue;
        }

        if view.is_root_node(node_id) {
            ancestor_words[node_index] = Some(empty_words.clone());
            stack.pop();
            continue;
        }

        let parent_node_ids = view.parent_node_ids(node_id);
        let mut all_parents_resolved = true;
        for parent_node_id in &parent_node_ids {
            if ancestor_words[index_lookup[parent_node_id]].is_none() {
                stack.push(*parent_node_id);
                all_parents_resolved = false;
            }
        }

        if !all_parents_resolved {
            continue;
        }

        // Every parent resolved: this node's set is each parent itself plus
        // each parent's own ancestors, merged with bitwise OR.
        let mut words: Vec<u64> = vec![0; word_count];
        for parent_node_id in &parent_node_ids {
            let parent_index = index_lookup[parent_node_id];
            AncestorBitSets::<K>::set_bit(&mut words, parent_index);
            let parent_words = ancestor_words[parent_index]
                .as_ref()
                .expect("parent ancestor set must be resolved");
            for i in 0..word_count {
                words[i] |= parent_words[i];
            }
        }

        ancestor_words[node_index] = Some(words);
        stack.pop();
    }

    let ancestor_words: Vec<Vec<u64>> = ancestor_words
        .into_iter()
        .map(|w| w.unwrap_or_else(|| vec![0; word_count]))
        .collect();

    Some(AncestorBitSets {
        index_lookup,
        ids,
        ancestor_words,
        word_count,
    })
}

/// The map-of-sets form of the ancestor lookup - the public contract of the C#
/// `GetAncestorNodesLookup`.
pub(crate) fn get_ancestor_nodes_lookup<K: Key>(
    view: &impl AncestorGraphView<K>,
    circular_dependencies: &[CircularDependency<K>],
) -> Option<IndexMap<K, IndexSet<K>>> {
    get_ancestor_bit_sets(view, circular_dependencies).map(|bits| bits.to_lookup())
}
