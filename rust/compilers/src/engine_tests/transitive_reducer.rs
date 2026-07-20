//! Ports of `VertexTransitiveReducerTests.cs` and `ArrowTransitiveReducerTests.cs`.

use super::common::{
    arrow_edge, arrow_node, vertex_edge, vertex_node, vertex_removable_edge, AState, VState,
};
use crate::contracts::{IArrowTransitiveReducer, IVertexTransitiveReducer};
use crate::{
    ArrowTarjanStronglyConnectedComponentsFinder, ArrowTransitiveReducer, DummyEdgeOrchestrator,
    VertexTarjanStronglyConnectedComponentsFinder, VertexTransitiveReducer,
};
use zametek_maths_graphs_primitives::NodeType;

// -- Vertex ------------------------------------------------------------------

fn v_scc() -> VertexTarjanStronglyConnectedComponentsFinder {
    VertexTarjanStronglyConnectedComponentsFinder
}

fn build_linear_vertex_state() -> VState {
    let mut state = VState::new();
    vertex_node(&mut state, 1, NodeType::Start, 1, false);
    vertex_node(&mut state, 2, NodeType::Normal, 1, false);
    vertex_node(&mut state, 3, NodeType::End, 1, false);
    vertex_edge(&mut state, 100, 1, 2);
    vertex_edge(&mut state, 101, 2, 3);
    state
}

fn build_linear_vertex_state_with_circular_dependency() -> VState {
    let mut state = VState::new();
    vertex_node(&mut state, 1, NodeType::Start, 1, false);
    vertex_node(&mut state, 2, NodeType::Normal, 1, false);
    vertex_node(&mut state, 3, NodeType::Normal, 1, false);
    vertex_node(&mut state, 4, NodeType::End, 1, false);
    vertex_edge(&mut state, 100, 1, 2);
    vertex_edge(&mut state, 101, 2, 3);
    vertex_edge(&mut state, 102, 3, 2);
    vertex_edge(&mut state, 103, 3, 4);
    state
}

fn build_vertex_state_with_redundant_edge() -> VState {
    // 1 -> 2 -> 3 plus a redundant 1 -> 3; all removable.
    let mut state = VState::new();
    vertex_node(&mut state, 1, NodeType::Start, 1, true);
    vertex_node(&mut state, 2, NodeType::Normal, 1, true);
    vertex_node(&mut state, 3, NodeType::End, 1, true);
    vertex_removable_edge(&mut state, 100, 1, 2);
    vertex_removable_edge(&mut state, 101, 2, 3);
    vertex_removable_edge(&mut state, 102, 1, 3);
    state
}

#[test]
fn vertex_reducer_ancestor_lookup_with_unsatisfied_dependencies_then_none() {
    let mut state = build_linear_vertex_state();
    state.add_unsatisfied_successor(7777, 99);
    assert!(VertexTransitiveReducer
        .get_ancestor_nodes_lookup(&state, &v_scc())
        .is_none());
}

#[test]
fn vertex_reducer_ancestor_lookup_with_circular_dependencies_then_none() {
    let state = build_linear_vertex_state_with_circular_dependency();
    assert!(VertexTransitiveReducer
        .get_ancestor_nodes_lookup(&state, &v_scc())
        .is_none());
}

#[test]
fn vertex_reducer_ancestor_lookup_with_linear_graph_then_correct_lookup() {
    let state = build_linear_vertex_state();
    let output = VertexTransitiveReducer
        .get_ancestor_nodes_lookup(&state, &v_scc())
        .unwrap();
    let ancestors_of_3 = output.get(&3).unwrap();
    assert!(ancestors_of_3.contains(&1));
    assert!(ancestors_of_3.contains(&2));
}

#[test]
fn vertex_reducer_reduce_graph_with_unsatisfied_dependencies_then_false() {
    let mut state = build_linear_vertex_state();
    state.add_unsatisfied_successor(7777, 99);
    assert!(!VertexTransitiveReducer.reduce_graph(&mut state, &v_scc()));
}

#[test]
fn vertex_reducer_reduce_graph_with_redundant_edge_then_removes_it() {
    let mut state = build_vertex_state_with_redundant_edge();
    let initial = state.edge_ids().len();
    assert!(VertexTransitiveReducer.reduce_graph(&mut state, &v_scc()));
    assert!(state.edge_ids().len() < initial);
}

// -- Arrow -------------------------------------------------------------------

fn a_scc() -> ArrowTarjanStronglyConnectedComponentsFinder {
    ArrowTarjanStronglyConnectedComponentsFinder
}

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

fn build_linear_arrow_state_with_circular_dependency() -> AState {
    let mut state = AState::new();
    arrow_node(&mut state, 1, NodeType::Start);
    arrow_node(&mut state, 2, NodeType::Normal);
    arrow_node(&mut state, 3, NodeType::Normal);
    arrow_node(&mut state, 4, NodeType::End);
    state.start_node_id = Some(1);
    state.end_node_id = Some(4);
    arrow_edge(&mut state, 11, 1, 2, 3, false);
    arrow_edge(&mut state, 12, 2, 3, 3, false);
    arrow_edge(&mut state, 13, 3, 2, 3, false);
    arrow_edge(&mut state, 14, 3, 4, 2, false);
    state
}

#[test]
fn arrow_reducer_ancestor_lookup_with_unsatisfied_dependencies_then_none() {
    let mut state = build_linear_arrow_state();
    state.add_unsatisfied_successor(7777, 99);
    assert!(ArrowTransitiveReducer
        .get_ancestor_nodes_lookup(&state, &a_scc())
        .is_none());
}

#[test]
fn arrow_reducer_ancestor_lookup_with_circular_dependencies_then_none() {
    let state = build_linear_arrow_state_with_circular_dependency();
    assert!(ArrowTransitiveReducer
        .get_ancestor_nodes_lookup(&state, &a_scc())
        .is_none());
}

#[test]
fn arrow_reducer_ancestor_lookup_with_linear_graph_then_correct_lookup() {
    let state = build_linear_arrow_state();
    let output = ArrowTransitiveReducer
        .get_ancestor_nodes_lookup(&state, &a_scc())
        .unwrap();
    let ancestors_of_end = output.get(&3).unwrap();
    assert!(ancestors_of_end.contains(&1));
    assert!(ancestors_of_end.contains(&2));
}

#[test]
fn arrow_reducer_reduce_graph_with_unsatisfied_dependencies_then_false() {
    let mut state = build_linear_arrow_state();
    state.add_unsatisfied_successor(7777, 99);
    assert!(!ArrowTransitiveReducer
        .reduce_graph(&mut state, &a_scc(), &DummyEdgeOrchestrator)
        .unwrap());
}

#[test]
fn arrow_reducer_reduce_graph_with_valid_graph_then_true() {
    let mut state = build_linear_arrow_state();
    assert!(ArrowTransitiveReducer
        .reduce_graph(&mut state, &a_scc(), &DummyEdgeOrchestrator)
        .unwrap());
}
