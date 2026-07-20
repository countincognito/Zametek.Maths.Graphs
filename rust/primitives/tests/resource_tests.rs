//! Ports of `.../Entities/ResourceTests.cs`. The null-phases constructor guard
//! has no Rust counterpart (the argument is non-nullable by type) and is
//! omitted.

use indexmap::IndexSet;
use zametek_maths_graphs_primitives::{InterActivityAllocationType, Resource};

type Res = Resource<i32, i32>;

#[test]
fn resource_given_ctor_then_properties_set() {
    let resource = Res::new(
        1,
        Some("R1".to_string()),
        true,
        true,
        InterActivityAllocationType::Indirect,
        2.5,
        3.5,
        7,
        [11, 12],
    );
    assert_eq!(resource.id, 1);
    assert_eq!(resource.name.as_deref(), Some("R1"));
    assert!(resource.is_explicit_target);
    assert!(resource.is_inactive);
    assert_eq!(
        resource.inter_activity_allocation_type,
        InterActivityAllocationType::Indirect
    );
    assert_eq!(resource.unit_cost, 2.5);
    assert_eq!(resource.unit_billing, 3.5);
    assert_eq!(resource.allocation_order, 7);
    assert_eq!(resource.inter_activity_phases, IndexSet::from([11, 12]));
}

#[test]
fn resource_given_clone_then_all_properties_preserved() {
    let resource = Res::new(
        1,
        Some("R1".to_string()),
        true,
        false,
        InterActivityAllocationType::Direct,
        2.5,
        3.5,
        7,
        [11],
    );

    let clone = resource.clone();

    assert_eq!(clone.id, 1);
    assert_eq!(clone.name.as_deref(), Some("R1"));
    assert!(clone.is_explicit_target);
    assert!(!clone.is_inactive);
    assert_eq!(
        clone.inter_activity_allocation_type,
        InterActivityAllocationType::Direct
    );
    assert_eq!(clone.unit_cost, 2.5);
    assert_eq!(clone.unit_billing, 3.5);
    assert_eq!(clone.allocation_order, 7);
    assert_eq!(clone.inter_activity_phases, IndexSet::from([11]));
}

#[test]
fn resource_given_clone_then_phases_are_independent_copies() {
    let resource = Res::new(
        1,
        Some("R1".to_string()),
        false,
        false,
        InterActivityAllocationType::None,
        1.0,
        1.0,
        0,
        [11],
    );

    let mut clone = resource.clone();
    clone.inter_activity_phases.insert(12);

    assert_eq!(resource.inter_activity_phases.len(), 1);
    assert_eq!(clone.inter_activity_phases.len(), 2);
}
