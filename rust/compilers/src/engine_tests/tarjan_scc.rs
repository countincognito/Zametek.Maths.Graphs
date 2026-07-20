//! Ports of `VertexTarjanStronglyConnectedComponentsFinderTests.cs` and
//! `ArrowTarjanStronglyConnectedComponentsFinderTests.cs`.

use super::common::{arrow_edge, arrow_node, vertex_edge, vertex_node, AState, VState};
use crate::contracts::{
    IArrowStronglyConnectedComponentsFinder, IVertexStronglyConnectedComponentsFinder,
};
use crate::{
    ArrowTarjanStronglyConnectedComponentsFinder, VertexTarjanStronglyConnectedComponentsFinder,
};
use zametek_maths_graphs_primitives::NodeType;

// -- Vertex ------------------------------------------------------------------

fn build_acyclic_vertex_state() -> VState {
    let mut state = VState::new();
    vertex_node(&mut state, 1, NodeType::Start, 5, false);
    vertex_node(&mut state, 2, NodeType::End, 5, false);
    vertex_edge(&mut state, 100, 1, 2);
    state
}

fn build_cyclic_vertex_state() -> VState {
    let mut state = VState::new();
    vertex_node(&mut state, 1, NodeType::Normal, 1, false);
    vertex_node(&mut state, 2, NodeType::Normal, 1, false);
    vertex_node(&mut state, 3, NodeType::Normal, 1, false);
    vertex_edge(&mut state, 100, 1, 2);
    vertex_edge(&mut state, 101, 2, 3);
    vertex_edge(&mut state, 102, 3, 1);
    state
}

fn build_cyclic_vertex_state_with_removable_node() -> VState {
    let mut state = VState::new();
    vertex_node(&mut state, 1, NodeType::Normal, 1, false);
    vertex_node(&mut state, 2, NodeType::Normal, 1, true);
    vertex_node(&mut state, 3, NodeType::Normal, 1, false);
    vertex_edge(&mut state, 100, 1, 2);
    vertex_edge(&mut state, 101, 2, 3);
    vertex_edge(&mut state, 102, 3, 1);
    state
}

#[test]
fn vertex_scc_with_empty_state_then_returns_empty() {
    let state = VState::new();
    let output = VertexTarjanStronglyConnectedComponentsFinder
        .find_strongly_connected_components(&state, false);
    assert!(output.is_empty());
}

#[test]
fn vertex_scc_with_acyclic_graph_then_no_cycle_has_more_than_one_node() {
    let state = build_acyclic_vertex_state();
    let output = VertexTarjanStronglyConnectedComponentsFinder
        .find_strongly_connected_components(&state, false);
    assert!(output.iter().all(|c| c.dependencies.len() <= 1));
}

#[test]
fn vertex_scc_with_cyclic_graph_then_cycle_contains_all_nodes() {
    let state = build_cyclic_vertex_state();
    let output = VertexTarjanStronglyConnectedComponentsFinder
        .find_strongly_connected_components(&state, false);
    let cycle = output.iter().find(|c| c.dependencies.len() > 1).unwrap();
    assert!(cycle.dependencies.contains(&1));
    assert!(cycle.dependencies.contains(&2));
    assert!(cycle.dependencies.contains(&3));
}

#[test]
fn vertex_scc_ignoring_dummies_with_removable_node_then_excludes_removable() {
    let state = build_cyclic_vertex_state_with_removable_node();
    let output = VertexTarjanStronglyConnectedComponentsFinder
        .find_strongly_connected_components(&state, true);
    let cycle = output.iter().find(|c| c.dependencies.len() > 1).unwrap();
    assert!(!cycle.dependencies.contains(&2));
    assert!(cycle.dependencies.contains(&1));
    assert!(cycle.dependencies.contains(&3));
}

#[test]
fn vertex_scc_not_ignoring_dummies_with_removable_node_then_includes_removable() {
    let state = build_cyclic_vertex_state_with_removable_node();
    let output = VertexTarjanStronglyConnectedComponentsFinder
        .find_strongly_connected_components(&state, false);
    let cycle = output.iter().find(|c| c.dependencies.len() > 1).unwrap();
    assert!(cycle.dependencies.contains(&2));
    assert!(cycle.dependencies.contains(&1));
    assert!(cycle.dependencies.contains(&3));
}

// -- Arrow -------------------------------------------------------------------

fn build_acyclic_arrow_state() -> AState {
    let mut state = AState::new();
    arrow_node(&mut state, 1, NodeType::Start);
    arrow_node(&mut state, 2, NodeType::End);
    state.start_node_id = Some(1);
    state.end_node_id = Some(2);
    arrow_edge(&mut state, 11, 1, 2, 5, false);
    state
}

fn build_cyclic_arrow_state() -> AState {
    let mut state = AState::new();
    arrow_node(&mut state, 1, NodeType::Normal);
    arrow_node(&mut state, 2, NodeType::Normal);
    arrow_node(&mut state, 3, NodeType::Normal);
    arrow_edge(&mut state, 11, 1, 2, 1, false);
    arrow_edge(&mut state, 12, 2, 3, 1, false);
    arrow_edge(&mut state, 13, 3, 1, 1, false);
    state
}

fn build_cyclic_arrow_state_with_removable_edge() -> AState {
    let mut state = AState::new();
    arrow_node(&mut state, 1, NodeType::Normal);
    arrow_node(&mut state, 2, NodeType::Normal);
    arrow_node(&mut state, 3, NodeType::Normal);
    arrow_edge(&mut state, 11, 1, 2, 1, false);
    arrow_edge(&mut state, 12, 2, 3, 0, true);
    arrow_edge(&mut state, 13, 3, 1, 1, false);
    state
}

#[test]
fn arrow_scc_with_empty_state_then_returns_empty() {
    let state = AState::new();
    let output = ArrowTarjanStronglyConnectedComponentsFinder
        .find_strongly_connected_components(&state, false);
    assert!(output.is_empty());
}

#[test]
fn arrow_scc_with_acyclic_graph_then_no_cycle_has_more_than_one_edge() {
    let state = build_acyclic_arrow_state();
    let output = ArrowTarjanStronglyConnectedComponentsFinder
        .find_strongly_connected_components(&state, false);
    assert!(output.iter().all(|c| c.dependencies.len() <= 1));
}

#[test]
fn arrow_scc_with_cyclic_graph_then_cycle_contains_all_edges() {
    let state = build_cyclic_arrow_state();
    let output = ArrowTarjanStronglyConnectedComponentsFinder
        .find_strongly_connected_components(&state, false);
    let cycle = output.iter().find(|c| c.dependencies.len() > 1).unwrap();
    assert!(cycle.dependencies.contains(&11));
    assert!(cycle.dependencies.contains(&12));
    assert!(cycle.dependencies.contains(&13));
}

#[test]
fn arrow_scc_ignoring_dummies_with_removable_edge_then_excludes_removable() {
    let state = build_cyclic_arrow_state_with_removable_edge();
    let output = ArrowTarjanStronglyConnectedComponentsFinder
        .find_strongly_connected_components(&state, true);
    let cycle = output.iter().find(|c| c.dependencies.len() > 1).unwrap();
    assert!(!cycle.dependencies.contains(&12));
    assert!(cycle.dependencies.contains(&11));
    assert!(cycle.dependencies.contains(&13));
}

#[test]
fn arrow_scc_not_ignoring_dummies_with_removable_edge_then_includes_removable() {
    let state = build_cyclic_arrow_state_with_removable_edge();
    let output = ArrowTarjanStronglyConnectedComponentsFinder
        .find_strongly_connected_components(&state, false);
    let cycle = output.iter().find(|c| c.dependencies.len() > 1).unwrap();
    assert!(cycle.dependencies.contains(&12));
    assert!(cycle.dependencies.contains(&11));
    assert!(cycle.dependencies.contains(&13));
}
