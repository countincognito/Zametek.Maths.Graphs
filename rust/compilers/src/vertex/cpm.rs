use super::state::VertexGraphState;
use crate::messages;
use crate::shuffle::shuffle;
use indexmap::IndexSet;
use zametek_maths_graphs_primitives::{GraphError, InvalidConstraint, Key, NodeType};

// The critical-path engine for Activity-on-Vertex graphs — the counterpart of
// the C# `VertexCriticalPathEngine`. Implements the forward pass (earliest
// start times), backward pass (latest finish times and free slack), and the
// isolated-node backfill.
//
// Returns `Ok(false)` where the C# returns `false` (preconditions not met)
// and `Err` where the C# throws (a cycle was encountered mid-flow).

fn all_completed<K: Key>(edge_ids: &IndexSet<K>, completed: &IndexSet<K>) -> bool {
    edge_ids.iter().all(|edge_id| completed.contains(edge_id))
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
    let mut completed_edge_ids: IndexSet<K> = IndexSet::new();
    let mut remaining_edge_ids: IndexSet<K> = state.edge_ids().into_iter().collect();

    // First complete the Isolated nodes.
    for node_id in state.nodes_of_type(NodeType::Isolated) {
        let content = &mut state
            .node_mut(node_id)
            .expect("isolated node must exist")
            .content;

        // Earliest Start Time.
        let mut earliest_start_time = 0;

        if let Some(min_est) = content.minimum_earliest_start_time {
            // Augment the earliest start time artificially (if required).
            if min_est > earliest_start_time {
                earliest_start_time = min_est;
            }
        }

        if let Some(max_lft) = content.maximum_latest_finish_time {
            let proposed_latest_start_time = max_lft - content.duration;
            // Diminish the earliest start time artificially (if required).
            if proposed_latest_start_time < earliest_start_time {
                earliest_start_time = proposed_latest_start_time;
            }
        }

        content.earliest_start_time = Some(earliest_start_time);

        // Latest Finish Time.
        let mut latest_finish_time = content
            .earliest_finish_time()
            .expect("EFT follows from EST");

        if let Some(max_lft) = content.maximum_latest_finish_time {
            // Diminish the latest finish time artificially (if required).
            if max_lft < latest_finish_time {
                latest_finish_time = max_lft;
            }
        } else if let Some(min_free_slack) = content.minimum_free_slack {
            let proposed = latest_finish_time + min_free_slack;
            // Augment the latest finish time artificially (if required).
            if proposed > latest_finish_time {
                latest_finish_time = proposed;
            }
        }

        content.latest_finish_time = Some(latest_finish_time);
    }

    // Complete the Start nodes first to ensure the completed edge IDs contains something.
    for node_id in state.nodes_of_type(NodeType::Start) {
        let content = &mut state
            .node_mut(node_id)
            .expect("start node must exist")
            .content;

        let mut earliest_start_time = 0;

        if let Some(min_est) = content.minimum_earliest_start_time {
            if min_est > earliest_start_time {
                earliest_start_time = min_est;
            }
        }

        if let Some(max_lft) = content.maximum_latest_finish_time {
            let proposed_latest_start_time = max_lft - content.duration;
            if proposed_latest_start_time < earliest_start_time {
                earliest_start_time = proposed_latest_start_time;
            }
        }

        content.earliest_start_time = Some(earliest_start_time);

        let node_eft = content
            .earliest_finish_time()
            .expect("EFT follows from EST");
        let min_free_slack = content.minimum_free_slack;

        let outgoing: Vec<K> = state
            .node(node_id)
            .expect("start node must exist")
            .outgoing
            .iter()
            .copied()
            .collect();

        for outgoing_edge_id in outgoing {
            let mut earliest_finish_time = node_eft;

            if let Some(mfs) = min_free_slack {
                let proposed = earliest_finish_time + mfs;
                // Augment the earliest finish time artificially (if required).
                if proposed > earliest_finish_time {
                    earliest_finish_time = proposed;
                }
            }

            state
                .edge_mut(outgoing_edge_id)
                .expect("outgoing edge must exist")
                .content
                .earliest_finish_time = Some(earliest_finish_time);
            completed_edge_ids.insert(outgoing_edge_id);
            remaining_edge_ids.shift_remove(&outgoing_edge_id);
        }
    }

    // Forward flow algorithm.
    while !remaining_edge_ids.is_empty() {
        let mut progress = false;
        let mut remaining_list: Vec<K> = remaining_edge_ids.iter().copied().collect();

        if shuffle_order {
            shuffle(&mut remaining_list);
        }

        for edge_id in remaining_list {
            // Get the dependency node (the edge's tail).
            let dependency_node_id = state
                .edge_tail_node_id(edge_id)
                .expect("edge tail must exist");

            // If calculations for all the dependency node's incoming edges have
            // been completed, then use them to complete the calculations for
            // this edge.
            let dependency_node = state.node(dependency_node_id).expect("node must exist");
            if !all_completed(&dependency_node.incoming, &completed_edge_ids) {
                continue;
            }

            if dependency_node.content.earliest_start_time.is_none() {
                let mut earliest_start_time = dependency_node
                    .incoming
                    .iter()
                    .map(|x| {
                        state
                            .edge(*x)
                            .expect("edge must exist")
                            .content
                            .earliest_finish_time
                            .expect("completed edge must have EFT")
                    })
                    .max()
                    .expect("dependency node must have incoming edges");

                if let Some(min_est) = dependency_node.content.minimum_earliest_start_time {
                    // Augment the earliest start time artificially (if required).
                    if min_est > earliest_start_time {
                        earliest_start_time = min_est;
                    }
                }

                if let Some(max_lft) = dependency_node.content.maximum_latest_finish_time {
                    let proposed_latest_start_time = max_lft - dependency_node.content.duration;
                    // Diminish the earliest start time artificially (if required).
                    if proposed_latest_start_time < earliest_start_time {
                        earliest_start_time = proposed_latest_start_time;
                    }
                }

                state
                    .node_mut(dependency_node_id)
                    .expect("node must exist")
                    .content
                    .earliest_start_time = Some(earliest_start_time);
            }

            let dependency_content = &state
                .node(dependency_node_id)
                .expect("node must exist")
                .content;
            let mut earliest_finish_time = dependency_content
                .earliest_finish_time()
                .expect("EFT follows from EST");

            if let Some(max_lft) = dependency_content.maximum_latest_finish_time {
                // Diminish the earliest finish time artificially (if required).
                if max_lft < earliest_finish_time {
                    earliest_finish_time = max_lft;
                }
            } else if let Some(min_free_slack) = dependency_content.minimum_free_slack {
                let proposed = earliest_finish_time + min_free_slack;
                // Augment the earliest finish time artificially (if required).
                if proposed > earliest_finish_time {
                    earliest_finish_time = proposed;
                }
            }

            state
                .edge_mut(edge_id)
                .expect("edge must exist")
                .content
                .earliest_finish_time = Some(earliest_finish_time);
            completed_edge_ids.insert(edge_id);
            remaining_edge_ids.shift_remove(&edge_id);
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

    // Now complete the End nodes.
    for node_id in state.nodes_of_type(NodeType::End) {
        let node = state.node(node_id).expect("end node must exist");
        if !all_completed(&node.incoming, &completed_edge_ids) {
            return Err(GraphError::new(format!(
                "Cannot calculate EST for activity {node_id} as not all dependency events have EFT values."
            )));
        }

        if node.content.earliest_start_time.is_none() {
            let mut earliest_start_time = node
                .incoming
                .iter()
                .map(|x| {
                    state
                        .edge(*x)
                        .expect("edge must exist")
                        .content
                        .earliest_finish_time
                        .expect("completed edge must have EFT")
                })
                .max()
                .expect("end node must have incoming edges");

            if let Some(min_est) = node.content.minimum_earliest_start_time {
                if min_est > earliest_start_time {
                    earliest_start_time = min_est;
                }
            }

            if let Some(max_lft) = node.content.maximum_latest_finish_time {
                let proposed_latest_start_time = max_lft - node.content.duration;
                if proposed_latest_start_time < earliest_start_time {
                    earliest_start_time = proposed_latest_start_time;
                }
            }

            state
                .node_mut(node_id)
                .expect("node must exist")
                .content
                .earliest_start_time = Some(earliest_start_time);
        }

        let content = &state.node(node_id).expect("node must exist").content;
        if content.latest_finish_time.is_none() {
            let mut latest_finish_time = content
                .earliest_finish_time()
                .expect("EFT follows from EST");

            if let Some(max_lft) = content.maximum_latest_finish_time {
                // Diminish the latest finish time artificially (if required).
                if max_lft < latest_finish_time {
                    latest_finish_time = max_lft;
                }
            } else if let Some(min_free_slack) = content.minimum_free_slack {
                let proposed = latest_finish_time + min_free_slack;
                // Augment the latest finish time artificially (if required).
                if proposed > latest_finish_time {
                    latest_finish_time = proposed;
                }
            }

            state
                .node_mut(node_id)
                .expect("node must exist")
                .content
                .latest_finish_time = Some(latest_finish_time);
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
    let mut completed_edge_ids: IndexSet<K> = IndexSet::new();
    let mut remaining_edge_ids: IndexSet<K> = state.edge_ids().into_iter().collect();

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
            let mut latest_finish_time = end_time;

            if let Some(max_lft) = content.maximum_latest_finish_time {
                // Diminish the latest finish time artificially (if required).
                if max_lft < latest_finish_time {
                    latest_finish_time = max_lft;
                }
            }

            content.latest_finish_time = Some(latest_finish_time);

            // Free float/slack calculations.
            content.free_slack = match (content.latest_finish_time, content.earliest_finish_time())
            {
                (Some(lft), Some(eft)) => Some(lft - eft),
                _ => None,
            };
        }

        let (node_lst, node_max_lft, incoming): (Option<i32>, Option<i32>, Vec<K>) = {
            let node = state.node(node_id).expect("node must exist");
            (
                node.content.latest_start_time(),
                node.content.maximum_latest_finish_time,
                node.incoming.iter().copied().collect(),
            )
        };

        for incoming_edge_id in incoming {
            let mut latest_finish_time: Option<i32> = node_lst;

            if let Some(max_lft) = node_max_lft {
                // Diminish the latest finish time artificially (if required).
                if max_lft < latest_finish_time.unwrap_or(0) {
                    latest_finish_time = Some(max_lft);
                }
            }

            state
                .edge_mut(incoming_edge_id)
                .expect("edge must exist")
                .content
                .latest_finish_time = latest_finish_time;
            completed_edge_ids.insert(incoming_edge_id);
            remaining_edge_ids.shift_remove(&incoming_edge_id);
        }
    }

    // Backward flow algorithm.
    while !remaining_edge_ids.is_empty() {
        let mut progress = false;
        let mut remaining_list: Vec<K> = remaining_edge_ids.iter().copied().collect();

        if shuffle_order {
            shuffle(&mut remaining_list);
        }

        for edge_id in remaining_list {
            // Get the successor node (the edge's head).
            let successor_node_id = state
                .edge_head_node_id(edge_id)
                .expect("edge head must exist");

            // If calculations for all the successor node's outgoing edges have
            // been completed, then use them to complete the calculations for
            // this edge.
            let successor_node = state.node(successor_node_id).expect("node must exist");
            if !all_completed(&successor_node.outgoing, &completed_edge_ids) {
                continue;
            }

            if successor_node.content.latest_finish_time.is_none() {
                let mut latest_finish_time = successor_node
                    .outgoing
                    .iter()
                    .map(|x| {
                        state
                            .edge(*x)
                            .expect("edge must exist")
                            .content
                            .latest_finish_time
                            .expect("completed edge must have LFT")
                    })
                    .min()
                    .expect("successor node must have outgoing edges");

                if let Some(max_lft) = successor_node.content.maximum_latest_finish_time {
                    // Diminish the latest finish time artificially (if required).
                    if max_lft < latest_finish_time {
                        latest_finish_time = max_lft;
                    }
                }

                state
                    .node_mut(successor_node_id)
                    .expect("node must exist")
                    .content
                    .latest_finish_time = Some(latest_finish_time);
            }

            let successor_node = state.node(successor_node_id).expect("node must exist");
            if successor_node.content.free_slack.is_none() {
                let mut latest_finish_time = successor_node
                    .outgoing
                    .iter()
                    .map(|x| {
                        let head_id = state.edge_head_node_id(*x).expect("edge head must exist");
                        state
                            .node(head_id)
                            .expect("node must exist")
                            .content
                            .earliest_start_time
                            .expect("head node must have EST")
                    })
                    .min()
                    .expect("successor node must have outgoing edges");

                if let Some(lft) = successor_node.content.latest_finish_time {
                    // Diminish the latest finish time artificially (if required).
                    if lft < latest_finish_time {
                        latest_finish_time = lft;
                    }
                }

                if let Some(max_lft) = successor_node.content.maximum_latest_finish_time {
                    // Diminish the latest finish time artificially (if required).
                    if max_lft < latest_finish_time {
                        latest_finish_time = max_lft;
                    }
                }

                // Free float/slack calculations.
                let est = successor_node
                    .content
                    .earliest_start_time
                    .expect("successor node must have EST");
                let duration = successor_node.content.duration;
                state
                    .node_mut(successor_node_id)
                    .expect("node must exist")
                    .content
                    .free_slack = Some(latest_finish_time - est - duration);
            }

            let successor_lst = state
                .node(successor_node_id)
                .expect("node must exist")
                .content
                .latest_start_time();
            state
                .edge_mut(edge_id)
                .expect("edge must exist")
                .content
                .latest_finish_time = successor_lst;
            completed_edge_ids.insert(edge_id);
            remaining_edge_ids.shift_remove(&edge_id);
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

    // Now complete the Start nodes.
    for node_id in start_node_ids {
        let node = state.node(node_id).expect("node must exist");
        if !all_completed(&node.outgoing, &completed_edge_ids) {
            return Err(GraphError::new(format!(
                "Cannot calculate LFT for activity {node_id} as not all dependency events have LFT values."
            )));
        }

        if node.content.latest_finish_time.is_none() {
            let mut latest_finish_time = node
                .outgoing
                .iter()
                .map(|x| {
                    state
                        .edge(*x)
                        .expect("edge must exist")
                        .content
                        .latest_finish_time
                        .expect("completed edge must have LFT")
                })
                .min()
                .unwrap_or(0);

            if let Some(max_lft) = node.content.maximum_latest_finish_time {
                // Diminish the latest finish time artificially (if required).
                if max_lft < latest_finish_time {
                    latest_finish_time = max_lft;
                }
            }

            state
                .node_mut(node_id)
                .expect("node must exist")
                .content
                .latest_finish_time = Some(latest_finish_time);
        }

        let node = state.node(node_id).expect("node must exist");
        if node.content.free_slack.is_none() {
            let mut latest_finish_time = node
                .outgoing
                .iter()
                .map(|x| {
                    let head_id = state.edge_head_node_id(*x).expect("edge head must exist");
                    state
                        .node(head_id)
                        .expect("node must exist")
                        .content
                        .earliest_start_time
                        .expect("head node must have EST")
                })
                .min()
                .unwrap_or(0);

            if let Some(lft) = node.content.latest_finish_time {
                // Diminish the latest finish time artificially (if required).
                if lft < latest_finish_time {
                    latest_finish_time = lft;
                }
            }

            if let Some(max_lft) = node.content.maximum_latest_finish_time {
                // Diminish the latest finish time artificially (if required).
                if max_lft < latest_finish_time {
                    latest_finish_time = max_lft;
                }
            }

            // Free float/slack calculations.
            let est = node
                .content
                .earliest_start_time
                .expect("start node must have EST");
            let duration = node.content.duration;
            state
                .node_mut(node_id)
                .expect("node must exist")
                .content
                .free_slack = Some(latest_finish_time - est - duration);
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
