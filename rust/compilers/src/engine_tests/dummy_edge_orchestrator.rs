//! Ports of `DummyEdgeOrchestratorTests.cs`. The C# `GetDummyEdgesInDescendingOrder`
//! test targets an internal helper that is not on the Rust orchestrator trait (a
//! crate-private free function, exercised indirectly via `RemoveRedundantDummyEdges`);
//! it is omitted here.

use super::common::{arrow_edge, arrow_node, AState};
use crate::contracts::IDummyEdgeOrchestrator;
use crate::{
    ArrowTarjanStronglyConnectedComponentsFinder, DummyActivityGenerator, DummyEdgeOrchestrator,
    NextIdGenerator,
};
use zametek_maths_graphs_primitives::NodeType;

fn a_scc() -> ArrowTarjanStronglyConnectedComponentsFinder {
    ArrowTarjanStronglyConnectedComponentsFinder
}

fn build_arrow_state_with_dummies() -> AState {
    // Start -> [real 11, dur 3] -> N1 -> [dummy 12, dur 0] -> End
    let mut state = AState::new();
    arrow_node(&mut state, 1, NodeType::Start);
    arrow_node(&mut state, 2, NodeType::Normal);
    arrow_node(&mut state, 3, NodeType::End);
    state.start_node_id = Some(1);
    state.end_node_id = Some(3);
    arrow_edge(&mut state, 11, 1, 2, 3, false);
    arrow_edge(&mut state, 12, 2, 3, 0, true);
    state
}

fn build_arrow_state_with_dummies_and_circular_dependency() -> AState {
    let mut state = AState::new();
    arrow_node(&mut state, 1, NodeType::Start);
    arrow_node(&mut state, 2, NodeType::Normal);
    arrow_node(&mut state, 3, NodeType::Normal);
    arrow_node(&mut state, 4, NodeType::End);
    state.start_node_id = Some(1);
    state.end_node_id = Some(4);
    arrow_edge(&mut state, 11, 1, 2, 0, true);
    arrow_edge(&mut state, 12, 2, 3, 3, false);
    arrow_edge(&mut state, 13, 3, 2, 3, false);
    arrow_edge(&mut state, 14, 3, 4, 0, true);
    state
}

#[test]
fn orchestrator_connect_with_dummy_edge_then_adds_dummy_edge_between_nodes() {
    let mut state = AState::new();
    arrow_node(&mut state, 1, NodeType::Normal);
    arrow_node(&mut state, 2, NodeType::Normal);

    const DUMMY_EDGE_ID: i32 = 555;
    let mut edge_gen = NextIdGenerator::new(DUMMY_EDGE_ID - 1);
    DummyEdgeOrchestrator.connect_with_dummy_edge(
        &mut state,
        &mut edge_gen,
        &DummyActivityGenerator,
        1,
        2,
    );

    assert!(state.contains_edge(DUMMY_EDGE_ID));
    assert!(state.edge(DUMMY_EDGE_ID).unwrap().content.is_dummy());
    assert!(state.edge(DUMMY_EDGE_ID).unwrap().content.can_be_removed());
    assert!(state.node(1).unwrap().outgoing.contains(&DUMMY_EDGE_ID));
    assert!(state.node(2).unwrap().incoming.contains(&DUMMY_EDGE_ID));
    assert_eq!(state.edge_tail_node_id(DUMMY_EDGE_ID), Some(1));
    assert_eq!(state.edge_head_node_id(DUMMY_EDGE_ID), Some(2));
}

#[test]
fn orchestrator_remove_dummy_activity_with_nonexistent_activity_then_false() {
    let mut state = AState::new();
    assert!(!DummyEdgeOrchestrator
        .remove_dummy_activity(&mut state, 999)
        .unwrap());
}

#[test]
fn orchestrator_remove_dummy_activity_with_non_dummy_activity_then_false() {
    let mut state = AState::new();
    arrow_node(&mut state, 1, NodeType::Normal);
    arrow_node(&mut state, 2, NodeType::Normal);
    arrow_edge(&mut state, 100, 1, 2, 5, false);
    assert!(!DummyEdgeOrchestrator
        .remove_dummy_activity(&mut state, 100)
        .unwrap());
}

#[test]
fn orchestrator_remove_dummy_activity_with_non_removable_dummy_then_false() {
    let mut state = AState::new();
    arrow_node(&mut state, 1, NodeType::Normal);
    arrow_node(&mut state, 2, NodeType::Normal);
    arrow_edge(&mut state, 100, 1, 2, 0, false);
    assert!(!DummyEdgeOrchestrator
        .remove_dummy_activity(&mut state, 100)
        .unwrap());
}

#[test]
fn orchestrator_redirect_dummy_edges_with_unsatisfied_dependencies_then_false() {
    let mut state = build_arrow_state_with_dummies();
    state.add_unsatisfied_successor(7777, 99);
    assert!(!DummyEdgeOrchestrator
        .redirect_dummy_edges(&mut state, &a_scc())
        .unwrap());
}

#[test]
fn orchestrator_redirect_dummy_edges_with_circular_dependencies_then_false() {
    let mut state = build_arrow_state_with_dummies_and_circular_dependency();
    assert!(!DummyEdgeOrchestrator
        .redirect_dummy_edges(&mut state, &a_scc())
        .unwrap());
}

#[test]
fn orchestrator_remove_redundant_dummy_edges_with_unsatisfied_dependencies_then_false() {
    let mut state = build_arrow_state_with_dummies();
    state.add_unsatisfied_successor(7777, 99);
    assert!(!DummyEdgeOrchestrator
        .remove_redundant_dummy_edges(&mut state, &a_scc())
        .unwrap());
}

#[test]
fn orchestrator_remove_redundant_dummy_edges_with_circular_dependencies_then_false() {
    let mut state = build_arrow_state_with_dummies_and_circular_dependency();
    assert!(!DummyEdgeOrchestrator
        .remove_redundant_dummy_edges(&mut state, &a_scc())
        .unwrap());
}
