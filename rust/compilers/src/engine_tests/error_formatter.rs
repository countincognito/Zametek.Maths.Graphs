//! Ports of `GraphCompilationErrorFormatterTests.cs`. The C# null-argument
//! variants coincide with the empty-slice cases in Rust and are folded into them.

use crate::error_formatter;
use zametek_maths_graphs_primitives::{
    CircularDependency, DependentActivity, InvalidConstraint, UnavailableResources,
};

type Dep = DependentActivity<i32, i32, i32>;

#[test]
fn invalid_dependencies_with_empty_dependencies_then_empty() {
    let output =
        error_formatter::build_invalid_dependencies_error_message::<i32, i32, i32>(&[], &[]);
    assert!(output.is_empty());
}

#[test]
fn invalid_dependencies_with_empty_activities_then_empty() {
    let output =
        error_formatter::build_invalid_dependencies_error_message::<i32, i32, i32>(&[99], &[]);
    assert!(output.is_empty());
}

#[test]
fn invalid_dependencies_with_referencing_activity_then_message_contains_both_ids() {
    let activity = Dep::with_dependencies(1, 10, [99]);
    let output = error_formatter::build_invalid_dependencies_error_message(&[99], &[&activity]);
    assert!(!output.is_empty());
    assert!(output.contains("99"));
    assert!(output.contains('1'));
}

#[test]
fn invalid_dependencies_with_planning_dependency_reference_then_message_contains_both_ids() {
    let activity = Dep::with_planning_dependencies(2, 10, [], [88]);
    let output = error_formatter::build_invalid_dependencies_error_message(&[88], &[&activity]);
    assert!(!output.is_empty());
    assert!(output.contains("88"));
    assert!(output.contains('2'));
}

#[test]
fn circular_dependencies_with_empty_input_then_empty() {
    let output = error_formatter::build_circular_dependencies_error_message::<i32>(&[]);
    assert!(output.is_empty());
}

#[test]
fn circular_dependencies_with_circular_deps_then_message_contains_arrow_separated_ids() {
    let circular = CircularDependency::new([1, 2, 3]);
    let output = error_formatter::build_circular_dependencies_error_message(&[circular]);
    assert!(!output.is_empty());
    assert!(output.contains("->"));
    assert!(output.contains('1'));
    assert!(output.contains('2'));
    assert!(output.contains('3'));
}

#[test]
fn invalid_constraints_with_empty_input_then_empty() {
    let output = error_formatter::build_invalid_constraints_error_message::<i32>(&[]);
    assert!(output.is_empty());
}

#[test]
fn invalid_constraints_with_constraints_then_message_contains_id_and_message() {
    let constraint = InvalidConstraint::new(7, "some-constraint-message");
    let output = error_formatter::build_invalid_constraints_error_message(&[constraint]);
    assert!(!output.is_empty());
    assert!(output.contains('7'));
    assert!(output.contains("some-constraint-message"));
}

#[test]
fn unavailable_resources_with_empty_input_then_empty() {
    let output = error_formatter::build_unavailable_resources_error_message::<i32, i32>(&[]);
    assert!(output.is_empty());
}

#[test]
fn unavailable_resources_with_entries_then_message_contains_activity_and_resource_ids() {
    let unavailable = UnavailableResources::new(5, [11, 22]);
    let output = error_formatter::build_unavailable_resources_error_message(&[unavailable]);
    assert!(!output.is_empty());
    assert!(output.contains('5'));
    assert!(output.contains("11"));
    assert!(output.contains("22"));
}
