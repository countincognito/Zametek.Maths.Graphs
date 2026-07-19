use super::state::{ArrowAncestorView, ArrowGraphState};
use crate::ancestor::{self, AncestorBitSets};
use crate::contracts::{IArrowStronglyConnectedComponentsFinder, IDummyEdgeOrchestrator};
use indexmap::{IndexMap, IndexSet};
use zametek_maths_graphs_primitives::{GraphError, Key, NodeType};

// Transitive reduction for Activity-on-Arrow graphs — the counterpart of the
// C# `ArrowTransitiveReducer`. Only dummy edges are reduced. Stateless: the
// graph state, the injected SCC finder and the injected dummy-edge orchestrator
// are supplied per call. The reduction walk lives here and removes each
// redundant dummy edge through the orchestrator's `remove_dummy_activity` — the
// orchestrator owns edge mutation; the reducer owns the traversal.

/// Builds a lookup from each node ID to the full set of its ancestor node IDs.
/// Returns `None` if the graph has unsatisfied or circular dependencies.
pub(crate) fn get_ancestor_nodes_lookup<K: Key, R: Key, W: Key>(
    state: &ArrowGraphState<K, R, W>,
    scc_finder: &dyn IArrowStronglyConnectedComponentsFinder<K, R, W>,
) -> Option<IndexMap<K, IndexSet<K>>> {
    if !state.all_dependencies_satisfied() {
        return None;
    }

    let circular_dependencies = scc_finder.find_strongly_circular_dependencies(state, false);

    ancestor::get_ancestor_nodes_lookup(&ArrowAncestorView { state }, &circular_dependencies)
}

fn get_ancestor_bit_sets<K: Key, R: Key, W: Key>(
    state: &ArrowGraphState<K, R, W>,
    scc_finder: &dyn IArrowStronglyConnectedComponentsFinder<K, R, W>,
) -> Option<AncestorBitSets<K>> {
    if !state.all_dependencies_satisfied() {
        return None;
    }

    let circular_dependencies = scc_finder.find_strongly_circular_dependencies(state, false);

    ancestor::get_ancestor_bit_sets(&ArrowAncestorView { state }, &circular_dependencies)
}

/// Performs transitive reduction, removing all redundant dummy edges. Returns
/// `Ok(false)` if it cannot be performed.
pub(crate) fn reduce_graph<K: Key, R: Key, W: Key>(
    state: &mut ArrowGraphState<K, R, W>,
    scc_finder: &dyn IArrowStronglyConnectedComponentsFinder<K, R, W>,
    orchestrator: &dyn IDummyEdgeOrchestrator<K, R, W>,
) -> Result<bool, GraphError> {
    let Some(ancestor_bit_sets) = get_ancestor_bit_sets(state, scc_finder) else {
        return Ok(false);
    };

    let end_node_ids = state.nodes_of_type(NodeType::End);

    remove_redundant_incoming_dummy_edges(state, end_node_ids, &ancestor_bit_sets, orchestrator)?;

    Ok(true)
}

// Iterative walk with a single shared visited set: each node removes only its
// own incoming dummy edges, using the static ancestor bitsets, so the operation
// is independent of visit order and idempotent per node. Removal is delegated to
// the injected orchestrator so a custom orchestrator can still customise how a
// dummy edge is removed.
fn remove_redundant_incoming_dummy_edges<K: Key, R: Key, W: Key>(
    state: &mut ArrowGraphState<K, R, W>,
    root_node_ids: Vec<K>,
    ancestor_bit_sets: &AncestorBitSets<K>,
    orchestrator: &dyn IDummyEdgeOrchestrator<K, R, W>,
) -> Result<(), GraphError> {
    let mut visited: IndexSet<K> = IndexSet::new();
    let mut stack: Vec<K> = root_node_ids;
    let mut scratch = ancestor_bit_sets.create_scratch();

    while let Some(current_node_id) = stack.pop() {
        if !visited.insert(current_node_id) {
            continue;
        }

        let Some(node) = state.node(current_node_id) else {
            continue;
        };

        if matches!(node.node_type(), NodeType::Start | NodeType::Isolated) {
            continue;
        }

        // Go through all the incoming edges and collate the ancestors of their
        // tail nodes.
        AncestorBitSets::<K>::clear_scratch(&mut scratch);
        for incoming_edge_id in &node.incoming {
            let tail_id = state
                .edge_tail_node_id(*incoming_edge_id)
                .expect("edge tail must exist");
            ancestor_bit_sets.union_ancestors_into(&mut scratch, tail_id);
        }

        // Go through the incoming dummy edges and remove any that connect
        // directly to any ancestors of the non-dummy edges' tail nodes.
        let incoming_dummy_edges: Vec<K> = node
            .incoming
            .iter()
            .filter(|edge_id| {
                state
                    .edge(**edge_id)
                    .map(|e| e.content.is_dummy() && e.content.can_be_removed())
                    .unwrap_or(false)
            })
            .copied()
            .collect();

        for dummy_edge_id in incoming_dummy_edges {
            let Some(dummy_edge_tail_node_id) = state.edge_tail_node_id(dummy_edge_id) else {
                continue;
            };
            if ancestor_bit_sets.scratch_contains(&scratch, dummy_edge_tail_node_id) {
                orchestrator.remove_dummy_activity(state, dummy_edge_id)?;
            }
        }

        // Continue with all the remaining incoming edges' tail nodes.
        let remaining_tails: Vec<K> = match state.node(current_node_id) {
            Some(node) => node
                .incoming
                .iter()
                .map(|edge_id| {
                    state
                        .edge_tail_node_id(*edge_id)
                        .expect("edge tail must exist")
                })
                .collect(),
            None => Vec::new(),
        };
        for tail_node_id in remaining_tails {
            stack.push(tail_node_id);
        }
    }
    Ok(())
}
