//! The Activity-on-Vertex view of the shared corpus - the counterpart of the C#
//! `PriorityListCorpus`.
//!
//! Used to prove that changes to the priority-list calculation (or to the vertex
//! critical-path engine underneath it) leave its output untouched. The priority
//! list decides which activity is offered a resource first, so it determines the
//! final schedule: an optimisation that alters it is a behaviour change, not an
//! optimisation.
//!
//! The shapes themselves, and the reasoning behind them, live in
//! [`crate::corpus_shapes`] - the arrow corpus builds the same networks, so the
//! two engines are pinned against the same set of graphs.

#![allow(dead_code)]

use crate::corpus_shapes;
use zametek_maths_graphs_compilers::{NextIdGenerator, VertexGraphBuilder};
use zametek_maths_graphs_primitives::DependentActivity;

pub type Builder = VertexGraphBuilder<i32, i32, i32>;

pub struct Case {
    pub name: String,
    pub graph_builder: Builder,
}

pub fn generate() -> Vec<Case> {
    corpus_shapes::generate().into_iter().map(build).collect()
}

fn build(spec: corpus_shapes::GraphSpec) -> Case {
    let mut graph_builder = VertexGraphBuilder::new(NextIdGenerator::new(0));

    for activity_spec in &spec.activities {
        let mut activity = DependentActivity::new(activity_spec.id, activity_spec.duration);
        activity.minimum_earliest_start_time = activity_spec.minimum_earliest_start_time;
        activity.minimum_free_slack = activity_spec.minimum_free_slack;
        activity.maximum_latest_finish_time = activity_spec.maximum_latest_finish_time;
        graph_builder.add_activity_with_dependencies(activity, activity_spec.dependencies.clone());
    }

    Case {
        name: spec.name,
        graph_builder,
    }
}
