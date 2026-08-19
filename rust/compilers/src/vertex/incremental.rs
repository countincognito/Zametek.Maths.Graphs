//! The incremental critical-path recalculation - the counterpart of the C#
//! `VertexCriticalPathEngine.IncrementalCriticalPath`.
//!
//! Between iterations of the priority-list calculation exactly one thing changes -
//! one activity's duration is reduced to zero - and very little moves with it.
//! Measured on a 2,000-activity graph, about 19 earliest start times and 99 latest
//! finish times change per iteration, so a full pass recomputes 2,000 values in
//! order to alter around 118 of them and discards the rest of the work.
//!
//! This propagates outwards from the changed activity and stops wherever a
//! recomputed value equals the one already there. That early stop is the entire
//! saving, and it is exact rather than approximate: a value that has not changed
//! cannot change anything downstream of it, because everything below depends on the
//! values above only through the numbers being compared here.
//!
//! The graph structure does not change during a priority-list calculation, so the
//! topological order is computed once and reused. Visiting changed nodes in that
//! order means each is recomputed at most once, with all of its predecessors already
//! final - which is what makes a single comparison per node sufficient.

use super::cpm::{
    augmented_finish_time, clamp_earliest_start_time, clamp_latest_finish_time,
    end_node_incoming_edge_latest_finish_time, free_slack_from_successors,
    max_edge_earliest_finish_time, min_edge_latest_finish_time, min_successor_earliest_start_time,
    start_node_edge_earliest_finish_time,
};
use super::state::VertexGraphState;
use indexmap::{IndexMap, IndexSet};
use std::collections::BTreeSet;
use zametek_maths_graphs_primitives::{Key, NodeType};

/// An in-progress incremental calculation over a graph whose structure does not
/// change.
///
/// Only valid for as long as that holds: adding or removing an activity or a
/// dependency invalidates the cached ordering and a new one must be begun. Durations
/// may change freely - that is what it exists for.
pub struct IncrementalCriticalPath<K: Key> {
    nodes_in_topological_order: Vec<K>,
    topological_index_by_node_id: IndexMap<K, usize>,

    /// The nodes the project finish time is measured from, and where they sit in the
    /// order. Held rather than asked for: `nodes_of_type` filters every node in the
    /// graph and collects them, and both the finish time and the seeding below want
    /// them on iterations that are supposed to cost nothing proportional to the graph.
    end_nodes: Vec<K>,
    isolated_nodes: Vec<K>,
    end_node_topological_indexes: Vec<usize>,

    /// The project finish time the current latest finish times were built from. It
    /// has to be remembered rather than recomputed on entry, because by then the
    /// caller has already changed the duration and the graph no longer holds the
    /// value being compared against.
    end_time: i32,

    // The work lists are fields rather than locals so that an iteration allocates
    // nothing; the whole point of this type is to stop doing work per activity that
    // is proportional to the graph.
    pending_topological_indexes: BTreeSet<usize>,
    earliest_start_time_changed: IndexSet<K>,
    latest_finish_time_changed: IndexSet<K>,
    free_slack_to_recompute: IndexSet<K>,
    edge_scratch: Vec<K>,

    /// Whether the forward propagation reached a node the finish time is measured
    /// from, and so whether that finish time is worth recomputing at all.
    end_or_isolated_node_reached: bool,
}

impl<K: Key> IncrementalCriticalPath<K> {
    /// Begins a session over a graph that has already been calculated in full.
    ///
    /// Returns `None` when the graph cannot be ordered, which means it contains a
    /// cycle; the caller should fall back to the full passes, which report that
    /// properly.
    pub fn begin<R: Key, W: Key>(state: &VertexGraphState<K, R, W>) -> Option<Self> {
        let mut nodes_in_topological_order: Vec<K> = Vec::with_capacity(state.nodes.len());
        let mut topological_index_by_node_id: IndexMap<K, usize> =
            IndexMap::with_capacity(state.nodes.len());
        let mut pending_incoming_edge_counts: IndexMap<K, usize> =
            IndexMap::with_capacity(state.nodes.len());
        let mut ready: Vec<K> = Vec::new();

        for node in state.nodes.values() {
            let incoming_edge_count =
                if matches!(node.node_type(), NodeType::Start | NodeType::Isolated) {
                    0
                } else {
                    node.incoming.len()
                };
            pending_incoming_edge_counts.insert(node.id(), incoming_edge_count);

            if incoming_edge_count == 0 {
                ready.push(node.id());
            }
        }

        while let Some(node_id) = ready.pop() {
            topological_index_by_node_id.insert(node_id, nodes_in_topological_order.len());
            nodes_in_topological_order.push(node_id);

            let node = state.node(node_id).expect("node must exist");
            if matches!(node.node_type(), NodeType::End | NodeType::Isolated) {
                continue;
            }

            for edge_id in node.outgoing.iter().copied() {
                let successor_node_id = state
                    .edge_head_node_id(edge_id)
                    .expect("edge head must exist");
                let pending = pending_incoming_edge_counts
                    .get_mut(&successor_node_id)
                    .expect("node with incoming edges must have a pending count");
                *pending -= 1;

                if *pending == 0 {
                    ready.push(successor_node_id);
                }
            }
        }

        // A node left unordered means the walk could not reach it, which only a cycle
        // can cause.
        if nodes_in_topological_order.len() != state.nodes.len() {
            return None;
        }

        let mut end_nodes: Vec<K> = Vec::new();
        let mut isolated_nodes: Vec<K> = Vec::new();
        let mut end_node_topological_indexes: Vec<usize> = Vec::new();

        for (index, node_id) in nodes_in_topological_order.iter().copied().enumerate() {
            match state.node(node_id).expect("node must exist").node_type() {
                NodeType::End => {
                    end_nodes.push(node_id);
                    end_node_topological_indexes.push(index);
                }
                NodeType::Isolated => isolated_nodes.push(node_id),
                _ => {}
            }
        }

        let mut session = Self {
            nodes_in_topological_order,
            topological_index_by_node_id,
            end_nodes,
            isolated_nodes,
            end_node_topological_indexes,
            end_time: 0,
            pending_topological_indexes: BTreeSet::new(),
            earliest_start_time_changed: IndexSet::new(),
            latest_finish_time_changed: IndexSet::new(),
            free_slack_to_recompute: IndexSet::new(),
            edge_scratch: Vec::new(),
            end_or_isolated_node_reached: false,
        };
        session.end_time = session.compute_end_time(state);
        Some(session)
    }

    /// Recalculates the graph after the given activity's duration has been changed,
    /// propagating from that activity and stopping wherever a recomputed value
    /// matches the one already there.
    ///
    /// Returns false only when the activity is not part of this graph, in which case
    /// the caller should recalculate in full.
    pub fn apply_duration_change<R: Key, W: Key>(
        &mut self,
        state: &mut VertexGraphState<K, R, W>,
        activity_id: K,
    ) -> bool {
        let Some(changed_node_index) = self.topological_index_by_node_id.get(&activity_id).copied()
        else {
            return false;
        };

        self.earliest_start_time_changed.clear();
        self.latest_finish_time_changed.clear();
        self.free_slack_to_recompute.clear();
        self.pending_topological_indexes.clear();

        // Forwards, in topological order.
        self.end_or_isolated_node_reached = false;
        self.pending_topological_indexes.insert(changed_node_index);

        while let Some(index) = self.pending_topological_indexes.iter().next().copied() {
            self.pending_topological_indexes.remove(&index);
            let node_id = self.nodes_in_topological_order[index];
            self.propagate_earliest_times(state, node_id);
        }

        // The project finish time is only worth recomputing if the propagation reached
        // something it is measured from. On a graph whose last layer is wide, that scan
        // is otherwise the one part of an iteration that stays proportional to the
        // graph.
        let mut end_time_changed = false;

        if self.end_or_isolated_node_reached {
            let end_time = self.compute_end_time(state);
            end_time_changed = end_time != self.end_time;
            self.end_time = end_time;
        }

        // Backwards, in reverse topological order, seeded by the changed activity - its
        // duration feeds the latest finish times of everything before it.
        //
        // A moved finish time is seeded rather than surrendered to. It changes every End
        // node's latest finish time and so, in principle, the whole graph; but only in
        // principle, and the propagation stops wherever a value has not actually moved
        // just as it does anywhere else. This matters more than it looks: the selection
        // picks the activity with the least total slack, which is one on the critical
        // path, so shortening it moves the finish time far more often than the share of
        // iterations measured on shallow graphs would suggest - and the deeper the
        // graph, the more often.
        self.pending_topological_indexes.insert(changed_node_index);

        if end_time_changed {
            for index in self.end_node_topological_indexes.iter().copied() {
                self.pending_topological_indexes.insert(index);
            }
        }

        while let Some(index) = self.pending_topological_indexes.iter().next_back().copied() {
            self.pending_topological_indexes.remove(&index);
            let node_id = self.nodes_in_topological_order[index];
            self.propagate_latest_times(state, node_id);
        }

        self.recompute_free_slack(state, activity_id);
        true
    }

    /// The forward pass's finish value for the End and Isolated nodes, which is what
    /// it feeds the backward pass. The End nodes' stored latest finish time cannot be
    /// read back for this, because the backward pass overwrites it with a value
    /// derived from this one - so it is recomputed from the earliest finish time,
    /// exactly as the forward pass computes it.
    fn compute_end_time<R: Key, W: Key>(&self, state: &VertexGraphState<K, R, W>) -> i32 {
        let end_nodes_end_time = self
            .end_nodes
            .iter()
            .copied()
            .map(|node_id| {
                let content = &state.node(node_id).expect("node must exist").content;
                augmented_finish_time(
                    content,
                    content
                        .earliest_finish_time()
                        .expect("EFT follows from EST"),
                )
            })
            .max()
            .unwrap_or(0);

        let isolated_nodes_end_time = self
            .isolated_nodes
            .iter()
            .copied()
            .filter_map(|node_id| {
                state
                    .node(node_id)
                    .expect("node must exist")
                    .content
                    .latest_finish_time
            })
            .max()
            .unwrap_or(0);

        end_nodes_end_time.max(isolated_nodes_end_time)
    }

    fn propagate_earliest_times<R: Key, W: Key>(
        &mut self,
        state: &mut VertexGraphState<K, R, W>,
        node_id: K,
    ) {
        let node = state.node(node_id).expect("node must exist");
        let node_type = node.node_type();

        let base = if matches!(node_type, NodeType::Start | NodeType::Isolated) {
            0
        } else {
            max_edge_earliest_finish_time(state, &node.incoming)
        };

        {
            let content = &mut state.node_mut(node_id).expect("node must exist").content;
            let earliest_start_time = clamp_earliest_start_time(content, base);

            if content.earliest_start_time != Some(earliest_start_time) {
                content.earliest_start_time = Some(earliest_start_time);
                self.earliest_start_time_changed.insert(node_id);
            }
        }

        if node_type == NodeType::Isolated {
            // An Isolated node's latest finish time comes from the forward pass and is
            // never revisited by the backward one, so it is maintained here.
            let content = &mut state.node_mut(node_id).expect("node must exist").content;
            let earliest_finish_time = content
                .earliest_finish_time()
                .expect("EFT follows from EST");
            content.latest_finish_time = Some(augmented_finish_time(content, earliest_finish_time));
            self.end_or_isolated_node_reached = true;
            return;
        }

        if node_type == NodeType::End {
            self.end_or_isolated_node_reached = true;
            return;
        }

        // Every outgoing edge of a node is given the same value, so one comparison
        // decides whether anything downstream needs revisiting. The value is recomputed
        // even when the earliest start time did not move, because the changed activity's
        // own duration feeds it directly.
        let edge_earliest_finish_time = {
            let content = &state.node(node_id).expect("node must exist").content;
            let node_eft = content
                .earliest_finish_time()
                .expect("EFT follows from EST");

            if node_type == NodeType::Start {
                start_node_edge_earliest_finish_time(content, node_eft)
            } else {
                augmented_finish_time(content, node_eft)
            }
        };

        let mut edge_scratch = std::mem::take(&mut self.edge_scratch);
        edge_scratch.clear();
        edge_scratch.extend(
            state
                .node(node_id)
                .expect("node must exist")
                .outgoing
                .iter()
                .copied(),
        );

        for edge_id in edge_scratch.iter().copied() {
            let edge = state.edge_mut(edge_id).expect("edge must exist");

            if edge.content.earliest_finish_time == Some(edge_earliest_finish_time) {
                continue;
            }

            edge.content.earliest_finish_time = Some(edge_earliest_finish_time);
            let head_node_id = state
                .edge_head_node_id(edge_id)
                .expect("edge head must exist");
            let index = self
                .topological_index_by_node_id
                .get(&head_node_id)
                .copied()
                .expect("head node must be ordered");
            self.pending_topological_indexes.insert(index);
        }

        self.edge_scratch = edge_scratch;
    }

    fn propagate_latest_times<R: Key, W: Key>(
        &mut self,
        state: &mut VertexGraphState<K, R, W>,
        node_id: K,
    ) {
        let node_type = state.node(node_id).expect("node must exist").node_type();

        // An End node takes its latest finish time from the project finish time;
        // everything else takes it from the edges leaving it. An Isolated node has
        // neither, and keeps the value the forward propagation gave it.
        if node_type != NodeType::Isolated {
            let base = if node_type == NodeType::End {
                self.end_time
            } else {
                let node = state.node(node_id).expect("node must exist");
                min_edge_latest_finish_time(state, &node.outgoing)
            };

            let content = &mut state.node_mut(node_id).expect("node must exist").content;
            let latest_finish_time = clamp_latest_finish_time(content, base);

            if content.latest_finish_time != Some(latest_finish_time) {
                content.latest_finish_time = Some(latest_finish_time);
                self.latest_finish_time_changed.insert(node_id);
            }
        }

        if matches!(node_type, NodeType::Start | NodeType::Isolated) {
            return;
        }

        let edge_latest_finish_time = {
            let content = &state.node(node_id).expect("node must exist").content;

            if node_type == NodeType::End {
                end_node_incoming_edge_latest_finish_time(content)
            } else {
                content.latest_start_time()
            }
        };

        let mut edge_scratch = std::mem::take(&mut self.edge_scratch);
        edge_scratch.clear();
        edge_scratch.extend(
            state
                .node(node_id)
                .expect("node must exist")
                .incoming
                .iter()
                .copied(),
        );

        for edge_id in edge_scratch.iter().copied() {
            let edge = state.edge_mut(edge_id).expect("edge must exist");

            if edge.content.latest_finish_time == edge_latest_finish_time {
                continue;
            }

            edge.content.latest_finish_time = edge_latest_finish_time;
            let tail_node_id = state
                .edge_tail_node_id(edge_id)
                .expect("edge tail must exist");
            let index = self
                .topological_index_by_node_id
                .get(&tail_node_id)
                .copied()
                .expect("tail node must be ordered");
            self.pending_topological_indexes.insert(index);
        }

        self.edge_scratch = edge_scratch;
    }

    /// Free slack is tracked separately from the two flows because it depends on both,
    /// and on the successors as well as on the node itself: a node's free slack moves
    /// if its own earliest start, latest finish or duration moved, or if any of its
    /// successors' earliest start times did.
    ///
    /// The priority-list selection does not read free slack - it works from total
    /// slack, which is derived from the two flows - so this is not needed to get the
    /// list right. It is maintained so that an incrementally updated graph is
    /// indistinguishable from a fully recalculated one, which is what lets the two be
    /// compared value for value.
    fn recompute_free_slack<R: Key, W: Key>(
        &mut self,
        state: &mut VertexGraphState<K, R, W>,
        changed_activity_id: K,
    ) {
        self.free_slack_to_recompute.insert(changed_activity_id);

        for node_id in self.latest_finish_time_changed.iter().copied() {
            self.free_slack_to_recompute.insert(node_id);
        }

        let earliest_start_time_changed = std::mem::take(&mut self.earliest_start_time_changed);

        for node_id in earliest_start_time_changed.iter().copied() {
            self.free_slack_to_recompute.insert(node_id);

            let node = state.node(node_id).expect("node must exist");
            if matches!(node.node_type(), NodeType::Start | NodeType::Isolated) {
                continue;
            }

            for edge_id in node.incoming.iter().copied() {
                let tail_node_id = state
                    .edge_tail_node_id(edge_id)
                    .expect("edge tail must exist");
                self.free_slack_to_recompute.insert(tail_node_id);
            }
        }

        self.earliest_start_time_changed = earliest_start_time_changed;

        let free_slack_to_recompute = std::mem::take(&mut self.free_slack_to_recompute);

        for node_id in free_slack_to_recompute.iter().copied() {
            let node = state.node(node_id).expect("node must exist");
            let node_type = node.node_type();

            if node_type == NodeType::Isolated {
                // Left alone, as the full passes leave it: an Isolated node's free slack
                // is filled in afterwards by `back_fill_isolated_nodes`.
                continue;
            }

            if node_type == NodeType::End {
                let content = &mut state.node_mut(node_id).expect("node must exist").content;
                content.free_slack =
                    match (content.latest_finish_time, content.earliest_finish_time()) {
                        (Some(lft), Some(eft)) => Some(lft - eft),
                        _ => None,
                    };
                continue;
            }

            let base = min_successor_earliest_start_time(state, &node.outgoing);
            let content = &mut state.node_mut(node_id).expect("node must exist").content;
            content.free_slack = free_slack_from_successors(content, base);
        }

        self.free_slack_to_recompute = free_slack_to_recompute;
    }
}
