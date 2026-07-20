//! Ports of `VertexCriticalPathEngineTests.cs` and `ArrowCriticalPathEngineTests.cs`.

use super::common::{arrow_edge, arrow_node, vertex_edge, vertex_node, AState, VState};
use crate::contracts::{IArrowCriticalPathEngine, IVertexCriticalPathEngine};
use crate::{ArrowCriticalPathEngine, VertexCriticalPathEngine};
use zametek_maths_graphs_primitives::{InvalidConstraint, NodeType};

fn bad_constraints() -> Vec<InvalidConstraint<i32>> {
    vec![InvalidConstraint::new(99, "some-constraint")]
}

// -- Vertex ------------------------------------------------------------------

fn build_linear_vertex_state() -> VState {
    let mut state = VState::new();
    vertex_node(&mut state, 1, NodeType::Start, 3, false);
    vertex_node(&mut state, 2, NodeType::End, 2, false);
    vertex_edge(&mut state, 100, 1, 2);
    state
}

fn build_linear_vertex_state_with_forward_flow() -> VState {
    let mut state = build_linear_vertex_state();
    VertexCriticalPathEngine
        .calculate_critical_path_forward_flow(&mut state, &[], false)
        .unwrap();
    state
}

fn build_isolated_and_end_vertex_state() -> VState {
    let mut state = VState::new();
    vertex_node(&mut state, 1, NodeType::Isolated, 4, false);
    vertex_node(&mut state, 2, NodeType::Start, 5, false);
    vertex_node(&mut state, 3, NodeType::End, 5, false);
    vertex_edge(&mut state, 100, 2, 3);
    state
}

#[test]
fn vertex_forward_flow_with_invalid_constraints_then_returns_false() {
    let mut state = build_linear_vertex_state();
    let result = VertexCriticalPathEngine
        .calculate_critical_path_forward_flow(&mut state, &bad_constraints(), false)
        .unwrap();
    assert!(!result);
}

#[test]
fn vertex_forward_flow_with_linear_graph_then_computes_earliest_start_times() {
    let mut state = build_linear_vertex_state();
    let result = VertexCriticalPathEngine
        .calculate_critical_path_forward_flow(&mut state, &[], false)
        .unwrap();
    assert!(result);
    assert_eq!(state.node(1).unwrap().content.earliest_start_time, Some(0));
    assert_eq!(state.node(2).unwrap().content.earliest_start_time, Some(3));
    assert_eq!(
        state.node(1).unwrap().content.earliest_finish_time(),
        Some(3)
    );
    assert_eq!(
        state.node(2).unwrap().content.earliest_finish_time(),
        Some(5)
    );
}

#[test]
fn vertex_backward_flow_with_invalid_constraints_then_returns_false() {
    let mut state = build_linear_vertex_state_with_forward_flow();
    let result = VertexCriticalPathEngine
        .calculate_critical_path_backward_flow(&mut state, &bad_constraints(), false)
        .unwrap();
    assert!(!result);
}

#[test]
fn vertex_backward_flow_with_missing_earliest_finish_times_then_returns_false() {
    let mut state = build_linear_vertex_state();
    let result = VertexCriticalPathEngine
        .calculate_critical_path_backward_flow(&mut state, &[], false)
        .unwrap();
    assert!(!result);
}

#[test]
fn vertex_backward_flow_with_linear_graph_then_computes_latest_finish_times_and_free_slack() {
    let mut state = build_linear_vertex_state_with_forward_flow();
    let result = VertexCriticalPathEngine
        .calculate_critical_path_backward_flow(&mut state, &[], false)
        .unwrap();
    assert!(result);
    assert_eq!(state.node(2).unwrap().content.latest_finish_time, Some(5));
    assert_eq!(state.node(1).unwrap().content.latest_finish_time, Some(3));
    assert_eq!(state.node(1).unwrap().content.free_slack, Some(0));
    assert_eq!(state.node(2).unwrap().content.free_slack, Some(0));
}

#[test]
fn vertex_back_fill_with_invalid_constraints_then_returns_false() {
    let mut state = build_isolated_and_end_vertex_state();
    let result = VertexCriticalPathEngine.back_fill_isolated_nodes(&mut state, &bad_constraints());
    assert!(!result);
}

#[test]
fn vertex_back_fill_with_end_node_missing_latest_finish_time_then_returns_false() {
    let mut state = build_isolated_and_end_vertex_state();
    let result = VertexCriticalPathEngine.back_fill_isolated_nodes(&mut state, &[]);
    assert!(!result);
}

#[test]
fn vertex_back_fill_with_end_and_isolated_nodes_then_sets_latest_finish_time_and_free_slack() {
    let mut state = build_isolated_and_end_vertex_state();
    {
        let end = state.node_mut(3).unwrap();
        end.content.earliest_start_time = Some(0);
        end.content.latest_finish_time = Some(10);
    }
    {
        let isolated = state.node_mut(1).unwrap();
        isolated.content.earliest_start_time = Some(0);
        isolated.content.latest_finish_time = Some(5);
    }

    let result = VertexCriticalPathEngine.back_fill_isolated_nodes(&mut state, &[]);

    assert!(result);
    assert_eq!(state.node(1).unwrap().content.latest_finish_time, Some(10));
    assert_eq!(state.node(1).unwrap().content.free_slack, Some(10 - 4));
}

// -- Arrow -------------------------------------------------------------------

fn build_linear_arrow_state() -> AState {
    let mut state = AState::new();
    arrow_node(&mut state, 1, NodeType::Start);
    arrow_node(&mut state, 2, NodeType::Normal);
    arrow_node(&mut state, 3, NodeType::End);
    state.start_node_id = Some(1);
    state.end_node_id = Some(3);
    arrow_edge(&mut state, 11, 1, 2, 3, false);
    arrow_edge(&mut state, 12, 2, 3, 2, false);
    state
}

fn build_linear_arrow_state_with_earliest_finish_times() -> AState {
    let mut state = build_linear_arrow_state();
    ArrowCriticalPathEngine
        .calculate_event_earliest_finish_times(&mut state, &[], false)
        .unwrap();
    state
}

fn build_linear_arrow_state_with_latest_finish_times() -> AState {
    let mut state = build_linear_arrow_state_with_earliest_finish_times();
    ArrowCriticalPathEngine
        .calculate_event_latest_finish_times(&mut state, &[], false)
        .unwrap();
    state
}

#[test]
fn arrow_earliest_finish_times_with_missing_start_node_then_errors() {
    let mut state = AState::new();
    assert!(ArrowCriticalPathEngine
        .calculate_event_earliest_finish_times(&mut state, &[], false)
        .is_err());
}

#[test]
fn arrow_earliest_finish_times_with_missing_end_node_then_errors() {
    let mut state = AState::new();
    arrow_node(&mut state, 1, NodeType::Start);
    state.start_node_id = Some(1);
    assert!(ArrowCriticalPathEngine
        .calculate_event_earliest_finish_times(&mut state, &[], false)
        .is_err());
}

#[test]
fn arrow_earliest_finish_times_with_invalid_constraints_then_returns_false() {
    let mut state = build_linear_arrow_state();
    let result = ArrowCriticalPathEngine
        .calculate_event_earliest_finish_times(&mut state, &bad_constraints(), false)
        .unwrap();
    assert!(!result);
}

#[test]
fn arrow_earliest_finish_times_with_linear_graph_then_computes_earliest_finish_times() {
    let mut state = build_linear_arrow_state();
    let result = ArrowCriticalPathEngine
        .calculate_event_earliest_finish_times(&mut state, &[], false)
        .unwrap();
    assert!(result);
    assert_eq!(state.node(1).unwrap().content.earliest_finish_time, Some(0));
    assert_eq!(state.node(2).unwrap().content.earliest_finish_time, Some(3));
    assert_eq!(state.node(3).unwrap().content.earliest_finish_time, Some(5));
}

#[test]
fn arrow_latest_finish_times_with_missing_end_node_then_errors() {
    let mut state = AState::new();
    assert!(ArrowCriticalPathEngine
        .calculate_event_latest_finish_times(&mut state, &[], false)
        .is_err());
}

#[test]
fn arrow_latest_finish_times_with_invalid_constraints_then_returns_false() {
    let mut state = build_linear_arrow_state_with_earliest_finish_times();
    let result = ArrowCriticalPathEngine
        .calculate_event_latest_finish_times(&mut state, &bad_constraints(), false)
        .unwrap();
    assert!(!result);
}

#[test]
fn arrow_latest_finish_times_with_events_missing_earliest_finish_times_then_returns_false() {
    let mut state = build_linear_arrow_state();
    let result = ArrowCriticalPathEngine
        .calculate_event_latest_finish_times(&mut state, &[], false)
        .unwrap();
    assert!(!result);
}

#[test]
fn arrow_latest_finish_times_with_linear_graph_then_computes_latest_finish_times() {
    let mut state = build_linear_arrow_state_with_earliest_finish_times();
    let result = ArrowCriticalPathEngine
        .calculate_event_latest_finish_times(&mut state, &[], false)
        .unwrap();
    assert!(result);
    assert_eq!(state.node(3).unwrap().content.latest_finish_time, Some(5));
    assert_eq!(state.node(2).unwrap().content.latest_finish_time, Some(3));
    assert_eq!(state.node(1).unwrap().content.latest_finish_time, Some(0));
}

#[test]
fn arrow_critical_path_variables_with_invalid_constraints_then_returns_false() {
    let mut state = build_linear_arrow_state_with_latest_finish_times();
    let result = ArrowCriticalPathEngine
        .calculate_critical_path_variables(&mut state, &bad_constraints())
        .unwrap();
    assert!(!result);
}

#[test]
fn arrow_critical_path_variables_with_events_missing_earliest_finish_times_then_returns_false() {
    let mut state = build_linear_arrow_state();
    let result = ArrowCriticalPathEngine
        .calculate_critical_path_variables(&mut state, &[])
        .unwrap();
    assert!(!result);
}

#[test]
fn arrow_critical_path_variables_with_linear_graph_then_computes_edge_times_and_free_slack() {
    let mut state = build_linear_arrow_state_with_latest_finish_times();
    let result = ArrowCriticalPathEngine
        .calculate_critical_path_variables(&mut state, &[])
        .unwrap();
    assert!(result);
    assert_eq!(state.edge(11).unwrap().content.earliest_start_time, Some(0));
    assert_eq!(state.edge(11).unwrap().content.latest_finish_time, Some(3));
    assert_eq!(state.edge(11).unwrap().content.free_slack, Some(0));
    assert_eq!(state.edge(12).unwrap().content.earliest_start_time, Some(3));
    assert_eq!(state.edge(12).unwrap().content.latest_finish_time, Some(5));
    assert_eq!(state.edge(12).unwrap().content.free_slack, Some(0));
}
