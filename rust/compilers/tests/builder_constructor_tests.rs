//! Port of `GraphBuilderConstructorTests.cs`. The builders have constructors
//! that take only the ID generators (`new`, and `from_graph` when they load a
//! graph), and they take their standard engines from the engines bundle. These
//! tests show that they use the ID generators that they are given, also when they
//! load a graph: the events (vertex), and the dummy activities and the nodes
//! (arrow), that they make get their IDs from those generators.

use indexmap::IndexSet;
use zametek_maths_graphs_compilers::primitives::DependentActivity;
use zametek_maths_graphs_compilers::{ArrowGraphBuilder, NextIdGenerator, VertexGraphBuilder};

type Act = DependentActivity<i32, i32, i32>;
type VertexBuilder = VertexGraphBuilder<i32, i32, i32>;
type ArrowBuilder = ArrowGraphBuilder<i32, i32, i32>;

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

#[test]
fn vertex_new_given_an_id_generator_then_the_events_have_ids_from_it() {
    let mut builder = VertexBuilder::new(NextIdGenerator::new(1000));

    add_vertex_plan(&mut builder);

    assert!(!builder.event_ids().is_empty());
    assert!(builder.event_ids().iter().all(|x| *x > 1000));
}

#[test]
fn vertex_from_graph_given_a_graph_and_an_id_generator_then_the_events_added_later_have_ids_from_it(
) {
    let mut first = VertexBuilder::new(NextIdGenerator::new(0));
    add_vertex_plan(&mut first);

    let mut second =
        VertexBuilder::from_graph(first.to_graph().unwrap(), NextIdGenerator::new(1000)).unwrap();
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
fn arrow_new_given_id_generators_then_the_dummy_activities_and_the_nodes_have_ids_from_them() {
    let mut builder = ArrowBuilder::new(NextIdGenerator::new(1000), NextIdGenerator::new(2000));

    add_arrow_plan(&mut builder);

    assert!(!builder.node_ids().is_empty());
    assert!(builder.node_ids().iter().all(|x| *x > 2000));
    let dummies = added(&[1, 2, 3, 4], &builder.edge_ids());
    assert!(!dummies.is_empty());
    assert!(dummies.iter().all(|x| *x > 1000));
}

#[test]
fn arrow_from_graph_given_a_graph_and_id_generators_then_the_dummy_activities_and_the_nodes_added_later_have_ids_from_them(
) {
    let mut first = ArrowBuilder::new(NextIdGenerator::new(100), NextIdGenerator::new(200));
    add_arrow_plan(&mut first);

    let mut second = ArrowBuilder::from_graph(
        first.to_graph().unwrap(),
        NextIdGenerator::new(1000),
        NextIdGenerator::new(2000),
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
