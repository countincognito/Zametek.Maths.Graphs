use super::state::ArrowGraphState;
use crate::messages;
use crate::shuffle::shuffle;
use indexmap::IndexMap;
use zametek_maths_graphs_primitives::{GraphError, InvalidConstraint, Key, NodeType};

// The critical-path engine for Activity-on-Arrow graphs - the counterpart of
// the C# `ArrowCriticalPathEngine`: forward pass (earliest event finish
// times), backward pass (latest event finish times), and per-activity
// critical-path variables.

// Records that a forward-flow edge is complete, and queues the event at its head
// once that was the last incoming edge it was waiting on.
fn release_successor_event<K: Key, R: Key, W: Key>(
    state: &ArrowGraphState<K, R, W>,
    edge_id: K,
    pending_incoming_edge_counts: &mut IndexMap<K, usize>,
    ready_nodes: &mut Vec<K>,
) {
    let head_id = state
        .edge_head_node_id(edge_id)
        .expect("edge head must exist");
    let pending = pending_incoming_edge_counts
        .get_mut(&head_id)
        .expect("event with incoming edges must have a pending count");
    *pending -= 1;

    if *pending == 0 {
        ready_nodes.push(head_id);
    }
}

// The backward-flow mirror: queues the event at an edge's tail once it has no
// outgoing edges left outstanding.
fn release_predecessor_event<K: Key, R: Key, W: Key>(
    state: &ArrowGraphState<K, R, W>,
    edge_id: K,
    pending_outgoing_edge_counts: &mut IndexMap<K, usize>,
    ready_nodes: &mut Vec<K>,
) {
    let tail_id = state
        .edge_tail_node_id(edge_id)
        .expect("edge tail must exist");
    let pending = pending_outgoing_edge_counts
        .get_mut(&tail_id)
        .expect("event with outgoing edges must have a pending count");
    *pending -= 1;

    if *pending == 0 {
        ready_nodes.push(tail_id);
    }
}

pub(crate) fn calculate_event_earliest_finish_times<K: Key, R: Key, W: Key>(
    state: &mut ArrowGraphState<K, R, W>,
    invalid_constraints: &[InvalidConstraint<K>],
    shuffle_order: bool,
) -> Result<bool, GraphError> {
    let Some(start_node_id) = state.start_node_id else {
        return Err(GraphError::new("Arrow graph state has no Start node"));
    };
    let Some(end_node_id) = state.end_node_id else {
        return Err(GraphError::new("Arrow graph state has no End node"));
    };

    if !invalid_constraints.is_empty() {
        return Ok(false);
    }

    // An event can be given its earliest finish time once every event it depends
    // on - the tail of each of its incoming edges - has been given one. Rather
    // than sweeping the remaining events repeatedly to find which have become
    // ready, each event counts how many incoming edges it is still waiting on and
    // is queued the moment that count reaches zero. Only the Start node is
    // excluded: it depends on nothing and is completed below.
    //
    // Removing the remaining-set bookkeeping matters more here than it did in C#,
    // because retiring an event meant an `IndexSet::shift_remove`, which is linear
    // in the size of the set.
    let mut pending_incoming_edge_counts: IndexMap<K, usize> =
        IndexMap::with_capacity(state.nodes.len());
    let mut start_node_present = false;

    for node in state.nodes.values() {
        if node.id() == start_node_id {
            start_node_present = true;
            continue;
        }
        pending_incoming_edge_counts.insert(node.id(), node.incoming.len());
    }

    // Make sure the graph contains the Start node.
    if !start_node_present {
        return Ok(false);
    }

    let mut ready_nodes: Vec<K> = Vec::new();

    // An event with no incoming edges at all is ready from the outset, and nothing
    // will ever release an edge into it to say so. The sweep this replaces reached
    // such an event on its first pass, so it is queued here to match.
    for (node_id, pending) in &pending_incoming_edge_counts {
        if *pending == 0 {
            ready_nodes.push(*node_id);
        }
    }

    // Complete the Start node first, which seeds the first round.
    state
        .node_mut(start_node_id)
        .expect("start node must exist")
        .content
        .earliest_finish_time = Some(0);
    let mut completed_node_count = 1;

    let start_outgoing: Vec<K> = state
        .node(start_node_id)
        .expect("start node must exist")
        .outgoing
        .iter()
        .copied()
        .collect();
    for outgoing_edge_id in start_outgoing {
        release_successor_event(
            state,
            outgoing_edge_id,
            &mut pending_incoming_edge_counts,
            &mut ready_nodes,
        );
    }

    // Forward flow algorithm.
    //
    // This walks the events in dependency order, visiting each once, instead of
    // sweeping the remaining set repeatedly at O(depth x V) per pass. The value an
    // event receives is still aggregated over all of its incoming edges, exactly
    // as before, and an event is still only given a value once every event it
    // depends on has one - so the numbers are unchanged.
    let mut next_ready_nodes: Vec<K> = Vec::new();
    while !ready_nodes.is_empty() {
        if shuffle_order {
            shuffle(&mut ready_nodes);
        }

        next_ready_nodes.clear();

        for node_id in ready_nodes.iter().copied() {
            let node = state.node(node_id).expect("node must exist");

            // Get the incoming edges and the dependency node IDs.
            let mut incoming_edges: Vec<K> = node.incoming.iter().copied().collect();

            if shuffle_order {
                shuffle(&mut incoming_edges);
            }

            let mut earliest_finish_time = 0;

            for incoming_edge_id in &incoming_edges {
                let incoming_edge = state.edge(*incoming_edge_id).expect("edge must exist");
                let tail_id = state
                    .edge_tail_node_id(*incoming_edge_id)
                    .expect("edge tail must exist");
                let tail_node = state.node(tail_id).expect("tail node must exist");

                if let Some(tail_eft) = tail_node.content.earliest_finish_time {
                    let mut proposed = tail_eft + incoming_edge.content.duration;
                    proposed += incoming_edge.content.minimum_free_slack.unwrap_or(0);
                    // Augment the earliest finish time artificially (if required).
                    if proposed > earliest_finish_time {
                        earliest_finish_time = proposed;
                    }
                }

                if let Some(min_est) = incoming_edge.content.minimum_earliest_start_time {
                    let proposed = min_est + incoming_edge.content.duration;
                    // Augment the earliest finish time artificially (if required).
                    if proposed > earliest_finish_time {
                        earliest_finish_time = proposed;
                    }
                }

                // It is only necessary to check the Maximum LFT if the head
                // node is not the End node, and if the tail node is not the
                // Start node. Otherwise, it ends up imposing an LFT value that
                // is unnecessarily constrained without any good reason.
                if node_id != end_node_id && tail_id != start_node_id {
                    if let Some(max_lft) = incoming_edge.content.maximum_latest_finish_time {
                        // Diminish the earliest finish time artificially (if required).
                        if max_lft < earliest_finish_time {
                            earliest_finish_time = max_lft;
                        }
                    }
                }
            }

            state
                .node_mut(node_id)
                .expect("node must exist")
                .content
                .earliest_finish_time = Some(earliest_finish_time);
            completed_node_count += 1;

            // An End node has no outgoing edges to carry the flow onwards.
            if state.node(node_id).expect("node must exist").node_type() != NodeType::End {
                let outgoing: Vec<K> = state
                    .node(node_id)
                    .expect("node must exist")
                    .outgoing
                    .iter()
                    .copied()
                    .collect();
                for outgoing_edge_id in outgoing {
                    release_successor_event(
                        state,
                        outgoing_edge_id,
                        &mut pending_incoming_edge_counts,
                        &mut next_ready_nodes,
                    );
                }
            }
        }

        std::mem::swap(&mut ready_nodes, &mut next_ready_nodes);
    }

    // If some events were never reached once nothing more can become ready, then
    // a cycle must exist in the graph and we will not be able to calculate the
    // earliest finish times.
    if completed_node_count != state.nodes.len() {
        return Err(GraphError::new(
            messages::MSG_CANNOT_CALCULATE_EARLIEST_FINISH_TIMES_DUE_TO_CYCLIC_DEPENDENCY,
        ));
    }
    Ok(true)
}

pub(crate) fn calculate_event_latest_finish_times<K: Key, R: Key, W: Key>(
    state: &mut ArrowGraphState<K, R, W>,
    invalid_constraints: &[InvalidConstraint<K>],
    shuffle_order: bool,
) -> Result<bool, GraphError> {
    let Some(end_node_id) = state.end_node_id else {
        return Err(GraphError::new("Arrow graph state has no End node"));
    };

    if !invalid_constraints.is_empty() {
        return Ok(false);
    }

    // Only perform if all events have earliest finish times.
    if !state
        .nodes
        .values()
        .all(|n| n.content.earliest_finish_time.is_some())
    {
        return Ok(false);
    }

    // The mirror image of the forward flow: an event can be given its latest
    // finish time once every event that depends on it - the head of each of its
    // outgoing edges - has been given one. Only the End node is excluded; nothing
    // depends on it and it is completed below.
    let mut pending_outgoing_edge_counts: IndexMap<K, usize> =
        IndexMap::with_capacity(state.nodes.len());
    let mut end_node_present = false;

    for node in state.nodes.values() {
        if node.id() == end_node_id {
            end_node_present = true;
            continue;
        }
        pending_outgoing_edge_counts.insert(node.id(), node.outgoing.len());
    }

    // Make sure the graph contains the End node.
    if !end_node_present {
        return Ok(false);
    }

    // Complete the End node first, which seeds the first round.
    let end_node_eft = state
        .node(end_node_id)
        .expect("end node must exist")
        .content
        .earliest_finish_time;
    state
        .node_mut(end_node_id)
        .expect("end node must exist")
        .content
        .latest_finish_time = end_node_eft;

    let Some(end_node_latest_finish_time) = end_node_eft else {
        return Ok(false);
    };

    let mut completed_node_count = 1;
    let mut ready_nodes: Vec<K> = Vec::new();

    // An event that nothing depends on is ready from the outset, for the same
    // reason its forward-flow counterpart is.
    for (node_id, pending) in &pending_outgoing_edge_counts {
        if *pending == 0 {
            ready_nodes.push(*node_id);
        }
    }

    let end_incoming: Vec<K> = state
        .node(end_node_id)
        .expect("end node must exist")
        .incoming
        .iter()
        .copied()
        .collect();
    for incoming_edge_id in end_incoming {
        release_predecessor_event(
            state,
            incoming_edge_id,
            &mut pending_outgoing_edge_counts,
            &mut ready_nodes,
        );
    }

    // Backward flow algorithm - the reverse-order walk matching the forward flow
    // above, visiting each event once rather than sweeping the remaining set.
    let mut next_ready_nodes: Vec<K> = Vec::new();
    while !ready_nodes.is_empty() {
        if shuffle_order {
            shuffle(&mut ready_nodes);
        }

        next_ready_nodes.clear();

        for node_id in ready_nodes.iter().copied() {
            let node = state.node(node_id).expect("node must exist");

            // Get the outgoing edges and the successor node IDs.
            let mut outgoing_edges: Vec<K> = node.outgoing.iter().copied().collect();

            if shuffle_order {
                shuffle(&mut outgoing_edges);
            }

            let mut latest_finish_time = end_node_latest_finish_time;

            for outgoing_edge_id in &outgoing_edges {
                let outgoing_edge = state.edge(*outgoing_edge_id).expect("edge must exist");
                let head_id = state
                    .edge_head_node_id(*outgoing_edge_id)
                    .expect("edge head must exist");
                let head_node = state.node(head_id).expect("head node must exist");

                if let Some(head_lft) = head_node.content.latest_finish_time {
                    let proposed = head_lft - outgoing_edge.content.duration;
                    // Diminish the latest finish time artificially (if required).
                    if proposed < latest_finish_time {
                        latest_finish_time = proposed;
                    }
                }

                if let Some(max_lft) = outgoing_edge.content.maximum_latest_finish_time {
                    let proposed = max_lft - outgoing_edge.content.duration;
                    // Diminish the latest finish time artificially (if required).
                    if proposed < latest_finish_time {
                        latest_finish_time = proposed;
                    }
                }
            }

            state
                .node_mut(node_id)
                .expect("node must exist")
                .content
                .latest_finish_time = Some(latest_finish_time);
            completed_node_count += 1;

            // A Start node has no incoming edges to carry the flow backwards.
            if state.node(node_id).expect("node must exist").node_type() != NodeType::Start {
                let incoming: Vec<K> = state
                    .node(node_id)
                    .expect("node must exist")
                    .incoming
                    .iter()
                    .copied()
                    .collect();
                for incoming_edge_id in incoming {
                    release_predecessor_event(
                        state,
                        incoming_edge_id,
                        &mut pending_outgoing_edge_counts,
                        &mut next_ready_nodes,
                    );
                }
            }
        }

        std::mem::swap(&mut ready_nodes, &mut next_ready_nodes);
    }

    // If some events were never reached once nothing more can become ready, then
    // a cycle must exist in the graph and we will not be able to calculate the
    // latest finish times.
    if completed_node_count != state.nodes.len() {
        return Err(GraphError::new(
            messages::MSG_CANNOT_CALCULATE_LATEST_FINISH_TIMES_DUE_TO_CYCLIC_DEPENDENCY,
        ));
    }
    Ok(true)
}

pub(crate) fn calculate_critical_path_variables<K: Key, R: Key, W: Key>(
    state: &mut ArrowGraphState<K, R, W>,
    invalid_constraints: &[InvalidConstraint<K>],
) -> Result<bool, GraphError> {
    if !invalid_constraints.is_empty() {
        return Ok(false);
    }

    // Only perform if all events have earliest finish times.
    if !state
        .nodes
        .values()
        .all(|n| n.content.earliest_finish_time.is_some())
    {
        return Ok(false);
    }

    // Only perform if all events have latest finish times.
    if !state
        .nodes
        .values()
        .all(|n| n.content.latest_finish_time.is_some())
    {
        return Ok(false);
    }

    // We can assume at this point that all the activity constraints are valid.

    // Earliest Start Times and Latest Finish Times.
    for edge_id in state.edge_ids() {
        let edge = state.edge(edge_id).expect("edge must exist");
        let tail_id = state
            .edge_tail_node_id(edge_id)
            .expect("edge tail must exist");
        let head_id = state
            .edge_head_node_id(edge_id)
            .expect("edge head must exist");

        let mut earliest_start_time = state
            .node(tail_id)
            .expect("tail node must exist")
            .content
            .earliest_finish_time;

        // Note: the C# comparisons here are lifted nullable comparisons, which
        // are false when the current value is null - so a null value is never
        // replaced.
        if let Some(min_est) = edge.content.minimum_earliest_start_time {
            // Augment the earliest start time artificially (if required).
            if earliest_start_time.is_some_and(|est| min_est > est) {
                earliest_start_time = Some(min_est);
            }
        }

        if let Some(max_lft) = edge.content.maximum_latest_finish_time {
            let proposed_latest_start_time = max_lft - edge.content.duration;
            // Diminish the earliest start time artificially (if required).
            if earliest_start_time.is_some_and(|est| proposed_latest_start_time < est) {
                earliest_start_time = Some(proposed_latest_start_time);
            }
        }

        let mut latest_finish_time = state
            .node(head_id)
            .expect("head node must exist")
            .content
            .latest_finish_time;

        if let Some(max_lft) = edge.content.maximum_latest_finish_time {
            // Diminish the latest finish time artificially (if required).
            if latest_finish_time.is_some_and(|lft| max_lft < lft) {
                latest_finish_time = Some(max_lft);
            }
        }

        let content = &mut state.edge_mut(edge_id).expect("edge must exist").content;
        content.earliest_start_time = earliest_start_time;
        content.latest_finish_time = latest_finish_time;
    }

    // Free float/slack calculations.
    for edge_id in state.edge_ids() {
        let edge = state.edge(edge_id).expect("edge must exist");
        let head_id = state
            .edge_head_node_id(edge_id)
            .expect("edge head must exist");
        let head_node = state.node(head_id).expect("head node must exist");

        if head_node.node_type() == NodeType::End {
            if let (Some(head_eft), Some(edge_eft)) = (
                head_node.content.earliest_finish_time,
                edge.content.earliest_finish_time(),
            ) {
                let mut free_slack = head_eft - edge_eft;

                if let Some(max_lft) = edge.content.maximum_latest_finish_time {
                    let proposed_free_slack = max_lft - edge_eft;
                    // Diminish the free slack artificially (if required).
                    if proposed_free_slack < free_slack {
                        free_slack = proposed_free_slack;
                    }
                }

                state
                    .edge_mut(edge_id)
                    .expect("edge must exist")
                    .content
                    .free_slack = Some(free_slack);
            }

            continue;
        }

        let outgoing_edges: Vec<K> = head_node.outgoing.iter().copied().collect();
        let mut min_earliest_start_time_of_outgoing_edges =
            head_node.content.latest_finish_time.unwrap_or(0);

        for outgoing_edge_id in outgoing_edges {
            let outgoing_edge = state.edge(outgoing_edge_id).expect("edge must exist");

            if let Some(proposed_earliest_start_time) = outgoing_edge.content.earliest_start_time {
                if proposed_earliest_start_time < min_earliest_start_time_of_outgoing_edges {
                    min_earliest_start_time_of_outgoing_edges = proposed_earliest_start_time;
                }
            }

            if let Some(max_lft) = outgoing_edge.content.maximum_latest_finish_time {
                let proposed_latest_start_time = max_lft - outgoing_edge.content.duration;
                if proposed_latest_start_time < min_earliest_start_time_of_outgoing_edges {
                    min_earliest_start_time_of_outgoing_edges = proposed_latest_start_time;
                }
            }
        }

        let edge = state.edge(edge_id).expect("edge must exist");
        if let Some(edge_eft) = edge.content.earliest_finish_time() {
            let mut free_slack = min_earliest_start_time_of_outgoing_edges - edge_eft;

            if let Some(max_lft) = edge.content.maximum_latest_finish_time {
                let proposed_free_slack = max_lft - edge_eft;
                // Diminish the free slack artificially (if required).
                if proposed_free_slack < free_slack {
                    free_slack = proposed_free_slack;
                }
            }

            state
                .edge_mut(edge_id)
                .expect("edge must exist")
                .content
                .free_slack = Some(free_slack);
        }
    }
    Ok(true)
}
