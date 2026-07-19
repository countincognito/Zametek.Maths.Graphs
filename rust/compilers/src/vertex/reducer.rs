use super::state::{VertexAncestorView, VertexGraphState};
use crate::ancestor::{self, AncestorBitSets};
use crate::contracts::IVertexStronglyConnectedComponentsFinder;
use indexmap::{IndexMap, IndexSet};
use zametek_maths_graphs_primitives::{Key, NodeType};

// Transitive reduction for Activity-on-Vertex graphs - the counterpart of the
// C# `VertexTransitiveReducer`. Stateless: the graph state and the injected SCC
// finder are supplied per call.

/// Builds a lookup from each node ID to the full set of its ancestor node IDs.
/// Returns `None` if the graph has unsatisfied or circular dependencies.
pub(crate) fn get_ancestor_nodes_lookup<K: Key, R: Key, W: Key>(
    state: &VertexGraphState<K, R, W>,
    scc_finder: &dyn IVertexStronglyConnectedComponentsFinder<K, R, W>,
) -> Option<IndexMap<K, IndexSet<K>>> {
    if !state.all_dependencies_satisfied() {
        return None;
    }

    let circular_dependencies = scc_finder.find_strongly_circular_dependencies(state, false);

    ancestor::get_ancestor_nodes_lookup(&VertexAncestorView { state }, &circular_dependencies)
}

fn get_ancestor_bit_sets<K: Key, R: Key, W: Key>(
    state: &VertexGraphState<K, R, W>,
    scc_finder: &dyn IVertexStronglyConnectedComponentsFinder<K, R, W>,
) -> Option<AncestorBitSets<K>> {
    if !state.all_dependencies_satisfied() {
        return None;
    }

    let circular_dependencies = scc_finder.find_strongly_circular_dependencies(state, false);

    ancestor::get_ancestor_bit_sets(&VertexAncestorView { state }, &circular_dependencies)
}

/// Performs transitive reduction, removing all redundant edges. Returns false
/// if it cannot be performed.
pub(crate) fn reduce_graph<K: Key, R: Key, W: Key>(
    state: &mut VertexGraphState<K, R, W>,
    scc_finder: &dyn IVertexStronglyConnectedComponentsFinder<K, R, W>,
) -> bool {
    let Some(ancestor_bit_sets) = get_ancestor_bit_sets(state, scc_finder) else {
        return false;
    };

    let root_node_ids = state.nodes_of_type(NodeType::End);
    remove_redundant_incoming_edges(state, root_node_ids, &ancestor_bit_sets);

    true
}

// Iterative walk with a single shared visited set: each node removes only its
// own incoming edges, using the static ancestor bitsets, so the operation is
// independent of visit order and idempotent per node.
fn remove_redundant_incoming_edges<K: Key, R: Key, W: Key>(
    state: &mut VertexGraphState<K, R, W>,
    root_node_ids: Vec<K>,
    ancestor_bit_sets: &AncestorBitSets<K>,
) {
    let mut visited: IndexSet<K> = IndexSet::new();
    let mut stack: Vec<K> = root_node_ids;
    let mut scratch = ancestor_bit_sets.create_scratch();

    while let Some(node_id) = stack.pop() {
        if !visited.insert(node_id) {
            continue;
        }

        let Some(node) = state.node(node_id) else {
            continue;
        };

        if matches!(node.node_type(), NodeType::Start | NodeType::Isolated) {
            continue;
        }

        // Go through all the incoming edges and collate the ancestors of their
        // tail nodes into the scratch bitset.
        AncestorBitSets::<K>::clear_scratch(&mut scratch);
        for incoming_edge_id in &node.incoming {
            let tail_id = state
                .edge_tail_node_id(*incoming_edge_id)
                .expect("edge tail must exist");
            ancestor_bit_sets.union_ancestors_into(&mut scratch, tail_id);
        }

        // Go through the incoming edges and remove any that connect directly
        // to any ancestors of the edges' tail nodes. In a vertex graph, all
        // edges are removable.
        let removable_edge_ids: Vec<K> = node
            .incoming
            .iter()
            .filter(|edge_id| {
                state
                    .edge(**edge_id)
                    .map(|e| e.content.can_be_removed())
                    .unwrap_or(false)
            })
            .copied()
            .collect();

        for edge_id in removable_edge_ids {
            let tail_node_id = state
                .edge_tail_node_id(edge_id)
                .expect("edge tail must exist");
            if ancestor_bit_sets.scratch_contains(&scratch, tail_node_id) {
                // Remove the edge from the tail node.
                if let Some(tail_node) = state.node_mut(tail_node_id) {
                    tail_node.outgoing.shift_remove(&edge_id);
                }
                state.remove_edge_tail_node(edge_id);

                // Remove the edge from the node itself.
                if let Some(node) = state.node_mut(node_id) {
                    node.incoming.shift_remove(&edge_id);
                }
                state.remove_edge_head_node(edge_id);

                // Remove the edge completely.
                state.remove_edge(edge_id);
            }
        }

        // Continue with all the remaining incoming edges' tail nodes.
        let remaining_tails: Vec<K> = state
            .node(node_id)
            .expect("node must exist")
            .incoming
            .iter()
            .map(|edge_id| {
                state
                    .edge_tail_node_id(*edge_id)
                    .expect("edge tail must exist")
            })
            .collect();
        for tail_node_id in remaining_tails {
            stack.push(tail_node_id);
        }
    }
}
