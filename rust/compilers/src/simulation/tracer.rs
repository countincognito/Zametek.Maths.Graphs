//! Reads the critical activities and the critical paths of a schedule that the
//! iteration runner has just made. Port of `CriticalPathTracer.cs`.
//!
//! A critical path is a chain of activities that sets the finish time. Each link
//! of the chain is a tight link: the predecessor is critical, and it finishes at
//! the moment that the activity starts. A predecessor is an activity that the
//! plan names (a dependency or a planning dependency), or an activity that the
//! resource allocation put before it. The chain starts at an activity with no
//! tight link and it ends at a critical activity that finishes at the finish
//! time.
//!
//! The order of the walk is fixed (the activities and their predecessors are
//! taken in the order of the plan, and not in the order in which a set happens
//! to enumerate them), so that the two ports find the same paths in the same
//! order. A path is held as a list of numbers, `(activity number << 2) | link`,
//! with the activities that take time only (a milestone is passed through).

use crate::vertex::VertexGraphBuilder;
use std::collections::HashMap;
use zametek_maths_graphs_primitives::{CriticalPathLink, Key};

/// The walk is limited to this many complete walks for each path that it may
/// record, so that a plan with very many tied paths that all look alike (they
/// differ only in milestones) cannot make an iteration take for ever.
const WALK_LIMIT_PER_PATH: i64 = 16;

pub(crate) struct CriticalPathTracer<K: Key> {
    activity_ids: Vec<K>,
    index_by_id: HashMap<K, usize>,
    critical: Vec<bool>,
    start: Vec<i32>,
    finish: Vec<i32>,
    // One frame for each activity of the chain that is being built, from the end
    // of the chain (frame 0) to its latest start.
    frame_node: Vec<usize>,
    frame_predecessors: Vec<Vec<usize>>,
    frame_next: Vec<usize>,
    frame_resource: Vec<bool>,
}

impl<K: Key> CriticalPathTracer<K> {
    pub(crate) fn new(activity_ids: &[K]) -> Self {
        let count = activity_ids.len();
        Self {
            activity_ids: activity_ids.to_vec(),
            index_by_id: activity_ids
                .iter()
                .enumerate()
                .map(|(index, id)| (*id, index))
                .collect(),
            critical: vec![false; count],
            start: vec![0; count],
            finish: vec![0; count],
            frame_node: Vec::new(),
            frame_predecessors: Vec::new(),
            frame_next: Vec::new(),
            frame_resource: Vec::new(),
        }
    }

    /// Sets the bit of each activity that is critical. The bits of an iteration
    /// are the words that start at the offset.
    pub(crate) fn fill_critical_bits<R: Key, W: Key>(
        &self,
        builder: &VertexGraphBuilder<K, R, W>,
        bits: &mut [u64],
        offset: usize,
    ) {
        for (index, id) in self.activity_ids.iter().enumerate() {
            let activity = builder.activity(*id).expect("activity must exist");
            if activity.is_critical() {
                bits[offset + (index >> 6)] |= 1u64 << (index & 63);
            }
        }
    }

    /// Finds the critical paths, and adds each one that is new to the list.
    /// Returns whether the list is complete: it is not when there are more paths
    /// than the maximum, or when the walk reached its limit.
    pub(crate) fn trace<R: Key, W: Key>(
        &mut self,
        builder: &VertexGraphBuilder<K, R, W>,
        finish_time: i32,
        durations: &[i32],
        maximum_paths: usize,
        paths: &mut Vec<Vec<u32>>,
    ) -> bool {
        for (index, id) in self.activity_ids.iter().enumerate() {
            let activity = builder.activity(*id).expect("activity must exist");
            self.critical[index] = activity.is_critical();
            self.start[index] = activity.earliest_start_time.unwrap_or(0);
            self.finish[index] = activity.earliest_finish_time().unwrap_or(0);
        }

        let mut walk_budget = (maximum_paths as i64) * WALK_LIMIT_PER_PATH;
        for sink in 0..self.activity_ids.len() {
            if !self.critical[sink] || self.finish[sink] != finish_time {
                continue;
            }

            let mut depth: isize = 0;
            self.push(builder, 0, sink);
            while depth >= 0 {
                let d = depth as usize;
                if self.frame_predecessors[d].is_empty() {
                    // The start of a chain: the chain is complete.
                    walk_budget -= 1;
                    if walk_budget < 0 {
                        return false;
                    }
                    let path = self.encode(d, durations);
                    if !path.is_empty() && !paths.contains(&path) {
                        if paths.len() >= maximum_paths {
                            return false;
                        }
                        paths.push(path);
                    }
                    depth -= 1;
                } else if self.frame_next[d] >= self.frame_predecessors[d].len() {
                    depth -= 1;
                } else {
                    let next = self.frame_predecessors[d][self.frame_next[d]];
                    self.frame_next[d] += 1;
                    self.frame_resource[d] = (next & 1) != 0;
                    depth += 1;
                    self.push(builder, d + 1, next >> 1);
                }
            }
        }
        true
    }

    fn push<R: Key, W: Key>(
        &mut self,
        builder: &VertexGraphBuilder<K, R, W>,
        depth: usize,
        node: usize,
    ) {
        let predecessors = self.tight_predecessors(builder, node);
        if depth == self.frame_node.len() {
            self.frame_node.push(node);
            self.frame_predecessors.push(predecessors);
            self.frame_next.push(0);
            self.frame_resource.push(false);
        } else {
            self.frame_node[depth] = node;
            self.frame_predecessors[depth] = predecessors;
            self.frame_next[depth] = 0;
            self.frame_resource[depth] = false;
        }
    }

    // The tight predecessors of a critical activity, as
    // `(predecessor number << 1) | 1` for a link that exists only because of a
    // resource, and as `predecessor number << 1` for a link of the plan. They
    // are sorted by the number of the predecessor.
    fn tight_predecessors<R: Key, W: Key>(
        &self,
        builder: &VertexGraphBuilder<K, R, W>,
        node: usize,
    ) -> Vec<usize> {
        let activity = builder
            .activity(self.activity_ids[node])
            .expect("activity must exist");
        let mut found: Vec<usize> = Vec::with_capacity(4);

        for id in &activity.dependencies {
            self.consider(node, *id, false, &mut found);
        }
        for id in &activity.planning_dependencies {
            if !activity.dependencies.contains(id) {
                self.consider(node, *id, false, &mut found);
            }
        }
        for id in &activity.resource_dependencies {
            if !activity.dependencies.contains(id) && !activity.planning_dependencies.contains(id) {
                self.consider(node, *id, true, &mut found);
            }
        }

        found.sort_unstable();
        found
    }

    fn consider(&self, node: usize, predecessor_id: K, resource: bool, found: &mut Vec<usize>) {
        if let Some(&predecessor) = self.index_by_id.get(&predecessor_id) {
            if self.critical[predecessor] && self.finish[predecessor] == self.start[node] {
                found.push((predecessor << 1) | usize::from(resource));
            }
        }
    }

    // The chain in frames 0 to `depth`, as a path in the order of the schedule:
    // from the frame at the depth (the start of the chain) back to frame 0 (its
    // end).
    fn encode(&self, depth: usize, durations: &[i32]) -> Vec<u32> {
        let mut steps: Vec<u32> = Vec::with_capacity(depth + 1);
        let mut saw_resource = false;
        let mut first = true;
        for k in (0..=depth).rev() {
            let node = self.frame_node[k];
            if k < depth && self.frame_resource[k] {
                saw_resource = true;
            }
            if durations[node] == 0 {
                continue;
            }

            let link = if first {
                CriticalPathLink::None
            } else if saw_resource {
                CriticalPathLink::Resource
            } else {
                CriticalPathLink::Dependency
            };
            steps.push(((node as u32) << 2) | (link as u32));
            first = false;
            saw_resource = false;
        }
        steps
    }
}
