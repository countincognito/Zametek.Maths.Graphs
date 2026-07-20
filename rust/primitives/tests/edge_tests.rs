//! Ports of `.../Entities/EdgeTests.cs`. The null-content and null-equality
//! guards have no Rust counterpart (the argument is non-nullable by type) and
//! are omitted.

use zametek_maths_graphs_primitives::{Edge, Event};

type Ed = Edge<i32, Event<i32>>;

#[test]
fn edge_given_id_then_delegates_to_content_id() {
    let edge = Ed::new(Event::new(7));
    assert_eq!(edge.id(), 7);
}

#[test]
fn edge_given_equals_when_same_id_then_equal_even_if_content_differs() {
    let edge1 = Ed::new(Event::with_times(7, Some(1), Some(2)));
    let edge2 = Ed::new(Event::with_times(7, Some(3), Some(4)));
    assert_eq!(edge1, edge2);
}

#[test]
fn edge_given_equals_when_different_id_then_not_equal() {
    let edge1 = Ed::new(Event::new(7));
    let edge2 = Ed::new(Event::new(8));
    assert_ne!(edge1, edge2);
}

#[test]
fn edge_given_clone_then_content_is_cloned() {
    let mut content = Event::with_times(7, Some(1), Some(2));
    content.set_as_removable();
    let edge = Ed::new(content);

    let clone = edge.clone();

    assert_eq!(clone.id(), 7);
    assert_eq!(clone.content.earliest_finish_time, Some(1));
    assert_eq!(clone.content.latest_finish_time, Some(2));
    assert!(clone.content.can_be_removed());
}
