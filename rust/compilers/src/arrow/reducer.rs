use super::orchestrator;
use super::state::{ArrowAncestorView, ArrowState, ArrowTraversal};
use crate::ancestor::{self, AncestorBitSets};
use crate::tarjan;
use indexmap::{IndexMap, IndexSet};
use zametek_maths_graphs_primitives::{GraphError, Key, NodeType};

// Transitive reduction for Activity-on-Arrow graphs — the counterpart of the
// C# `ArrowTransitiveReducer`. Only dummy edges are reduced in arrow graphs.

/// Builds a lookup from each node ID to the full set of its ancestor node IDs.
/// Returns `None` if the graph has unsatisfied or circular dependencies.
pub(crate) fn get_ancestor_nodes_lookup<K: Key, R: Key, W: Key>(
    state: &ArrowState<K, R, W>,
) -> Option<IndexMap<K, IndexSet<K>>> {
    if !state.all_dependencies_satisfied() {
        return None;
    }

    let circular_dependencies =
        tarjan::find_strongly_circular_dependencies(&ArrowTraversal { state }, false);

    ancestor::get_ancestor_nodes_lookup(&ArrowAncestorView { state }, &circular_dependencies)
}

fn get_ancestor_bit_sets<K: Key, R: Key, W: Key>(
    state: &ArrowState<K, R, W>,
) -> Option<AncestorBitSets<K>> {
    if !state.all_dependencies_satisfied() {
        return None;
    }

    let circular_dependencies =
        tarjan::find_strongly_circular_dependencies(&ArrowTraversal { state }, false);

    ancestor::get_ancestor_bit_sets(&ArrowAncestorView { state }, &circular_dependencies)
}

/// Performs transitive reduction, removing all redundant dummy edges. Returns
/// `Ok(false)` if it cannot be performed.
pub(crate) fn reduce_graph<K: Key, R: Key, W: Key>(
    state: &mut ArrowState<K, R, W>,
) -> Result<bool, GraphError> {
    let Some(ancestor_bit_sets) = get_ancestor_bit_sets(state) else {
        return Ok(false);
    };

    let end_node_ids = state.nodes_of_type(NodeType::End);

    orchestrator::remove_redundant_incoming_dummy_edges(state, end_node_ids, &ancestor_bit_sets)?;

    Ok(true)
}
