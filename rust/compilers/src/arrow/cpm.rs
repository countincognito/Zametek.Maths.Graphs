use super::state::ArrowState;
use crate::messages;
use crate::shuffle::shuffle;
use indexmap::IndexSet;
use zametek_maths_graphs_primitives::{GraphError, InvalidConstraint, Key, NodeType};

// The critical-path engine for Activity-on-Arrow graphs — the counterpart of
// the C# `ArrowCriticalPathEngine`: forward pass (earliest event finish
// times), backward pass (latest event finish times), and per-activity
// critical-path variables.

pub(crate) fn calculate_event_earliest_finish_times<K: Key, R: Key, W: Key>(
    state: &mut ArrowState<K, R, W>,
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

    let mut completed_node_ids: IndexSet<K> = IndexSet::new();
    let mut remaining_node_ids: IndexSet<K> = state.node_ids().into_iter().collect();

    // Make sure the remaining node IDs contain the Start node.
    if !remaining_node_ids.contains(&start_node_id) {
        return Ok(false);
    }

    // Complete the Start node first to ensure the completed node IDs contains something.
    state
        .node_mut(start_node_id)
        .expect("start node must exist")
        .content
        .earliest_finish_time = Some(0);
    completed_node_ids.insert(start_node_id);
    remaining_node_ids.shift_remove(&start_node_id);

    // Forward flow algorithm.
    while !remaining_node_ids.is_empty() {
        let mut progress = false;
        let mut remaining_list: Vec<K> = remaining_node_ids.iter().copied().collect();

        if shuffle_order {
            shuffle(&mut remaining_list);
        }

        for node_id in remaining_list {
            let node = state.node(node_id).expect("node must exist");

            // Get the incoming edges and the dependency node IDs.
            let mut incoming_edges: Vec<K> = node.incoming.iter().copied().collect();

            if shuffle_order {
                shuffle(&mut incoming_edges);
            }

            // If calculations for all the dependency nodes (the incoming edges'
            // tail nodes) have been completed, then use them to complete the
            // calculations for this node.
            let all_dependency_nodes_completed = incoming_edges.iter().all(|edge_id| {
                let tail_id = state
                    .edge_tail_node_id(*edge_id)
                    .expect("edge tail must exist");
                completed_node_ids.contains(&tail_id)
            });

            if !all_dependency_nodes_completed {
                continue;
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
            completed_node_ids.insert(node_id);
            remaining_node_ids.shift_remove(&node_id);
            // Note we are making progress.
            progress = true;
        }

        // If we have not made any progress then a cycle must exist in the
        // graph and we will not be able to calculate the earliest finish times.
        if !progress {
            return Err(GraphError::new(
                messages::MSG_CANNOT_CALCULATE_EARLIEST_FINISH_TIMES_DUE_TO_CYCLIC_DEPENDENCY,
            ));
        }
    }
    Ok(true)
}

pub(crate) fn calculate_event_latest_finish_times<K: Key, R: Key, W: Key>(
    state: &mut ArrowState<K, R, W>,
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

    let mut completed_node_ids: IndexSet<K> = IndexSet::new();
    let mut remaining_node_ids: IndexSet<K> = state.node_ids().into_iter().collect();

    // Make sure the remaining node IDs contain the End node.
    if !remaining_node_ids.contains(&end_node_id) {
        return Ok(false);
    }

    // Complete the End node first to ensure the completed node IDs contains something.
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

    completed_node_ids.insert(end_node_id);
    remaining_node_ids.shift_remove(&end_node_id);

    // Backward flow algorithm.
    while !remaining_node_ids.is_empty() {
        let mut progress = false;
        let mut remaining_list: Vec<K> = remaining_node_ids.iter().copied().collect();

        if shuffle_order {
            shuffle(&mut remaining_list);
        }

        for node_id in remaining_list {
            let node = state.node(node_id).expect("node must exist");

            // Get the outgoing edges and the successor node IDs.
            let mut outgoing_edges: Vec<K> = node.outgoing.iter().copied().collect();

            if shuffle_order {
                shuffle(&mut outgoing_edges);
            }

            // Are all the successor nodes (the outgoing edges' head nodes) completed?
            let all_successor_nodes_completed = outgoing_edges.iter().all(|edge_id| {
                let head_id = state
                    .edge_head_node_id(*edge_id)
                    .expect("edge head must exist");
                completed_node_ids.contains(&head_id)
            });

            if !all_successor_nodes_completed {
                continue;
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
            completed_node_ids.insert(node_id);
            remaining_node_ids.shift_remove(&node_id);
            // Note we are making progress.
            progress = true;
        }

        // If we have not made any progress then a cycle must exist in the
        // graph and we will not be able to calculate the latest finish times.
        if !progress {
            return Err(GraphError::new(
                messages::MSG_CANNOT_CALCULATE_LATEST_FINISH_TIMES_DUE_TO_CYCLIC_DEPENDENCY,
            ));
        }
    }
    Ok(true)
}

pub(crate) fn calculate_critical_path_variables<K: Key, R: Key, W: Key>(
    state: &mut ArrowState<K, R, W>,
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
