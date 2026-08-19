//! The Activity-on-Arrow view of the shared corpus - the counterpart of the C#
//! `ArrowCriticalPathCorpus`.
//!
//! The shapes are the same networks the priority-list corpus builds, so both
//! engines are pinned against the same graphs. What differs is the
//! representation: here activities sit on edges and events on nodes, and the
//! builder inserts dummy edges to express the dependencies, so the graph the
//! engine walks is considerably larger than the activity list that produced it.
//!
//! The generator is shared with the vertex corpus (`priority_list_corpus`), and
//! is itself a value-for-value port of the C# one, so all three corpora - two
//! Rust, one C# - describe exactly the same networks.

#![allow(dead_code)]

use crate::corpus_shapes;
use zametek_maths_graphs_compilers::{ArrowGraphBuilder, NextIdGenerator};
use zametek_maths_graphs_primitives::DependentActivity;

pub type Builder = ArrowGraphBuilder<i32, i32, i32>;

/// The dummy-activity and event ID generators start where the C# arrow corpus
/// starts them, so generated IDs are stable and the baseline stays valid.
const FIRST_DUMMY_ACTIVITY_ID: i32 = 100_000;
const FIRST_EVENT_ID: i32 = 0;

pub struct Case {
    pub name: String,
    pub graph_builder: Builder,
}

pub fn generate() -> Vec<Case> {
    corpus_shapes::generate().into_iter().map(build).collect()
}

fn build(spec: corpus_shapes::GraphSpec) -> Case {
    let mut graph_builder = ArrowGraphBuilder::<i32, i32, i32>::new(
        NextIdGenerator::new(FIRST_DUMMY_ACTIVITY_ID),
        NextIdGenerator::new(FIRST_EVENT_ID),
    );

    for activity_spec in &spec.activities {
        let mut activity = DependentActivity::new(activity_spec.id, activity_spec.duration);
        activity.minimum_earliest_start_time = activity_spec.minimum_earliest_start_time;
        activity.minimum_free_slack = activity_spec.minimum_free_slack;
        activity.maximum_latest_finish_time = activity_spec.maximum_latest_finish_time;
        graph_builder.add_activity_with_dependencies(activity, activity_spec.dependencies.clone());
    }

    // The arrow critical-path tests all reduce before calculating, because the
    // builder's raw dependency wiring leaves redundant dummy edges behind that
    // change the free-slack values. Reducing here keeps the corpus consistent
    // with how the engine is actually driven.
    graph_builder
        .transitive_reduction()
        .expect("transitive reduction must succeed");

    Case {
        name: spec.name,
        graph_builder,
    }
}
