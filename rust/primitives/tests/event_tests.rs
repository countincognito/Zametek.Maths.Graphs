//! Ports of `.../Entities/EventTests.cs`.

use zametek_maths_graphs_primitives::Event;

#[test]
fn event_given_ctor_with_id_only_then_times_are_none_and_not_removable() {
    let ev = Event::<i32>::new(5);
    assert_eq!(ev.id(), 5);
    assert_eq!(ev.earliest_finish_time, None);
    assert_eq!(ev.latest_finish_time, None);
    assert!(!ev.can_be_removed());
}

#[test]
fn event_given_ctor_with_times_then_times_are_set() {
    let ev = Event::<i32>::with_times(5, Some(3), Some(7));
    assert_eq!(ev.earliest_finish_time, Some(3));
    assert_eq!(ev.latest_finish_time, Some(7));
}

#[test]
fn event_given_set_as_removable_then_can_be_removed_is_true() {
    let mut ev = Event::<i32>::new(1);
    ev.set_as_removable();
    assert!(ev.can_be_removed());
}

#[test]
fn event_given_set_as_read_only_then_can_be_removed_is_false() {
    let mut ev = Event::<i32>::new(1);
    ev.set_as_removable();
    ev.set_as_read_only();
    assert!(!ev.can_be_removed());
}

#[test]
fn event_given_clone_then_all_properties_preserved() {
    let ev = Event::<i32>::with_times(5, Some(3), Some(7));
    let clone = ev.clone();
    assert_eq!(clone.id(), 5);
    assert_eq!(clone.earliest_finish_time, Some(3));
    assert_eq!(clone.latest_finish_time, Some(7));
    assert!(!clone.can_be_removed());
}

// Regression: clone must preserve `can_be_removed` (a past bug reset it to
// false, silently breaking transitive reduction on cloned vertex builders).
#[test]
fn event_given_clone_when_removable_then_clone_is_also_removable() {
    let mut ev = Event::<i32>::with_times(5, Some(3), Some(7));
    ev.set_as_removable();
    let clone = ev.clone();
    assert!(clone.can_be_removed());
}
