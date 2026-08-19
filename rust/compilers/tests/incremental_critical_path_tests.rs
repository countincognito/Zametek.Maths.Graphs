//! Port of `IncrementalCriticalPathTests.cs`.
//!
//! The priority-list calculation changes one activity's duration per iteration and
//! recalculates. Doing that incrementally is only worth anything if it is exactly
//! equivalent to recalculating in full, so these compare the two directly rather
//! than against a recorded baseline: the full passes are the specification, and the
//! test runs both and demands they agree.
//!
//! The C# suite additionally compares the two paths value for value after every
//! single duration change, including free slack, which the priority-list selection
//! never reads. That check needs to drive a session against a builder's own graph
//! state, which is private here, so it is not duplicated - and it does not need to
//! be: both languages check their priority lists against the same committed
//! baseline, so a divergence in the shared algorithm would fail there.

mod corpus_shapes;

use indexmap::IndexSet;
use std::sync::Arc;
use zametek_maths_graphs_compilers::contracts::IVertexCriticalPathEngine;
use zametek_maths_graphs_compilers::vertex::{IncrementalCriticalPath, VertexGraphState};
use zametek_maths_graphs_compilers::{
    NextIdGenerator, VertexCriticalPathEngine, VertexGraphBuilder, VertexGraphBuilderEngines,
};
use zametek_maths_graphs_primitives::{DependentActivity, GraphError, InvalidConstraint};

/// Declines to begin an incremental calculation, so the builder falls back to
/// recalculating in full after every duration change - the behaviour these tests
/// compare against.
#[derive(Default)]
struct FullRecalculationOnlyEngine {
    inner: VertexCriticalPathEngine,
}

impl IVertexCriticalPathEngine<i32, i32, i32> for FullRecalculationOnlyEngine {
    fn calculate_critical_path_forward_flow(
        &self,
        state: &mut VertexGraphState<i32, i32, i32>,
        invalid_constraints: &[InvalidConstraint<i32>],
        shuffle: bool,
    ) -> Result<bool, GraphError> {
        self.inner
            .calculate_critical_path_forward_flow(state, invalid_constraints, shuffle)
    }

    fn calculate_critical_path_backward_flow(
        &self,
        state: &mut VertexGraphState<i32, i32, i32>,
        invalid_constraints: &[InvalidConstraint<i32>],
        shuffle: bool,
    ) -> Result<bool, GraphError> {
        self.inner
            .calculate_critical_path_backward_flow(state, invalid_constraints, shuffle)
    }

    fn back_fill_isolated_nodes(
        &self,
        state: &mut VertexGraphState<i32, i32, i32>,
        invalid_constraints: &[InvalidConstraint<i32>],
    ) -> bool {
        self.inner
            .back_fill_isolated_nodes(state, invalid_constraints)
    }

    fn begin_incremental_critical_path(
        &self,
        _state: &VertexGraphState<i32, i32, i32>,
    ) -> Option<IncrementalCriticalPath<i32>> {
        None
    }
}

fn build(spec: &corpus_shapes::GraphSpec, full_only: bool) -> VertexGraphBuilder<i32, i32, i32> {
    let mut engines = VertexGraphBuilderEngines::<i32, i32, i32> {
        edge_id_generator: Box::new(NextIdGenerator::new(1_000_000)),
        ..Default::default()
    };

    if full_only {
        engines.critical_path_engine = Arc::new(FullRecalculationOnlyEngine::default());
    }

    let mut graph_builder = VertexGraphBuilder::with_engines(engines);

    for activity_spec in &spec.activities {
        let mut activity = DependentActivity::new(activity_spec.id, activity_spec.duration);
        activity.minimum_earliest_start_time = activity_spec.minimum_earliest_start_time;
        activity.minimum_free_slack = activity_spec.minimum_free_slack;
        activity.maximum_latest_finish_time = activity_spec.maximum_latest_finish_time;
        graph_builder.add_activity_with_dependencies(activity, activity_spec.dependencies.clone());
    }

    graph_builder
}

#[test]
fn priority_list_given_corpus_then_incremental_matches_full_recalculation() {
    for spec in corpus_shapes::generate() {
        let incremental = build(&spec, false)
            .calculate_critical_path_priority_list()
            .unwrap_or_else(|error| panic!("case {} failed: {error}", spec.name));
        let full = build(&spec, true)
            .calculate_critical_path_priority_list()
            .unwrap_or_else(|error| panic!("case {} failed: {error}", spec.name));

        assert_eq!(incremental, full, "case {}", spec.name);
    }
}

#[test]
fn priority_list_given_layered_graphs_then_incremental_matches_full_recalculation() {
    // Deliberately larger and deeper than the corpus, because the corpus graphs are
    // small enough that a propagation bug could stop before it reached anything.
    for (size, layers) in [(200, 8), (200, 40), (400, 20), (400, 100)] {
        let spec = layered_spec(size, layers);

        let incremental = build(&spec, false)
            .calculate_critical_path_priority_list()
            .expect("incremental priority list");
        let full = build(&spec, true)
            .calculate_critical_path_priority_list()
            .expect("full priority list");

        assert_eq!(incremental, full, "layered {size}/{layers}");
        assert_eq!(incremental.len(), size as usize, "layered {size}/{layers}");
    }
}

/// Each activity depends on two in the previous layer, which gives the propagation
/// somewhere to fan out to and somewhere to stop.
fn layered_spec(size: i32, layers: i32) -> corpus_shapes::GraphSpec {
    let mut activities: Vec<corpus_shapes::ActivitySpec> = Vec::new();
    let per_layer = (size / layers).max(1);

    for id in 1..=size {
        let layer = (id - 1) / per_layer;
        let mut dependencies: IndexSet<i32> = IndexSet::new();

        if layer > 0 {
            let previous_start = ((layer - 1) * per_layer) + 1;
            let previous_end = (layer * per_layer).min(size);

            if previous_end >= previous_start {
                let span = previous_end - previous_start + 1;
                dependencies.insert(previous_start + ((id * 7) % span));
                dependencies.insert(previous_start + ((id * 13) % span));
            }
        }

        let mut spec = corpus_shapes::ActivitySpec {
            id,
            duration: 1 + (id % 9),
            dependencies,
            minimum_earliest_start_time: None,
            minimum_free_slack: None,
            maximum_latest_finish_time: None,
        };

        // A scattering of constraints, because they are what make the two passes
        // interesting - the maximum latest finish time in particular feeds the
        // earliest start time through the duration, so zeroing an activity can raise
        // its own earliest start rather than only lowering its finish.
        if id % 17 == 0 {
            spec.minimum_earliest_start_time = Some(id % 5);
        }
        if id % 23 == 0 {
            spec.maximum_latest_finish_time = Some(400 + (id % 11));
        } else if id % 29 == 0 {
            spec.minimum_free_slack = Some(id % 4);
        }

        activities.push(spec);
    }

    corpus_shapes::GraphSpec {
        name: format!("layered-{size}-{layers}"),
        activities,
    }
}

#[test]
fn priority_list_given_chain_then_incremental_matches_full_recalculation() {
    // A pure chain is the shape where every selected activity is on the critical path,
    // so the project finish time moves on nearly every iteration - the case the
    // propagation seeds every End node for rather than giving up on.
    let mut activities: Vec<corpus_shapes::ActivitySpec> = Vec::new();

    for id in 1..=150 {
        let mut dependencies: IndexSet<i32> = IndexSet::new();
        if id > 1 {
            dependencies.insert(id - 1);
        }
        activities.push(corpus_shapes::ActivitySpec {
            id,
            duration: 1 + (id % 7),
            dependencies,
            minimum_earliest_start_time: None,
            minimum_free_slack: None,
            maximum_latest_finish_time: None,
        });
    }

    let spec = corpus_shapes::GraphSpec {
        name: "chain-150".to_string(),
        activities,
    };

    let incremental = build(&spec, false)
        .calculate_critical_path_priority_list()
        .expect("incremental priority list");
    let full = build(&spec, true)
        .calculate_critical_path_priority_list()
        .expect("full priority list");

    assert_eq!(incremental, full);
}

#[test]
fn priority_list_given_isolated_activities_then_incremental_matches_full_recalculation() {
    // Isolated nodes are the ones the backward pass never reaches, so their latest
    // finish time is maintained by the forward propagation alone.
    let mut activities: Vec<corpus_shapes::ActivitySpec> = Vec::new();

    for id in 1..=20 {
        activities.push(corpus_shapes::ActivitySpec {
            id,
            duration: 1 + (id % 5),
            dependencies: IndexSet::new(),
            minimum_earliest_start_time: None,
            minimum_free_slack: None,
            maximum_latest_finish_time: if id % 4 == 0 { Some(10) } else { None },
        });
    }

    let spec = corpus_shapes::GraphSpec {
        name: "isolated-20".to_string(),
        activities,
    };

    let incremental = build(&spec, false)
        .calculate_critical_path_priority_list()
        .expect("incremental priority list");
    let full = build(&spec, true)
        .calculate_critical_path_priority_list()
        .expect("full priority list");

    assert_eq!(incremental, full);
}
