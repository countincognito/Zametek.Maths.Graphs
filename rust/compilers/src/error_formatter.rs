use crate::messages;
use std::fmt::Write;
use zametek_maths_graphs_primitives::{
    CircularDependency, DependentActivity, InvalidConstraint, Key, UnavailableResources,
};

/// Builds the human-readable error messages for graph compilation errors -
/// the counterpart of the C# `GraphCompilationErrorFormatter`. Lines are
/// joined with `\n`.
pub(crate) fn build_invalid_dependencies_error_message<K: Key, R: Key, W: Key>(
    invalid_dependencies: &[K],
    activities: &[&DependentActivity<K, R, W>],
) -> String {
    if invalid_dependencies.is_empty() || activities.is_empty() {
        return String::new();
    }
    let mut output = String::new();
    let _ = writeln!(output, "{}", messages::MSG_INVALID_DEPENDENCIES);
    for invalid_dependency in invalid_dependencies {
        let mut referencing_ids: Vec<K> = activities
            .iter()
            .filter(|x| {
                x.dependencies.contains(invalid_dependency)
                    || x.planning_dependencies.contains(invalid_dependency)
            })
            .map(|x| x.id())
            .collect();
        referencing_ids.sort();
        let joined = referencing_ids
            .iter()
            .map(|id| id.to_string())
            .collect::<Vec<_>>()
            .join(", ");
        let _ = writeln!(
            output,
            "{} {} {}",
            invalid_dependency,
            messages::MSG_IS_INVALID_BUT_REFERENCED_BY,
            joined
        );
    }
    output
}

pub(crate) fn build_circular_dependencies_error_message<K: Key>(
    circular_dependencies: &[CircularDependency<K>],
) -> String {
    if circular_dependencies.is_empty() {
        return String::new();
    }
    let mut output = String::new();
    let _ = writeln!(output, "{}", messages::MSG_CIRCULAR_DEPENDENCIES);
    for circular_dependency in circular_dependencies {
        let joined = circular_dependency
            .dependencies
            .iter()
            .map(|id| id.to_string())
            .collect::<Vec<_>>()
            .join(" -> ");
        let _ = writeln!(output, "{joined}");
    }
    output
}

pub(crate) fn build_invalid_constraints_error_message<K: Key>(
    invalid_constraints: &[InvalidConstraint<K>],
) -> String {
    if invalid_constraints.is_empty() {
        return String::new();
    }
    let mut output = String::new();
    let _ = writeln!(output, "{}", messages::MSG_INVALID_CONSTRAINTS);
    for invalid_constraint in invalid_constraints {
        let _ = writeln!(
            output,
            "{} -> {}",
            invalid_constraint.id, invalid_constraint.message
        );
    }
    output
}

pub(crate) fn build_unavailable_resources_error_message<K: Key, R: Key>(
    unavailable_resources_set: &[UnavailableResources<K, R>],
) -> String {
    if unavailable_resources_set.is_empty() {
        return String::new();
    }
    let mut output = String::new();
    let _ = writeln!(output, "{}", messages::MSG_UNAVAILABLE_RESOURCES);
    for unavailable_resources in unavailable_resources_set {
        let mut resource_ids: Vec<R> = unavailable_resources.resource_ids.iter().copied().collect();
        resource_ids.sort();
        let joined = resource_ids
            .iter()
            .map(|id| id.to_string())
            .collect::<Vec<_>>()
            .join(", ");
        let _ = writeln!(output, "{} -> {}", unavailable_resources.id, joined);
    }
    output
}
