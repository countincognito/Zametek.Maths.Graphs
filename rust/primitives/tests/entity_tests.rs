//! Ports of representative tests from Zametek.Maths.Graphs.Primitives.Tests.

use zametek_maths_graphs_primitives::{Activity, DependentActivity, Edge, Event, Node, NodeType};

#[test]
fn activity_given_duration_then_derived_times_follow_cs_semantics() {
    let mut activity: Activity<i32, i32, i32> = Activity::new(1, 10);
    assert_eq!(activity.id(), 1);
    assert_eq!(activity.duration, 10);
    assert!(!activity.is_dummy());
    assert_eq!(activity.total_slack(), None);
    assert_eq!(activity.earliest_finish_time(), None);
    assert_eq!(activity.latest_start_time(), None);
    assert!(!activity.is_critical());

    activity.earliest_start_time = Some(5);
    assert_eq!(activity.earliest_finish_time(), Some(15));

    activity.latest_finish_time = Some(20);
    assert_eq!(activity.latest_start_time(), Some(10));
    assert_eq!(activity.total_slack(), Some(5));

    activity.free_slack = Some(2);
    assert_eq!(activity.interfering_slack(), Some(3));
    assert!(!activity.is_critical());

    activity.latest_finish_time = Some(15);
    assert_eq!(activity.total_slack(), Some(0));
    assert!(activity.is_critical());
}

#[test]
fn activity_given_zero_duration_then_is_dummy() {
    let activity: Activity<i32, i32, i32> = Activity::new(1, 0);
    assert!(activity.is_dummy());
}

#[test]
fn activity_given_removability_flag_then_can_be_toggled() {
    let mut activity: Activity<i32, i32, i32> = Activity::new_removable(1, 10, true);
    assert!(activity.can_be_removed());
    activity.set_as_read_only();
    assert!(!activity.can_be_removed());
    activity.set_as_removable();
    assert!(activity.can_be_removed());
}

#[test]
fn dependent_activity_given_dependencies_then_stored_and_base_accessible() {
    let activity: DependentActivity<i32, i32, i32> =
        DependentActivity::with_dependencies(3, 7, [1, 2]);
    assert_eq!(activity.id(), 3);
    assert_eq!(activity.duration, 7);
    assert_eq!(activity.dependencies.len(), 2);
    assert!(activity.dependencies.contains(&1));
    assert!(activity.dependencies.contains(&2));
    assert!(activity.planning_dependencies.is_empty());
    assert!(activity.resource_dependencies.is_empty());
    assert!(activity.successors.is_empty());
}

#[test]
fn event_given_times_then_stored() {
    let mut event: Event<i32> = Event::with_times(1, Some(2), Some(3));
    assert_eq!(event.id(), 1);
    assert_eq!(event.earliest_finish_time, Some(2));
    assert_eq!(event.latest_finish_time, Some(3));
    assert!(!event.can_be_removed());
    event.set_as_removable();
    assert!(event.can_be_removed());
}

#[test]
fn edge_equality_is_by_id_only() {
    let edge1: Edge<i32, Event<i32>> = Edge::new(Event::with_times(1, Some(5), None));
    let edge2: Edge<i32, Event<i32>> = Edge::new(Event::new(1));
    let edge3: Edge<i32, Event<i32>> = Edge::new(Event::new(2));
    assert_eq!(edge1, edge2);
    assert_ne!(edge1, edge3);
}

#[test]
fn node_given_start_or_isolated_then_incoming_edges_error() {
    let start: Node<i32, Event<i32>> = Node::with_type(NodeType::Start, Event::new(1));
    assert!(start.incoming_edges().is_err());
    assert!(start.outgoing_edges().is_ok());

    let isolated: Node<i32, Event<i32>> = Node::with_type(NodeType::Isolated, Event::new(2));
    assert!(isolated.incoming_edges().is_err());
    assert!(isolated.outgoing_edges().is_err());
}

#[test]
fn node_given_end_or_isolated_then_outgoing_edges_error() {
    let end: Node<i32, Event<i32>> = Node::with_type(NodeType::End, Event::new(1));
    assert!(end.outgoing_edges().is_err());
    assert!(end.incoming_edges().is_ok());

    let normal: Node<i32, Event<i32>> = Node::new(Event::new(2));
    assert!(normal.incoming_edges().is_ok());
    assert!(normal.outgoing_edges().is_ok());
}

#[test]
fn node_equality_compares_id_type_and_edge_sets() {
    let mut a: Node<i32, Event<i32>> = Node::new(Event::new(1));
    let mut b: Node<i32, Event<i32>> = Node::new(Event::new(1));
    assert_eq!(a, b);

    a.incoming.insert(10);
    assert_ne!(a, b);
    b.incoming.insert(10);
    assert_eq!(a, b);

    a.set_node_type(NodeType::End);
    assert_ne!(a, b);
}
