//! Ports of the integer cases from `KeyExtensionsTests.cs`. The C# `Guid` cases
//! (`Next` yields a different value, `Previous` throws `InvalidOperationException`)
//! have no counterpart: this port's `Key` trait is implemented only for the
//! primitive integer types, so there is no non-incrementable key to exercise.

use zametek_maths_graphs_primitives::Key;

#[test]
fn next_type_int_then_value_is_incremented_by_one() {
    let first = 41;
    let second = first.next();
    assert_eq!(second, first + 1);
}

#[test]
fn previous_type_int_then_value_is_decremented_by_one() {
    let first = 41;
    let second = first.previous();
    assert_eq!(second, first - 1);
}
