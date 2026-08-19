use super::state::VertexGraphState;
use crate::messages;
use crate::shuffle::shuffle;
use indexmap::IndexMap;
use zametek_maths_graphs_primitives::{
    DependentActivity, GraphError, InsertionOrderSet, InvalidConstraint, Key, NodeType,
};

// The critical-path engine for Activity-on-Vertex graphs - the counterpart of
// the C# `VertexCriticalPathEngine`. Implements the forward pass (earliest
// start times), backward pass (latest finish times and free slack), and the
// isolated-node backfill.
//
// Returns `Ok(false)` where the C# returns `false` (preconditions not met)
// and `Err` where the C# throws (a cycle was encountered mid-flow).

// Records that a forward-flow edge is complete, and queues the node at its head
// if that was the last thing it was waiting for. An End node is never queued: it
// has no outgoing edges to carry the flow onwards and is completed by the End
// node pass instead.
fn release_successor<K: Key, R: Key, W: Key>(
    state: &VertexGraphState<K, R, W>,
    edge_id: K,
    pending_incoming_edge_counts: &mut IndexMap<K, usize>,
    ready_nodes: &mut Vec<K>,
) {
    let successor_node_id = state
        .edge_head_node_id(edge_id)
        .expect("edge head must exist");
    let pending = pending_incoming_edge_counts
        .get_mut(&successor_node_id)
        .expect("node with incoming edges must have a pending count");
    *pending -= 1;

    if *pending == 0
        && state
            .node(successor_node_id)
            .expect("node must exist")
            .node_type()
            != NodeType::End
    {
        ready_nodes.push(successor_node_id);
    }
}

// The backward-flow mirror: records that an edge is complete and queues the node
// at its tail once it has nothing left outstanding. A Start node is never
// queued, for the same reason an End node is not queued going forwards.
fn release_predecessor<K: Key, R: Key, W: Key>(
    state: &VertexGraphState<K, R, W>,
    edge_id: K,
    pending_outgoing_edge_counts: &mut IndexMap<K, usize>,
    ready_nodes: &mut Vec<K>,
) {
    let dependency_node_id = state
        .edge_tail_node_id(edge_id)
        .expect("edge tail must exist");
    let pending = pending_outgoing_edge_counts
        .get_mut(&dependency_node_id)
        .expect("node with outgoing edges must have a pending count");
    *pending -= 1;

    if *pending == 0
        && state
            .node(dependency_node_id)
            .expect("node must exist")
            .node_type()
            != NodeType::Start
    {
        ready_nodes.push(dependency_node_id);
    }
}

// The aggregate helpers below return zero for an empty edge set, matching the
// `unwrap_or(0)` the Start and End node passes used; the main loops only ever
// call them for nodes that have at least one edge.
pub(crate) fn max_edge_earliest_finish_time<K: Key, R: Key, W: Key>(
    state: &VertexGraphState<K, R, W>,
    edge_ids: &InsertionOrderSet<K>,
) -> i32 {
    let mut found = false;
    let mut maximum = 0;
    for edge_id in edge_ids {
        let earliest_finish_time = state
            .edge(*edge_id)
            .expect("edge must exist")
            .content
            .earliest_finish_time
            .expect("completed edge must have EFT");
        if !found || earliest_finish_time > maximum {
            maximum = earliest_finish_time;
            found = true;
        }
    }
    maximum
}

pub(crate) fn min_edge_latest_finish_time<K: Key, R: Key, W: Key>(
    state: &VertexGraphState<K, R, W>,
    edge_ids: &InsertionOrderSet<K>,
) -> i32 {
    let mut found = false;
    let mut minimum = 0;
    for edge_id in edge_ids {
        let latest_finish_time = state
            .edge(*edge_id)
            .expect("edge must exist")
            .content
            .latest_finish_time
            .expect("completed edge must have LFT");
        if !found || latest_finish_time < minimum {
            minimum = latest_finish_time;
            found = true;
        }
    }
    minimum
}

pub(crate) fn min_successor_earliest_start_time<K: Key, R: Key, W: Key>(
    state: &VertexGraphState<K, R, W>,
    edge_ids: &InsertionOrderSet<K>,
) -> i32 {
    let mut found = false;
    let mut minimum = 0;
    for edge_id in edge_ids {
        let head_id = state
            .edge_head_node_id(*edge_id)
            .expect("edge head must exist");
        let earliest_start_time = state
            .node(head_id)
            .expect("node must exist")
            .content
            .earliest_start_time
            .expect("head node must have EST");
        if !found || earliest_start_time < minimum {
            minimum = earliest_start_time;
            found = true;
        }
    }
    minimum
}

fn all_edges_have_earliest_finish_time<K: Key, R: Key, W: Key>(
    state: &VertexGraphState<K, R, W>,
    edge_ids: &InsertionOrderSet<K>,
) -> bool {
    edge_ids.iter().all(|edge_id| {
        state
            .edge(*edge_id)
            .expect("edge must exist")
            .content
            .earliest_finish_time
            .is_some()
    })
}

fn all_edges_have_latest_finish_time<K: Key, R: Key, W: Key>(
    state: &VertexGraphState<K, R, W>,
    edge_ids: &InsertionOrderSet<K>,
) -> bool {
    edge_ids.iter().all(|edge_id| {
        state
            .edge(*edge_id)
            .expect("edge must exist")
            .content
            .latest_finish_time
            .is_some()
    })
}

// The value helpers below are the arithmetic of the two flows, factored out so that
// the full passes and the incremental recalculation in `incremental.rs` compute from
// one copy rather than two. Each reproduces its original statements verbatim.

/// The earliest start time of a node, given the value implied by its predecessors
/// (zero for Start and Isolated nodes). This depends on the duration as well as on
/// the predecessors, through the maximum-latest-finish clamp - so shortening an
/// activity can raise its own earliest start.
pub(crate) fn clamp_earliest_start_time<K: Key, R: Key, W: Key>(
    activity: &DependentActivity<K, R, W>,
    mut earliest_start_time: i32,
) -> i32 {
    if let Some(min_est) = activity.minimum_earliest_start_time {
        // Augment the earliest start time artificially (if required).
        if min_est > earliest_start_time {
            earliest_start_time = min_est;
        }
    }

    if let Some(max_lft) = activity.maximum_latest_finish_time {
        let proposed_latest_start_time = max_lft - activity.duration;
        // Diminish the earliest start time artificially (if required).
        if proposed_latest_start_time < earliest_start_time {
            earliest_start_time = proposed_latest_start_time;
        }
    }

    earliest_start_time
}

/// The finish time a node hands on: diminished by its maximum, or else augmented by
/// its minimum free slack. This is the value given to a Normal node's outgoing edges,
/// and also the latest finish time the forward pass gives End and Isolated nodes.
pub(crate) fn augmented_finish_time<K: Key, R: Key, W: Key>(
    activity: &DependentActivity<K, R, W>,
    mut finish_time: i32,
) -> i32 {
    if let Some(max_lft) = activity.maximum_latest_finish_time {
        // Diminish the finish time artificially (if required).
        if max_lft < finish_time {
            finish_time = max_lft;
        }
    } else if let Some(min_free_slack) = activity.minimum_free_slack {
        let proposed = finish_time + min_free_slack;
        // Augment the finish time artificially (if required).
        if proposed > finish_time {
            finish_time = proposed;
        }
    }

    finish_time
}

/// The Start-node variant of the above, which applies the minimum free slack without
/// the maximum diminishment. The asymmetry is deliberate and long-standing.
pub(crate) fn start_node_edge_earliest_finish_time<K: Key, R: Key, W: Key>(
    activity: &DependentActivity<K, R, W>,
    mut finish_time: i32,
) -> i32 {
    if let Some(min_free_slack) = activity.minimum_free_slack {
        let proposed = finish_time + min_free_slack;
        // Augment the finish time artificially (if required).
        if proposed > finish_time {
            finish_time = proposed;
        }
    }

    finish_time
}

/// A latest finish time diminished by the node's maximum.
pub(crate) fn clamp_latest_finish_time<K: Key, R: Key, W: Key>(
    activity: &DependentActivity<K, R, W>,
    mut latest_finish_time: i32,
) -> i32 {
    if let Some(max_lft) = activity.maximum_latest_finish_time {
        // Diminish the latest finish time artificially (if required).
        if max_lft < latest_finish_time {
            latest_finish_time = max_lft;
        }
    }

    latest_finish_time
}

/// Free slack for Start and Normal nodes, from the earliest start times of their
/// successors. Stays optional because the original was: a node with no earliest start
/// time takes no free slack rather than taking it from zero.
pub(crate) fn free_slack_from_successors<K: Key, R: Key, W: Key>(
    activity: &DependentActivity<K, R, W>,
    min_successor_earliest_start_time: i32,
) -> Option<i32> {
    let mut latest_finish_time = min_successor_earliest_start_time;

    if let Some(lft) = activity.latest_finish_time {
        // Diminish the latest finish time artificially (if required).
        if lft < latest_finish_time {
            latest_finish_time = lft;
        }
    }

    if let Some(max_lft) = activity.maximum_latest_finish_time {
        // Diminish the latest finish time artificially (if required).
        if max_lft < latest_finish_time {
            latest_finish_time = max_lft;
        }
    }

    // Free float/slack calculations.
    activity
        .earliest_start_time
        .map(|est| latest_finish_time - est - activity.duration)
}

/// The latest finish time an End node gives its incoming edges. The comparison treats
/// a node without a latest start time as starting at zero, which is not the same as
/// propagating the absent value.
pub(crate) fn end_node_incoming_edge_latest_finish_time<K: Key, R: Key, W: Key>(
    activity: &DependentActivity<K, R, W>,
) -> Option<i32> {
    let mut latest_finish_time = activity.latest_start_time();

    if let Some(max_lft) = activity.maximum_latest_finish_time {
        // Diminish the latest finish time artificially (if required).
        if max_lft < latest_finish_time.unwrap_or(0) {
            latest_finish_time = Some(max_lft);
        }
    }

    latest_finish_time
}

pub(crate) fn calculate_critical_path_forward_flow<K: Key, R: Key, W: Key>(
    state: &mut VertexGraphState<K, R, W>,
    invalid_constraints: &[InvalidConstraint<K>],
    shuffle_order: bool,
) -> Result<bool, GraphError> {
    if !invalid_constraints.is_empty() {
        return Ok(false);
    }

    // We can assume at this point that all the activity constraints are valid.
    //
    // An edge is complete exactly when the node at its tail has been processed,
    // so rather than tracking edge identity in sets, each node counts how many of
    // its incoming edges are still outstanding. Only nodes with incoming edges -
    // Normal and End nodes - need an entry. This is what the flow used to spend
    // two edge-sized sets per call to discover, and removing them matters more
    // here than it did in C#: retiring an edge meant an `IndexSet::shift_remove`,
    // which is linear in the size of the set.
    let mut pending_incoming_edge_counts: IndexMap<K, usize> =
        IndexMap::with_capacity(state.nodes.len());

    for node in state.nodes.values() {
        if matches!(node.node_type(), NodeType::Start | NodeType::Isolated) {
            continue;
        }
        pending_incoming_edge_counts.insert(node.id(), node.incoming.len());
    }

    // Counted down as edges are completed; anything left at the end means the
    // walk could not reach every edge, which only a cycle can cause.
    let mut outstanding_edge_count = state.edges.len();
    let mut ready_nodes: Vec<K> = Vec::new();
    // Reused across nodes so the walk does not allocate per visit.
    let mut edge_scratch: Vec<K> = Vec::new();

    // First complete the Isolated nodes.
    for node_id in state.nodes_of_type(NodeType::Isolated) {
        let content = &mut state
            .node_mut(node_id)
            .expect("isolated node must exist")
            .content;

        // Earliest Start Time.
        content.earliest_start_time = Some(clamp_earliest_start_time(content, 0));

        // Latest Finish Time.
        let earliest_finish_time = content
            .earliest_finish_time()
            .expect("EFT follows from EST");
        content.latest_finish_time = Some(augmented_finish_time(content, earliest_finish_time));
    }

    // Complete the Start nodes first to ensure the completed edge IDs contains something.
    for node_id in state.nodes_of_type(NodeType::Start) {
        let content = &mut state
            .node_mut(node_id)
            .expect("start node must exist")
            .content;

        content.earliest_start_time = Some(clamp_earliest_start_time(content, 0));

        let node_eft = content
            .earliest_finish_time()
            .expect("EFT follows from EST");
        let earliest_finish_time = start_node_edge_earliest_finish_time(content, node_eft);

        edge_scratch.clear();
        edge_scratch.extend(
            state
                .node(node_id)
                .expect("start node must exist")
                .outgoing
                .iter()
                .copied(),
        );

        for outgoing_edge_id in edge_scratch.iter().copied() {
            state
                .edge_mut(outgoing_edge_id)
                .expect("outgoing edge must exist")
                .content
                .earliest_finish_time = Some(earliest_finish_time);
            outstanding_edge_count -= 1;
            release_successor(
                state,
                outgoing_edge_id,
                &mut pending_incoming_edge_counts,
                &mut ready_nodes,
            );
        }
    }

    // Forward flow algorithm.
    //
    // An edge can be completed once every incoming edge of its tail node is
    // complete, and the value it receives depends only on that tail node - so
    // every outgoing edge of a node is given the same earliest finish time. That
    // makes this a walk over nodes in dependency order rather than a search over
    // edges: a node is processed once all of its predecessors are done, and it
    // then completes all of its outgoing edges at once.
    //
    // The previous form swept the entire remaining edge set repeatedly, keeping
    // whichever edges had become ready, which cost O(depth x E). Visiting each
    // node once as it becomes ready costs O(V + E) and produces identical values,
    // because an edge is still only completed after exactly the same predecessors
    // are.
    //
    // Nodes are handled in rounds, and the shuffle hook shuffles each round.
    // Within a round the order genuinely cannot matter - that is what
    // `shuffle_processing_order` exists to demonstrate.
    //
    // The first round was seeded by the Start node pass above: every node whose
    // only unfinished dependency was a Start node is now ready. Nothing else can
    // be ready to begin with, because any other node has an incoming edge from a
    // node that has yet to be processed.
    let mut next_ready_nodes: Vec<K> = Vec::new();
    while !ready_nodes.is_empty() {
        if shuffle_order {
            shuffle(&mut ready_nodes);
        }

        next_ready_nodes.clear();

        for dependency_node_id in ready_nodes.iter().copied() {
            let dependency_node = state.node(dependency_node_id).expect("node must exist");

            if dependency_node.content.earliest_start_time.is_none() {
                let base = max_edge_earliest_finish_time(state, &dependency_node.incoming);

                let content = &mut state
                    .node_mut(dependency_node_id)
                    .expect("node must exist")
                    .content;
                content.earliest_start_time = Some(clamp_earliest_start_time(content, base));
            }

            let dependency_content = &state
                .node(dependency_node_id)
                .expect("node must exist")
                .content;
            let node_eft = dependency_content
                .earliest_finish_time()
                .expect("EFT follows from EST");
            let earliest_finish_time = augmented_finish_time(dependency_content, node_eft);

            // A node is only ever processed once, and only Start nodes had their
            // outgoing edges completed beforehand, so every outgoing edge here is
            // still outstanding.
            edge_scratch.clear();
            edge_scratch.extend(
                state
                    .node(dependency_node_id)
                    .expect("node must exist")
                    .outgoing
                    .iter()
                    .copied(),
            );

            for edge_id in edge_scratch.iter().copied() {
                state
                    .edge_mut(edge_id)
                    .expect("edge must exist")
                    .content
                    .earliest_finish_time = Some(earliest_finish_time);
                outstanding_edge_count -= 1;
                release_successor(
                    state,
                    edge_id,
                    &mut pending_incoming_edge_counts,
                    &mut next_ready_nodes,
                );
            }
        }

        std::mem::swap(&mut ready_nodes, &mut next_ready_nodes);
    }

    // If edges are still outstanding once nothing more can become ready, then a
    // cycle must exist in the graph and we will not be able to calculate the
    // earliest finish times.
    if outstanding_edge_count != 0 {
        return Err(GraphError::new(
            messages::MSG_CANNOT_CALCULATE_EARLIEST_FINISH_TIMES_DUE_TO_CYCLIC_DEPENDENCY,
        ));
    }

    // Now complete the End nodes.
    for node_id in state.nodes_of_type(NodeType::End) {
        let node = state.node(node_id).expect("end node must exist");
        // An edge is complete precisely when it has been given an earliest finish
        // time, and the pass above clears these before it starts, so this is the
        // same check the flow used to make against its set of completed edges.
        if !all_edges_have_earliest_finish_time(state, &node.incoming) {
            return Err(GraphError::new(format!(
                "Cannot calculate EST for activity {node_id} as not all dependency events have EFT values."
            )));
        }

        let node = state.node(node_id).expect("end node must exist");
        if node.content.earliest_start_time.is_none() {
            let base = max_edge_earliest_finish_time(state, &node.incoming);

            let content = &mut state.node_mut(node_id).expect("node must exist").content;
            content.earliest_start_time = Some(clamp_earliest_start_time(content, base));
        }

        let content = &mut state.node_mut(node_id).expect("node must exist").content;
        if content.latest_finish_time.is_none() {
            let earliest_finish_time = content
                .earliest_finish_time()
                .expect("EFT follows from EST");
            content.latest_finish_time = Some(augmented_finish_time(content, earliest_finish_time));
        }
    }

    Ok(true)
}

pub(crate) fn calculate_critical_path_backward_flow<K: Key, R: Key, W: Key>(
    state: &mut VertexGraphState<K, R, W>,
    invalid_constraints: &[InvalidConstraint<K>],
    shuffle_order: bool,
) -> Result<bool, GraphError> {
    if !invalid_constraints.is_empty() {
        return Ok(false);
    }

    // Only perform if all events have earliest finish times.
    if !state
        .edges
        .values()
        .all(|e| e.content.earliest_finish_time.is_some())
    {
        return Ok(false);
    }

    // Only perform if all activities have earliest finish times.
    if !state
        .nodes
        .values()
        .all(|n| n.content.earliest_finish_time().is_some())
    {
        return Ok(false);
    }

    // Snapshot these before potentially modifying them.
    let end_node_ids = state.nodes_of_type(NodeType::End);
    let isolated_node_ids = state.nodes_of_type(NodeType::Isolated);
    let start_node_ids = state.nodes_of_type(NodeType::Start);

    // Only perform if all end nodes have latest finish times.
    if !end_node_ids.iter().all(|id| {
        state
            .node(*id)
            .expect("node must exist")
            .content
            .latest_finish_time
            .is_some()
    }) {
        return Ok(false);
    }

    // We can assume at this point that all the activity constraints are valid.
    // As in the forward flow, an edge is complete exactly when the node at its
    // head has been processed, so outstanding edges are counted per node rather
    // than tracked by identity. Only nodes with outgoing edges - Start and Normal
    // nodes - need an entry.
    let mut pending_outgoing_edge_counts: IndexMap<K, usize> =
        IndexMap::with_capacity(state.nodes.len());

    for node in state.nodes.values() {
        if matches!(node.node_type(), NodeType::End | NodeType::Isolated) {
            continue;
        }
        pending_outgoing_edge_counts.insert(node.id(), node.outgoing.len());
    }

    let mut outstanding_edge_count = state.edges.len();
    let mut ready_nodes: Vec<K> = Vec::new();
    // Reused across nodes so the walk does not allocate per visit.
    let mut edge_scratch: Vec<K> = Vec::new();

    let end_nodes_end_time = end_node_ids
        .iter()
        .map(|id| {
            state
                .node(*id)
                .expect("node must exist")
                .content
                .latest_finish_time
                .expect("end node must have LFT")
        })
        .max()
        .unwrap_or(0);
    let isolated_nodes_end_time = isolated_node_ids
        .iter()
        .filter_map(|id| {
            state
                .node(*id)
                .expect("node must exist")
                .content
                .latest_finish_time
        })
        .max()
        .unwrap_or(0);
    let end_time = end_nodes_end_time.max(isolated_nodes_end_time);

    // Complete the End nodes first.
    for node_id in &end_node_ids {
        let node_id = *node_id;
        {
            let content = &mut state.node_mut(node_id).expect("node must exist").content;

            // Latest Finish Time.
            content.latest_finish_time = Some(clamp_latest_finish_time(content, end_time));

            // Free float/slack calculations.
            content.free_slack = match (content.latest_finish_time, content.earliest_finish_time())
            {
                (Some(lft), Some(eft)) => Some(lft - eft),
                _ => None,
            };
        }

        let edge_latest_finish_time = {
            let node = state.node(node_id).expect("node must exist");
            edge_scratch.clear();
            edge_scratch.extend(node.incoming.iter().copied());
            end_node_incoming_edge_latest_finish_time(&node.content)
        };

        for incoming_edge_id in edge_scratch.iter().copied() {
            let latest_finish_time = edge_latest_finish_time;

            state
                .edge_mut(incoming_edge_id)
                .expect("edge must exist")
                .content
                .latest_finish_time = latest_finish_time;
            outstanding_edge_count -= 1;
            release_predecessor(
                state,
                incoming_edge_id,
                &mut pending_outgoing_edge_counts,
                &mut ready_nodes,
            );
        }
    }

    // Backward flow algorithm.
    //
    // The mirror image of the forward flow: an edge can be completed once every
    // outgoing edge of its head node is complete, and the value it receives
    // depends only on that head node, so every incoming edge of a node is given
    // the same latest finish time. The same reasoning therefore applies - this
    // walks nodes in reverse dependency order, visiting each once, instead of
    // sweeping the remaining edge set repeatedly. The first round was seeded by
    // the End node pass above.
    let mut next_ready_nodes: Vec<K> = Vec::new();
    while !ready_nodes.is_empty() {
        if shuffle_order {
            shuffle(&mut ready_nodes);
        }

        next_ready_nodes.clear();

        for successor_node_id in ready_nodes.iter().copied() {
            let successor_node = state.node(successor_node_id).expect("node must exist");

            if successor_node.content.latest_finish_time.is_none() {
                let base = min_edge_latest_finish_time(state, &successor_node.outgoing);

                let content = &mut state
                    .node_mut(successor_node_id)
                    .expect("node must exist")
                    .content;
                content.latest_finish_time = Some(clamp_latest_finish_time(content, base));
            }

            let successor_node = state.node(successor_node_id).expect("node must exist");
            if successor_node.content.free_slack.is_none() {
                let base = min_successor_earliest_start_time(state, &successor_node.outgoing);

                let content = &mut state
                    .node_mut(successor_node_id)
                    .expect("node must exist")
                    .content;
                content.free_slack = free_slack_from_successors(content, base);
            }

            let successor_lst = state
                .node(successor_node_id)
                .expect("node must exist")
                .content
                .latest_start_time();

            // Only End nodes had their incoming edges completed beforehand, and a
            // node is processed once, so every incoming edge here is still
            // outstanding.
            edge_scratch.clear();
            edge_scratch.extend(
                state
                    .node(successor_node_id)
                    .expect("node must exist")
                    .incoming
                    .iter()
                    .copied(),
            );

            for edge_id in edge_scratch.iter().copied() {
                state
                    .edge_mut(edge_id)
                    .expect("edge must exist")
                    .content
                    .latest_finish_time = successor_lst;
                outstanding_edge_count -= 1;
                release_predecessor(
                    state,
                    edge_id,
                    &mut pending_outgoing_edge_counts,
                    &mut next_ready_nodes,
                );
            }
        }

        std::mem::swap(&mut ready_nodes, &mut next_ready_nodes);
    }

    // If edges are still outstanding once nothing more can become ready, then a
    // cycle must exist in the graph and we will not be able to calculate the
    // latest finish times.
    if outstanding_edge_count != 0 {
        return Err(GraphError::new(
            messages::MSG_CANNOT_CALCULATE_LATEST_FINISH_TIMES_DUE_TO_CYCLIC_DEPENDENCY,
        ));
    }

    // Now complete the Start nodes.
    for node_id in start_node_ids {
        let node = state.node(node_id).expect("node must exist");
        // An edge is complete precisely when it has been given a latest finish
        // time, which is the same check the flow used to make against its set of
        // completed edges.
        if !all_edges_have_latest_finish_time(state, &node.outgoing) {
            return Err(GraphError::new(format!(
                "Cannot calculate LFT for activity {node_id} as not all dependency events have LFT values."
            )));
        }

        let node = state.node(node_id).expect("node must exist");
        if node.content.latest_finish_time.is_none() {
            let base = min_edge_latest_finish_time(state, &node.outgoing);

            let content = &mut state.node_mut(node_id).expect("node must exist").content;
            content.latest_finish_time = Some(clamp_latest_finish_time(content, base));
        }

        let node = state.node(node_id).expect("node must exist");
        if node.content.free_slack.is_none() {
            let base = min_successor_earliest_start_time(state, &node.outgoing);

            let content = &mut state.node_mut(node_id).expect("node must exist").content;
            content.free_slack = free_slack_from_successors(content, base);
        }
    }

    // At this point, the Isolated Nodes will not have finish times or free
    // slack values. That needs to be done after all critical paths have been
    // calculated, otherwise it will screw up the priority list calculations.
    Ok(true)
}

pub(crate) fn back_fill_isolated_nodes<K: Key, R: Key, W: Key>(
    state: &mut VertexGraphState<K, R, W>,
    invalid_constraints: &[InvalidConstraint<K>],
) -> bool {
    if !invalid_constraints.is_empty() {
        return false;
    }

    let end_node_ids = state.nodes_of_type(NodeType::End);
    let isolated_node_ids = state.nodes_of_type(NodeType::Isolated);

    // Only perform if all end nodes have latest finish times.
    if !end_node_ids.iter().all(|id| {
        state
            .node(*id)
            .expect("node must exist")
            .content
            .latest_finish_time
            .is_some()
    }) {
        return false;
    }

    let end_nodes_end_time = end_node_ids
        .iter()
        .map(|id| {
            state
                .node(*id)
                .expect("node must exist")
                .content
                .latest_finish_time
                .expect("end node must have LFT")
        })
        .max()
        .unwrap_or(0);
    let isolated_nodes_end_time = isolated_node_ids
        .iter()
        .filter_map(|id| {
            state
                .node(*id)
                .expect("node must exist")
                .content
                .latest_finish_time
        })
        .max()
        .unwrap_or(0);
    let end_time = end_nodes_end_time.max(isolated_nodes_end_time);

    // Now backfill the Isolated Nodes.
    for node_id in isolated_node_ids {
        let content = &mut state.node_mut(node_id).expect("node must exist").content;

        // Latest Finish Time.
        let mut latest_finish_time = end_time;

        if let Some(max_lft) = content.maximum_latest_finish_time {
            // Diminish the latest finish time artificially (if required).
            if max_lft < latest_finish_time {
                latest_finish_time = max_lft;
            }
        }

        content.latest_finish_time = Some(latest_finish_time);

        // Free float/slack calculations.
        content.free_slack = match (content.latest_finish_time, content.earliest_finish_time()) {
            (Some(lft), Some(eft)) => Some(lft - eft),
            _ => None,
        };
    }

    true
}
