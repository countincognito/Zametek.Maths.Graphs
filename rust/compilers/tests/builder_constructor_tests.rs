//! Port of `GraphBuilderConstructorTests.cs`. The builders have constructors
//! that take only the ID generator factories (`new`, and `from_graph` when they
//! load a graph), and they take their standard engines from the engines bundle.
//! These tests show that they use the factories that they are given, also when
//! they load a graph. The builder asks a factory for a generator for the IDs
//! that the graph holds, so that the events (vertex), and the dummy activities
//! and the nodes (arrow), that the builder makes later get IDs from the
//! generator that are clear of those IDs.
//!
//! The C# tests of a null factory, of a bundle that has none and of a factory
//! that gives null have no counterpart: a factory in Rust cannot be null, and
//! `create` gives a `Box`.

use indexmap::IndexSet;
use std::sync::{Arc, Mutex};
use zametek_maths_graphs_compilers::contracts::{IIdGenerator, IIdGeneratorFactory};
use zametek_maths_graphs_compilers::primitives::DependentActivity;
use zametek_maths_graphs_compilers::{
    ArrowGraphBuilder, NextIdGenerator, NextIdGeneratorFactory, PreviousIdGeneratorFactory,
    VertexGraphBuilder,
};

type Act = DependentActivity<i32, i32, i32>;
type VertexBuilder = VertexGraphBuilder<i32, i32, i32>;
type ArrowBuilder = ArrowGraphBuilder<i32, i32, i32>;

// A factory that records the IDs that it is asked about, and gives generators
// that step up from 1,000.
struct RecordingIdGeneratorFactory {
    requests: Arc<Mutex<Vec<Vec<i32>>>>,
}

impl RecordingIdGeneratorFactory {
    fn new() -> (Self, Arc<Mutex<Vec<Vec<i32>>>>) {
        let requests = Arc::new(Mutex::new(Vec::new()));
        (
            Self {
                requests: Arc::clone(&requests),
            },
            requests,
        )
    }
}

impl IIdGeneratorFactory<i32> for RecordingIdGeneratorFactory {
    fn create(&self, used_ids: &[i32]) -> Box<dyn IIdGenerator<i32>> {
        self.requests.lock().unwrap().push(used_ids.to_vec());
        Box::new(NextIdGenerator::new(1_000))
    }
}

fn add_vertex_plan(builder: &mut VertexBuilder) {
    assert!(builder.add_activity(Act::new(1, 3)));
    assert!(builder.add_activity_with_dependencies(Act::new(2, 5), IndexSet::from([1])));
}

// 3 needs 1 and 2, and 4 needs only 2, which needs a dummy activity.
fn add_arrow_plan(builder: &mut ArrowBuilder) {
    assert!(builder.add_activity(Act::new(1, 3)));
    assert!(builder.add_activity(Act::new(2, 5)));
    assert!(builder.add_activity_with_dependencies(Act::new(3, 2), IndexSet::from([1, 2])));
    assert!(builder.add_activity_with_dependencies(Act::new(4, 4), IndexSet::from([2])));
}

// The IDs that the second list has and the first does not.
fn added(first: &[i32], second: &[i32]) -> Vec<i32> {
    second
        .iter()
        .copied()
        .filter(|x| !first.contains(x))
        .collect()
}

fn sorted(mut ids: Vec<i32>) -> Vec<i32> {
    ids.sort_unstable();
    ids
}

// -- Vertex -------------------------------------------------------------------

#[test]
fn vertex_new_given_an_id_generator_factory_then_the_events_have_ids_from_it() {
    let mut builder = VertexBuilder::new(NextIdGeneratorFactory::new(1000));

    add_vertex_plan(&mut builder);

    assert!(!builder.event_ids().is_empty());
    assert!(builder.event_ids().iter().all(|x| *x > 1000));
}

#[test]
fn vertex_from_graph_given_a_graph_and_an_id_generator_factory_then_the_events_added_later_have_ids_from_it(
) {
    let mut first = VertexBuilder::new(NextIdGeneratorFactory::new(0));
    add_vertex_plan(&mut first);

    let mut second =
        VertexBuilder::from_graph(first.to_graph().unwrap(), NextIdGeneratorFactory::new(1000))
            .unwrap();
    assert!(second.add_activity_with_dependencies(Act::new(3, 2), IndexSet::from([2])));

    // The events of the graph keep their IDs, and the new ones come from the generator.
    let first_events = first.event_ids();
    let second_events = second.event_ids();
    assert!(first_events.iter().all(|x| second_events.contains(x)));
    let new_events = added(&first_events, &second_events);
    assert!(!new_events.is_empty());
    assert!(new_events.iter().all(|x| *x > 1000));
}

#[test]
fn vertex_from_graph_given_a_factory_that_starts_inside_the_graph_then_the_events_added_later_do_not_reuse_its_ids(
) {
    // The graph has the event 1,001. A generator that stepped up from 1,000
    // would give 1,001 again.
    let mut first = VertexBuilder::new(NextIdGeneratorFactory::new(1000));
    add_vertex_plan(&mut first);
    assert_eq!(first.event_ids(), vec![1001]);

    let mut second =
        VertexBuilder::from_graph(first.to_graph().unwrap(), NextIdGeneratorFactory::new(1000))
            .unwrap();
    assert!(second.add_activity_with_dependencies(Act::new(3, 2), IndexSet::from([2])));

    assert_eq!(sorted(second.event_ids()), vec![1001, 1002]);
}

#[test]
fn vertex_from_graph_given_a_graph_and_the_default_factory_then_the_events_added_later_step_down_from_the_lowest_one(
) {
    let mut first = VertexBuilder::new(PreviousIdGeneratorFactory::default());
    add_vertex_plan(&mut first);
    assert_eq!(first.event_ids(), vec![-1]);

    let mut second = VertexBuilder::from_graph(
        first.to_graph().unwrap(),
        PreviousIdGeneratorFactory::default(),
    )
    .unwrap();
    assert!(second.add_activity_with_dependencies(Act::new(3, 2), IndexSet::from([2])));

    assert_eq!(sorted(second.event_ids()), vec![-2, -1]);
}

#[test]
fn vertex_new_then_the_factory_is_asked_for_a_generator_for_no_ids() {
    let (factory, requests) = RecordingIdGeneratorFactory::new();

    VertexBuilder::new(factory);

    let requests = requests.lock().unwrap();
    assert_eq!(requests.len(), 1);
    assert!(requests[0].is_empty());
}

#[test]
fn vertex_from_graph_then_the_factory_is_asked_for_a_generator_for_the_ids_of_its_edges() {
    let mut first = VertexBuilder::new(NextIdGeneratorFactory::new(0));
    add_vertex_plan(&mut first);
    let graph = first.to_graph().unwrap();
    let expected = sorted(graph.edges.iter().map(|e| e.id()).collect());
    let (factory, requests) = RecordingIdGeneratorFactory::new();

    VertexBuilder::from_graph(graph, factory).unwrap();

    let requests = requests.lock().unwrap();
    assert_eq!(requests.len(), 1);
    assert!(!requests[0].is_empty());
    assert_eq!(sorted(requests[0].clone()), expected);
}

// -- Arrow --------------------------------------------------------------------

#[test]
fn arrow_new_given_id_generator_factories_then_the_dummy_activities_and_the_nodes_have_ids_from_them(
) {
    let mut builder = ArrowBuilder::new(
        NextIdGeneratorFactory::new(1000),
        NextIdGeneratorFactory::new(2000),
    );

    add_arrow_plan(&mut builder);

    assert!(!builder.node_ids().is_empty());
    assert!(builder.node_ids().iter().all(|x| *x > 2000));
    let dummies = added(&[1, 2, 3, 4], &builder.edge_ids());
    assert!(!dummies.is_empty());
    assert!(dummies.iter().all(|x| *x > 1000));
}

#[test]
fn arrow_from_graph_given_a_graph_and_id_generator_factories_then_the_dummy_activities_and_the_nodes_added_later_have_ids_from_them(
) {
    let mut first = ArrowBuilder::new(
        NextIdGeneratorFactory::new(100),
        NextIdGeneratorFactory::new(200),
    );
    add_arrow_plan(&mut first);

    let mut second = ArrowBuilder::from_graph(
        first.to_graph().unwrap(),
        NextIdGeneratorFactory::new(1000),
        NextIdGeneratorFactory::new(2000),
    )
    .unwrap();
    assert!(second.add_activity_with_dependencies(Act::new(5, 1), IndexSet::from([3, 4])));

    // What the graph held keeps its IDs, and what is made later comes from the generators.
    let first_nodes = first.node_ids();
    let second_nodes = second.node_ids();
    assert!(first_nodes.iter().all(|x| second_nodes.contains(x)));
    let new_nodes = added(&first_nodes, &second_nodes);
    assert!(!new_nodes.is_empty());
    assert!(new_nodes.iter().all(|x| *x > 2000));
    let mut known = first.edge_ids();
    known.push(5);
    let dummies = added(&known, &second.edge_ids());
    assert!(!dummies.is_empty());
    assert!(dummies.iter().all(|x| *x > 1000));
}

#[test]
fn arrow_from_graph_given_factories_that_start_inside_the_graph_then_what_is_made_later_does_not_reuse_its_ids(
) {
    // The factories start at the IDs that the graph already holds: a generator
    // that stepped up from there would give them again.
    let mut first = ArrowBuilder::new(
        NextIdGeneratorFactory::new(100),
        NextIdGeneratorFactory::new(200),
    );
    add_arrow_plan(&mut first);
    let highest_node = *first.node_ids().iter().max().unwrap();
    let highest_edge = *first.edge_ids().iter().max().unwrap();

    let mut second = ArrowBuilder::from_graph(
        first.to_graph().unwrap(),
        NextIdGeneratorFactory::new(100),
        NextIdGeneratorFactory::new(200),
    )
    .unwrap();
    assert!(second.add_activity_with_dependencies(Act::new(5, 1), IndexSet::from([3, 4])));

    let new_nodes = added(&first.node_ids(), &second.node_ids());
    assert!(!new_nodes.is_empty());
    assert!(new_nodes.iter().all(|x| *x > highest_node));
    let mut known = first.edge_ids();
    known.push(5);
    let dummies = added(&known, &second.edge_ids());
    assert!(!dummies.is_empty());
    assert!(dummies.iter().all(|x| *x > highest_edge));
}

#[test]
fn arrow_from_graph_given_a_graph_and_the_default_factories_then_what_is_made_later_steps_down_from_the_lowest_id(
) {
    let mut first = ArrowBuilder::new(
        PreviousIdGeneratorFactory::default(),
        PreviousIdGeneratorFactory::default(),
    );
    add_arrow_plan(&mut first);
    let lowest_node = *first.node_ids().iter().min().unwrap();
    let lowest_edge = *first.edge_ids().iter().min().unwrap();

    let mut second = ArrowBuilder::from_graph(
        first.to_graph().unwrap(),
        PreviousIdGeneratorFactory::default(),
        PreviousIdGeneratorFactory::default(),
    )
    .unwrap();
    assert!(second.add_activity_with_dependencies(Act::new(5, 1), IndexSet::from([3, 4])));

    let new_nodes = added(&first.node_ids(), &second.node_ids());
    assert!(!new_nodes.is_empty());
    assert!(new_nodes.iter().all(|x| *x < lowest_node));
    let mut known = first.edge_ids();
    known.push(5);
    let dummies = added(&known, &second.edge_ids());
    assert!(!dummies.is_empty());
    assert!(dummies.iter().all(|x| *x < lowest_edge));
}

#[test]
fn arrow_new_then_the_factories_are_asked_for_generators_for_no_ids() {
    let (edge_factory, edge_requests) = RecordingIdGeneratorFactory::new();
    let (node_factory, node_requests) = RecordingIdGeneratorFactory::new();

    ArrowBuilder::new(edge_factory, node_factory);

    let edge_requests = edge_requests.lock().unwrap();
    let node_requests = node_requests.lock().unwrap();
    assert_eq!(edge_requests.len(), 1);
    assert!(edge_requests[0].is_empty());
    assert_eq!(node_requests.len(), 1);
    assert!(node_requests[0].is_empty());
}

#[test]
fn arrow_from_graph_then_the_factories_are_asked_for_generators_for_the_ids_of_its_edges_and_nodes()
{
    let mut first = ArrowBuilder::new(
        NextIdGeneratorFactory::new(100),
        NextIdGeneratorFactory::new(200),
    );
    add_arrow_plan(&mut first);
    let graph = first.to_graph().unwrap();
    let expected_edges = sorted(graph.edges.iter().map(|e| e.id()).collect());
    let expected_nodes = sorted(graph.nodes.iter().map(|n| n.id()).collect());
    let (edge_factory, edge_requests) = RecordingIdGeneratorFactory::new();
    let (node_factory, node_requests) = RecordingIdGeneratorFactory::new();

    ArrowBuilder::from_graph(graph, edge_factory, node_factory).unwrap();

    let edge_requests = edge_requests.lock().unwrap();
    let node_requests = node_requests.lock().unwrap();
    assert_eq!(edge_requests.len(), 1);
    assert!(!edge_requests[0].is_empty());
    assert_eq!(sorted(edge_requests[0].clone()), expected_edges);
    assert_eq!(node_requests.len(), 1);
    assert!(!node_requests[0].is_empty());
    assert_eq!(sorted(node_requests[0].clone()), expected_nodes);
}
