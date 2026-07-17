use super::state::{ArrowState, ArrowTraversal};
use crate::ancestor::AncestorBitSets;
use crate::id_gen::IdGenerator;
use crate::tarjan;
use indexmap::{IndexMap, IndexSet};
use zametek_maths_graphs_primitives::{DependentActivity, Edge, GraphError, Key, NodeType};

// All dummy-edge operations for Activity-on-Arrow graphs — the counterpart of
// the C# `DummyEdgeOrchestrator`. Operates directly on the shared
// `ArrowState`; where the C# throws `InvalidOperationException` this port
// returns `Err`.

pub(crate) fn generate_dummy_activity<K: Key, R: Key, W: Key>(
    edge_id_generator: &mut IdGenerator<K>,
) -> Edge<K, DependentActivity<K, R, W>> {
    let dummy_edge_id = edge_id_generator.generate();
    Edge::new(DependentActivity::new_removable(dummy_edge_id, 0, true))
}

pub(crate) fn connect_with_dummy_edge<K: Key, R: Key, W: Key>(
    state: &mut ArrowState<K, R, W>,
    edge_id_generator: &mut IdGenerator<K>,
    tail_node_id: K,
    head_node_id: K,
) {
    let dummy_edge = generate_dummy_activity(edge_id_generator);
    let dummy_edge_id = dummy_edge.id();
    state
        .node_mut(head_node_id)
        .expect("head node must exist")
        .incoming
        .insert(dummy_edge_id);
    state.set_edge_head_node(dummy_edge_id, head_node_id);
    state
        .node_mut(tail_node_id)
        .expect("tail node must exist")
        .outgoing
        .insert(dummy_edge_id);
    state.set_edge_tail_node(dummy_edge_id, tail_node_id);
    state.add_edge(dummy_edge);
}

/// Removes a dummy activity edge, merging adjacent nodes where possible.
/// Returns `Ok(false)` when the edge cannot be removed.
pub(crate) fn remove_dummy_activity<K: Key, R: Key, W: Key>(
    state: &mut ArrowState<K, R, W>,
    activity_id: K,
) -> Result<bool, GraphError> {
    // Retrieve the activity's edge.
    let Some(edge) = state.edge(activity_id) else {
        return Ok(false);
    };
    if !edge.content.is_dummy() {
        return Ok(false);
    }
    if !edge.content.can_be_removed() {
        return Ok(false);
    }

    let tail_node_id = state
        .edge_tail_node_id(activity_id)
        .expect("edge tail must exist");
    let head_node_id = state
        .edge_head_node_id(activity_id)
        .expect("edge head must exist");

    // Check to make sure that no other edges will be made parallel by removing
    // this edge.
    if have_descendant_or_ancestor_overlap(state, tail_node_id, head_node_id)
        && !share_more_than_one_edge(state, tail_node_id, head_node_id)
    {
        return Ok(false);
    }

    // Remove the edge from the tail node.
    state
        .node_mut(tail_node_id)
        .expect("tail node must exist")
        .outgoing
        .shift_remove(&activity_id);
    state.remove_edge_tail_node(activity_id);

    // Remove the edge from the head node.
    state
        .node_mut(head_node_id)
        .expect("head node must exist")
        .incoming
        .shift_remove(&activity_id);
    state.remove_edge_head_node(activity_id);

    // Remove the edge completely.
    state.remove_edge(activity_id);

    let head_node = state.node(head_node_id).expect("head node must exist");
    let head_type = head_node.node_type();
    let head_incoming_empty = head_node.incoming.is_empty();
    let head_outgoing: Vec<K> = head_node.outgoing.iter().copied().collect();

    let tail_node = state.node(tail_node_id).expect("tail node must exist");
    let tail_type = tail_node.node_type();
    let tail_outgoing_empty = tail_node.outgoing.is_empty();
    let tail_incoming: Vec<K> = tail_node.incoming.iter().copied().collect();

    // If the head node is not the End node, and it has no more incoming edges,
    // then transfer the head node's outgoing edges to the tail node.
    if !matches!(head_type, NodeType::End | NodeType::Isolated) && head_incoming_empty {
        for head_node_outgoing_edge_id in head_outgoing {
            let change_tail_success =
                change_edge_tail_node(state, head_node_outgoing_edge_id, tail_node_id)?;
            if !change_tail_success {
                return Err(GraphError::new(format!(
                    "Unable to change tail node of edge {head_node_outgoing_edge_id} to node {tail_node_id} when removing dummy activity {activity_id}"
                )));
            }
        }
    } else if !matches!(tail_type, NodeType::Start | NodeType::Isolated) && tail_outgoing_empty {
        // If the tail node is not the Start node, and it has no more outgoing
        // edges, then transfer the tail node's incoming edges to the head node.
        for tail_node_incoming_edge_id in tail_incoming {
            let change_head_success =
                change_edge_head_node(state, tail_node_incoming_edge_id, head_node_id)?;
            if !change_head_success {
                return Err(GraphError::new(format!(
                    "Unable to change head node of edge {tail_node_incoming_edge_id} to node {head_node_id} when removing dummy activity {activity_id}"
                )));
            }
        }
    }
    Ok(true)
}

/// Redirects redundant dummy edges (canonical arrow-graph normalisation).
pub(crate) fn redirect_dummy_edges<K: Key, R: Key, W: Key>(
    state: &mut ArrowState<K, R, W>,
) -> Result<bool, GraphError> {
    if !state.all_dependencies_satisfied() {
        return Ok(false);
    }

    let circular_dependencies =
        tarjan::find_strongly_circular_dependencies(&ArrowTraversal { state }, false);
    if !circular_dependencies.is_empty() {
        return Ok(false);
    }

    // Go through each node that is not an End or Isolated node, in descending
    // order of event earliest finish time.
    let mut node_ids: Vec<(Option<i32>, K)> = state
        .nodes
        .values()
        .filter(|x| !matches!(x.node_type(), NodeType::End | NodeType::Isolated))
        .map(|x| (x.content.earliest_finish_time, x.id()))
        .collect();
    node_ids.sort_by_key(|x| std::cmp::Reverse(x.0));

    for (_, node_id) in node_ids {
        // The node may have been removed by an earlier redirection pass; the C#
        // original would see its (emptied) edge sets and skip it below.
        let Some(node) = state.node(node_id) else {
            continue;
        };

        // Get the outgoing dummy edges and their head nodes.
        let outgoing_dummy_edge_id_lookup: IndexSet<K> = node
            .outgoing
            .iter()
            .filter(|edge_id| {
                state
                    .edge(**edge_id)
                    .map(|e| e.content.is_dummy() && e.content.can_be_removed())
                    .unwrap_or(false)
            })
            .copied()
            .collect();

        let dummy_edge_successor_node_ids: Vec<K> = outgoing_dummy_edge_id_lookup
            .iter()
            .map(|edge_id| {
                state
                    .edge_head_node_id(*edge_id)
                    .expect("edge head must exist")
            })
            .collect();

        // Now from the successor nodes, work backwards to find all the
        // dependency nodes that share the same successor nodes via dummy edges.

        // First find all the removable dummy edges that have the successor
        // nodes as head nodes.
        let dummy_edge_ids_to_successor_nodes: Vec<Vec<K>> = dummy_edge_successor_node_ids
            .iter()
            .map(|succ_id| {
                state
                    .node(*succ_id)
                    .expect("successor node must exist")
                    .incoming
                    .iter()
                    .filter(|edge_id| {
                        state
                            .edge(**edge_id)
                            .map(|e| e.content.is_dummy() && e.content.can_be_removed())
                            .unwrap_or(false)
                    })
                    .copied()
                    .collect()
            })
            .collect();

        if dummy_edge_ids_to_successor_nodes.is_empty() {
            continue;
        }

        // Now find the subset of dependency nodes that are common to all the
        // successor nodes via removable dummy edges.
        let mut common_dependency_nodes: Vec<K> = dummy_edge_ids_to_successor_nodes[0]
            .iter()
            .map(|edge_id| {
                state
                    .edge_tail_node_id(*edge_id)
                    .expect("edge tail must exist")
            })
            .collect();
        for edge_ids in dummy_edge_ids_to_successor_nodes.iter().skip(1) {
            let next: IndexSet<K> = edge_ids
                .iter()
                .map(|edge_id| {
                    state
                        .edge_tail_node_id(*edge_id)
                        .expect("edge tail must exist")
                })
                .collect();
            common_dependency_nodes.retain(|id| next.contains(id));
        }

        let common_dependency_node_lookup: IndexSet<K> =
            common_dependency_nodes.into_iter().collect();

        // Now filter the dummy edges by whether they originate from the common
        // dependency nodes.
        let common_dependency_edge_ids: Vec<K> = dummy_edge_ids_to_successor_nodes
            .iter()
            .flatten()
            .filter(|edge_id| {
                let tail_id = state
                    .edge_tail_node_id(**edge_id)
                    .expect("edge tail must exist");
                common_dependency_node_lookup.contains(&tail_id)
            })
            .copied()
            .collect();

        // In order to redirect any common dependencies to the original node, it
        // cannot have any successor nodes other than the common successor nodes
        // (i.e. its successor nodes must be a subset of the common successor
        // nodes).
        let all_successor_node_lookup: IndexSet<K> = state
            .node(node_id)
            .expect("node must exist")
            .outgoing
            .iter()
            .map(|edge_id| {
                state
                    .edge_head_node_id(*edge_id)
                    .expect("edge head must exist")
            })
            .collect();
        let common_successor_node_lookup: IndexSet<K> = common_dependency_edge_ids
            .iter()
            .map(|edge_id| {
                state
                    .edge_head_node_id(*edge_id)
                    .expect("edge head must exist")
            })
            .collect();

        if !all_successor_node_lookup
            .iter()
            .all(|id| common_successor_node_lookup.contains(id))
        {
            continue;
        }

        // Redirect all common dependencies towards the original node.
        let mut common_dependency_edge_ids_for_original_node: Vec<K> = common_dependency_edge_ids
            .into_iter()
            .filter(|edge_id| !outgoing_dummy_edge_id_lookup.contains(edge_id))
            .collect();
        common_dependency_edge_ids_for_original_node.sort();
        common_dependency_edge_ids_for_original_node.dedup();

        for common_dependency_edge_id in common_dependency_edge_ids_for_original_node {
            let change_head_success =
                change_edge_head_node(state, common_dependency_edge_id, node_id)?;
            if !change_head_success {
                return Err(GraphError::new(format!(
                    "Unable to change head node of edge {common_dependency_edge_id} to node {node_id} when redirecting dummy activities"
                )));
            }
        }

        remove_parallel_incoming_dummy_edges(state, node_id)?;
    }
    Ok(true)
}

/// Removes dummy edges that are transitively implied.
pub(crate) fn remove_redundant_dummy_edges<K: Key, R: Key, W: Key>(
    state: &mut ArrowState<K, R, W>,
) -> Result<bool, GraphError> {
    if !state.all_dependencies_satisfied() {
        return Ok(false);
    }

    let circular_dependencies =
        tarjan::find_strongly_circular_dependencies(&ArrowTraversal { state }, false);
    if !circular_dependencies.is_empty() {
        return Ok(false);
    }

    // Go through and remove all the dummy edges that are the only outgoing
    // edge of their tail node, and also the only incoming edge of their head
    // node.
    for edge_id in removable_dummy_edges_in_descending_order(state) {
        let Some(tail_id) = state.edge_tail_node_id(edge_id) else {
            continue;
        };
        let Some(head_id) = state.edge_head_node_id(edge_id) else {
            continue;
        };
        let tail_outgoing_count = state
            .node(tail_id)
            .expect("tail node must exist")
            .outgoing
            .len();
        let head_incoming_count = state
            .node(head_id)
            .expect("head node must exist")
            .incoming
            .len();
        if tail_outgoing_count == 1 && head_incoming_count == 1 {
            remove_dummy_activity(state, edge_id)?;
        }
    }

    // Next, go through and remove all the dummy edges that are the only
    // incoming edge of their head node.
    for edge_id in removable_dummy_edges_in_descending_order(state) {
        let Some(head_id) = state.edge_head_node_id(edge_id) else {
            continue;
        };
        if state
            .node(head_id)
            .expect("head node must exist")
            .incoming
            .len()
            == 1
        {
            remove_dummy_activity(state, edge_id)?;
        }
    }

    // Next, go through and remove all the dummy edges that are the only
    // outgoing edge of their tail node.
    for edge_id in removable_dummy_edges_in_descending_order(state) {
        let Some(tail_id) = state.edge_tail_node_id(edge_id) else {
            continue;
        };
        if state
            .node(tail_id)
            .expect("tail node must exist")
            .outgoing
            .len()
            == 1
        {
            remove_dummy_activity(state, edge_id)?;
        }
    }

    // Remove parallel dummy edges (if they exist).
    for node_id in state.node_ids() {
        remove_parallel_incoming_dummy_edges(state, node_id)?;
    }

    Ok(true)
}

fn removable_dummy_edges_in_descending_order<K: Key, R: Key, W: Key>(
    state: &ArrowState<K, R, W>,
) -> Vec<K> {
    get_dummy_edges_in_descending_order(state)
        .into_iter()
        .filter(|edge_id| {
            state
                .edge(*edge_id)
                .map(|e| e.content.can_be_removed())
                .unwrap_or(false)
        })
        .collect()
}

/// The dummy edges in depth-first discovery order from the Start node — the
/// counterpart of the C# `GetDummyEdgesInDescendingOrder` (implemented
/// iteratively so a deep graph cannot overflow the call stack; the output
/// order is identical).
pub(crate) fn get_dummy_edges_in_descending_order<K: Key, R: Key, W: Key>(
    state: &ArrowState<K, R, W>,
) -> Vec<K> {
    let mut recorded_edges: IndexSet<K> = IndexSet::new();
    let mut edges_in_descending_order: Vec<K> = Vec::new();

    let Some(start_node_id) = state.start_node_id else {
        return Vec::new();
    };

    // Each frame is one in-flight recursive call: the node's outgoing edges
    // plus the iteration cursor.
    struct Frame<K> {
        edges: Vec<K>,
        cursor: usize,
    }

    let node_outgoing = |node_id: K| -> Vec<K> {
        let Some(node) = state.node(node_id) else {
            return Vec::new();
        };
        if matches!(node.node_type(), NodeType::End | NodeType::Isolated) {
            return Vec::new();
        }
        node.outgoing.iter().copied().collect()
    };

    let mut frames: Vec<Frame<K>> = vec![Frame {
        edges: node_outgoing(start_node_id),
        cursor: 0,
    }];

    while let Some(frame) = frames.last_mut() {
        if frame.cursor >= frame.edges.len() {
            frames.pop();
            continue;
        }
        let edge_id = frame.edges[frame.cursor];
        frame.cursor += 1;

        // Record the edge and descend into its head node. (Descending again
        // for an already-recorded edge cannot record anything new, since the
        // state is frozen for the duration of the walk, so the recursion is
        // guarded without changing the output order.)
        if recorded_edges.insert(edge_id) {
            edges_in_descending_order.push(edge_id);
            let head_node_id = state
                .edge_head_node_id(edge_id)
                .expect("edge head must exist");
            frames.push(Frame {
                edges: node_outgoing(head_node_id),
                cursor: 0,
            });
        }
    }

    edges_in_descending_order
        .into_iter()
        .filter(|edge_id| {
            state
                .edge(*edge_id)
                .map(|e| e.content.is_dummy())
                .unwrap_or(false)
        })
        .collect()
}

/// Check to make sure that no other edges will be made parallel by removing
/// this edge. If there is an intersection between the ancestor/descendant
/// nodes of the edge's tail node, and the ancestor/descendant nodes of the
/// head node, then do not remove it.
fn have_descendant_or_ancestor_overlap<K: Key, R: Key, W: Key>(
    state: &ArrowState<K, R, W>,
    tail_node_id: K,
    head_node_id: K,
) -> bool {
    let tail_node = state.node(tail_node_id).expect("tail node must exist");
    let head_node = state.node(head_node_id).expect("head node must exist");

    let mut tail_neighbours: IndexSet<K> = IndexSet::new();
    // First the descendants of the tail node.
    if !matches!(tail_node.node_type(), NodeType::End | NodeType::Isolated) {
        tail_neighbours.extend(
            tail_node
                .outgoing
                .iter()
                .map(|e| state.edge_head_node_id(*e).expect("edge head must exist"))
                .filter(|id| *id != head_node_id),
        );
    }
    // Then the ancestors of the tail node.
    if !matches!(tail_node.node_type(), NodeType::Start | NodeType::Isolated) {
        tail_neighbours.extend(
            tail_node
                .incoming
                .iter()
                .map(|e| state.edge_tail_node_id(*e).expect("edge tail must exist"))
                .filter(|id| *id != head_node_id),
        );
    }

    let mut head_neighbours: IndexSet<K> = IndexSet::new();
    // Next the ancestors of the head node.
    if !matches!(head_node.node_type(), NodeType::Start | NodeType::Isolated) {
        head_neighbours.extend(
            head_node
                .incoming
                .iter()
                .map(|e| state.edge_tail_node_id(*e).expect("edge tail must exist"))
                .filter(|id| *id != tail_node_id),
        );
    }
    // Then the descendants of the head node.
    if !matches!(head_node.node_type(), NodeType::End | NodeType::Isolated) {
        head_neighbours.extend(
            head_node
                .outgoing
                .iter()
                .map(|e| state.edge_head_node_id(*e).expect("edge head must exist"))
                .filter(|id| *id != tail_node_id),
        );
    }

    tail_neighbours
        .iter()
        .any(|id| head_neighbours.contains(id))
}

fn share_more_than_one_edge<K: Key, R: Key, W: Key>(
    state: &ArrowState<K, R, W>,
    tail_node_id: K,
    head_node_id: K,
) -> bool {
    let tail_node = state.node(tail_node_id).expect("tail node must exist");
    let head_node = state.node(head_node_id).expect("head node must exist");

    let tail_outgoing: IndexSet<K> =
        if !matches!(tail_node.node_type(), NodeType::End | NodeType::Isolated) {
            tail_node.outgoing.iter().copied().collect()
        } else {
            IndexSet::new()
        };
    let head_incoming: IndexSet<K> =
        if !matches!(head_node.node_type(), NodeType::Start | NodeType::Isolated) {
            head_node.incoming.iter().copied().collect()
        } else {
            IndexSet::new()
        };

    tail_outgoing
        .iter()
        .filter(|e| head_incoming.contains(*e))
        .count()
        > 1
}

pub(crate) fn remove_parallel_incoming_dummy_edges<K: Key, R: Key, W: Key>(
    state: &mut ArrowState<K, R, W>,
    node_id: K,
) -> Result<(), GraphError> {
    // Clean up any dummy edges that are parallel coming into the head node.
    let Some(node) = state.node(node_id) else {
        return Ok(());
    };
    if matches!(node.node_type(), NodeType::Start | NodeType::Isolated) {
        return Ok(());
    }

    // First, find the tail nodes that connect to this node via dummy edges.
    let removable_incoming_dummy_edge_ids: Vec<K> = node
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

    let mut tail_node_parallel_dummy_edges_lookup: IndexMap<K, IndexSet<K>> = IndexMap::new();
    for incoming_dummy_edge_id in removable_incoming_dummy_edge_ids {
        let tail_node_id = state
            .edge_tail_node_id(incoming_dummy_edge_id)
            .expect("edge tail must exist");
        tail_node_parallel_dummy_edges_lookup
            .entry(tail_node_id)
            .or_default()
            .insert(incoming_dummy_edge_id);
    }

    // Now find the tail nodes that connect to this node via multiple dummy edges.
    let sets_of_more_than_one_dummy_edge: Vec<Vec<K>> = tail_node_parallel_dummy_edges_lookup
        .values()
        .filter(|edges| edges.len() > 1)
        .map(|edges| edges.iter().copied().collect())
        .collect();

    for dummy_edge_ids in sets_of_more_than_one_dummy_edge {
        // Leave one dummy edge behind.
        for dummy_edge_id in dummy_edge_ids.into_iter().skip(1) {
            remove_dummy_activity(state, dummy_edge_id)?;
        }
    }
    Ok(())
}

fn change_edge_tail_node_without_cleanup<K: Key, R: Key, W: Key>(
    state: &mut ArrowState<K, R, W>,
    edge_id: K,
    new_tail_node_id: K,
) -> bool {
    // Do not attend this unless all dependencies are satisfied.
    if !state.all_dependencies_satisfied() {
        return false;
    }
    // Retrieve the activity edge.
    if !state.contains_edge(edge_id) {
        return false;
    }
    // Retrieve the new tail event node.
    if !state.contains_node(new_tail_node_id) {
        return false;
    }

    // Remove the connection from the current tail node.
    let old_tail_node_id = state
        .edge_tail_node_id(edge_id)
        .expect("edge tail must exist");
    state
        .node_mut(old_tail_node_id)
        .expect("old tail node must exist")
        .outgoing
        .shift_remove(&edge_id);
    state.remove_edge_tail_node(edge_id);

    // Attach to the new tail node.
    state
        .node_mut(new_tail_node_id)
        .expect("new tail node must exist")
        .outgoing
        .insert(edge_id);
    state.set_edge_tail_node(edge_id, new_tail_node_id);
    true
}

fn change_edge_head_node_without_cleanup<K: Key, R: Key, W: Key>(
    state: &mut ArrowState<K, R, W>,
    edge_id: K,
    new_head_node_id: K,
) -> bool {
    // Do not attend this unless all dependencies are satisfied.
    if !state.all_dependencies_satisfied() {
        return false;
    }
    // Retrieve the activity edge.
    if !state.contains_edge(edge_id) {
        return false;
    }
    // Retrieve the new head event node.
    if !state.contains_node(new_head_node_id) {
        return false;
    }

    // Remove the connection from the current head node.
    let current_head_node_id = state
        .edge_head_node_id(edge_id)
        .expect("edge head must exist");
    state
        .node_mut(current_head_node_id)
        .expect("current head node must exist")
        .incoming
        .shift_remove(&edge_id);
    state.remove_edge_head_node(edge_id);

    // Attach to the new head node.
    state
        .node_mut(new_head_node_id)
        .expect("new head node must exist")
        .incoming
        .insert(edge_id);
    state.set_edge_head_node(edge_id, new_head_node_id);
    true
}

fn change_edge_tail_node<K: Key, R: Key, W: Key>(
    state: &mut ArrowState<K, R, W>,
    edge_id: K,
    new_tail_node_id: K,
) -> Result<bool, GraphError> {
    if !state.all_dependencies_satisfied() {
        return Ok(false);
    }

    let old_tail_node_id = state
        .edge_tail_node_id(edge_id)
        .expect("edge tail must exist");
    let change_tail_success =
        change_edge_tail_node_without_cleanup(state, edge_id, new_tail_node_id);
    if !change_tail_success {
        return Err(GraphError::new(format!(
            "Unable to change tail node of edge {edge_id} to node {new_tail_node_id} without cleanup"
        )));
    }

    // If the old tail node has no other outgoing edges, then connect its
    // incoming edges to the current head node.
    let old_tail_outgoing_empty = state
        .node(old_tail_node_id)
        .expect("old tail node must exist")
        .outgoing
        .is_empty();
    if old_tail_outgoing_empty {
        let head_node_id = state
            .edge_head_node_id(edge_id)
            .expect("edge head must exist");
        let old_tail_incoming: Vec<K> = state
            .node(old_tail_node_id)
            .expect("old tail node must exist")
            .incoming
            .iter()
            .copied()
            .collect();
        for old_tail_incoming_edge_id in old_tail_incoming {
            let change_head_success = change_edge_head_node_without_cleanup(
                state,
                old_tail_incoming_edge_id,
                head_node_id,
            );
            if !change_head_success {
                return Err(GraphError::new(format!(
                    "Unable to change head node of edge {old_tail_incoming_edge_id} to node {head_node_id} without cleanup"
                )));
            }
        }
    }

    // Final check to see if the tail node has no incoming or outgoing edges.
    // If it does not then remove it.
    let old_tail_node = state
        .node(old_tail_node_id)
        .expect("old tail node must exist");
    if !matches!(
        old_tail_node.node_type(),
        NodeType::Start | NodeType::Isolated
    ) && old_tail_node.incoming.is_empty()
        && old_tail_node.outgoing.is_empty()
    {
        state.remove_node(old_tail_node_id);
    }
    Ok(true)
}

fn change_edge_head_node<K: Key, R: Key, W: Key>(
    state: &mut ArrowState<K, R, W>,
    edge_id: K,
    new_head_node_id: K,
) -> Result<bool, GraphError> {
    // Do not attend this unless all dependencies are satisfied.
    if !state.all_dependencies_satisfied() {
        return Ok(false);
    }

    let old_head_node_id = state
        .edge_head_node_id(edge_id)
        .expect("edge head must exist");
    let change_head_success =
        change_edge_head_node_without_cleanup(state, edge_id, new_head_node_id);
    if !change_head_success {
        return Err(GraphError::new(format!(
            "Unable to change head node of edge {edge_id} to node {new_head_node_id} without cleanup"
        )));
    }

    // If the old head node has no other incoming edges, then connect its
    // outgoing edges to the current tail node.
    let old_head_incoming_empty = state
        .node(old_head_node_id)
        .expect("old head node must exist")
        .incoming
        .is_empty();
    if old_head_incoming_empty {
        let tail_node_id = state
            .edge_tail_node_id(edge_id)
            .expect("edge tail must exist");
        let old_head_outgoing: Vec<K> = state
            .node(old_head_node_id)
            .expect("old head node must exist")
            .outgoing
            .iter()
            .copied()
            .collect();
        for old_head_outgoing_edge_id in old_head_outgoing {
            let change_tail_success = change_edge_tail_node_without_cleanup(
                state,
                old_head_outgoing_edge_id,
                tail_node_id,
            );
            if !change_tail_success {
                return Err(GraphError::new(format!(
                    "Unable to change tail node of edge {old_head_outgoing_edge_id} to node {tail_node_id} without cleanup"
                )));
            }
        }
    }

    // Final check to see if the head node has no incoming or outgoing edges.
    // If it does not then remove it.
    let old_head_node = state
        .node(old_head_node_id)
        .expect("old head node must exist");
    if !matches!(
        old_head_node.node_type(),
        NodeType::End | NodeType::Isolated
    ) && old_head_node.incoming.is_empty()
        && old_head_node.outgoing.is_empty()
    {
        state.remove_node(old_head_node_id);
    }
    Ok(true)
}

/// Removes redundant incoming dummy edges using the compact ancestor bitsets —
/// the counterpart of the C# `RemoveRedundantIncomingDummyEdges` (bitset form).
pub(crate) fn remove_redundant_incoming_dummy_edges<K: Key, R: Key, W: Key>(
    state: &mut ArrowState<K, R, W>,
    root_node_ids: Vec<K>,
    ancestor_bit_sets: &AncestorBitSets<K>,
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
                remove_dummy_activity(state, dummy_edge_id)?;
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
