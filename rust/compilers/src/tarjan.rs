use indexmap::IndexMap;
use zametek_maths_graphs_primitives::{CircularDependency, Key};

/// A read-only traversal view over a graph, in whichever space the activities
/// live (edge-space for arrow graphs, node-space for vertex graphs) - the
/// counterpart of the C# `IGraphTraversal<T>`.
pub(crate) trait GraphTraversal<K: Key> {
    fn keys(&self) -> Vec<K>;
    fn predecessor_keys(&self, reference_id: K) -> Vec<K>;
    fn is_removable(&self, reference_id: K) -> bool;
}

/// Tarjan's strongly connected components algorithm, written once over a
/// [`GraphTraversal`] view - the counterpart of the C#
/// `TarjanStronglyConnectedComponents`.
///
/// The depth-first search is iterative (explicit frame stack), so very deep
/// graphs cannot overflow the call stack.
pub(crate) fn find_strongly_connected_components<K: Key>(
    traversal: &impl GraphTraversal<K>,
    ignore_dummies: bool,
) -> Vec<CircularDependency<K>> {
    let key_list = traversal.keys();

    let mut index: i64 = 0;
    let mut stack: Vec<K> = Vec::new();
    let mut on_stack: indexmap::IndexSet<K> = indexmap::IndexSet::new();
    let mut index_lookup: IndexMap<K, i64> = IndexMap::with_capacity(key_list.len());
    let mut low_link_lookup: IndexMap<K, i64> = IndexMap::with_capacity(key_list.len());
    let mut circular_dependencies: Vec<CircularDependency<K>> = Vec::new();

    for id in &key_list {
        index_lookup.insert(*id, -1);
        low_link_lookup.insert(*id, -1);
    }

    // Each frame is one in-flight StrongConnect "call": the key being visited
    // plus the iteration cursor over its predecessors.
    struct Frame<K> {
        reference_id: K,
        predecessors: Vec<K>,
        cursor: usize,
    }

    let mut frames: Vec<Frame<K>> = Vec::new();

    let begin_strong_connect = |reference_id: K,
                                index: &mut i64,
                                stack: &mut Vec<K>,
                                on_stack: &mut indexmap::IndexSet<K>,
                                index_lookup: &mut IndexMap<K, i64>,
                                low_link_lookup: &mut IndexMap<K, i64>,
                                frames: &mut Vec<Frame<K>>,
                                traversal: &dyn Fn(K) -> Vec<K>| {
        index_lookup[&reference_id] = *index;
        low_link_lookup[&reference_id] = *index;
        *index += 1;
        stack.push(reference_id);
        on_stack.insert(reference_id);
        frames.push(Frame {
            reference_id,
            predecessors: traversal(reference_id),
            cursor: 0,
        });
    };

    let predecessor_fn = |id: K| traversal.predecessor_keys(id);

    for root_id in &key_list {
        if index_lookup[root_id] >= 0 {
            continue;
        }

        begin_strong_connect(
            *root_id,
            &mut index,
            &mut stack,
            &mut on_stack,
            &mut index_lookup,
            &mut low_link_lookup,
            &mut frames,
            &predecessor_fn,
        );

        while let Some(frame_index) = frames.len().checked_sub(1) {
            let reference_id = frames[frame_index].reference_id;
            let mut descended = false;

            while frames[frame_index].cursor < frames[frame_index].predecessors.len() {
                let predecessor_id = frames[frame_index].predecessors[frames[frame_index].cursor];
                frames[frame_index].cursor += 1;

                if index_lookup[&predecessor_id] < 0 {
                    // Descend into the unvisited predecessor (the recursive call).
                    begin_strong_connect(
                        predecessor_id,
                        &mut index,
                        &mut stack,
                        &mut on_stack,
                        &mut index_lookup,
                        &mut low_link_lookup,
                        &mut frames,
                        &predecessor_fn,
                    );
                    descended = true;
                    break;
                } else if on_stack.contains(&predecessor_id) {
                    let updated = low_link_lookup[&reference_id].min(index_lookup[&predecessor_id]);
                    low_link_lookup[&reference_id] = updated;
                }
            }

            if descended {
                continue;
            }

            // All predecessors handled - the "call" for reference_id returns.
            frames.pop();

            if low_link_lookup[&reference_id] == index_lookup[&reference_id] {
                let mut dependencies: Vec<K> = Vec::new();
                loop {
                    let current_id = stack.pop().expect("Tarjan stack must not be empty");
                    on_stack.shift_remove(&current_id);

                    let is_dummy = traversal.is_removable(current_id);
                    if !ignore_dummies || !is_dummy {
                        dependencies.push(current_id);
                    }
                    if current_id == reference_id {
                        break;
                    }
                }
                circular_dependencies.push(CircularDependency::new(dependencies));
            }

            // Propagate the low-link back to the caller frame.
            if let Some(parent) = frames.last() {
                let parent_id = parent.reference_id;
                let updated = low_link_lookup[&parent_id].min(low_link_lookup[&reference_id]);
                low_link_lookup[&parent_id] = updated;
            }
        }
    }

    circular_dependencies
}

/// The strongly connected components with more than one member - the actual
/// circular dependencies.
pub(crate) fn find_strongly_circular_dependencies<K: Key>(
    traversal: &impl GraphTraversal<K>,
    ignore_dummies: bool,
) -> Vec<CircularDependency<K>> {
    find_strongly_connected_components(traversal, ignore_dummies)
        .into_iter()
        .filter(|x| x.dependencies.len() > 1)
        .collect()
}
