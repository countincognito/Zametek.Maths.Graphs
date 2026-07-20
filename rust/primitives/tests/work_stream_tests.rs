//! Ports of `.../Entities/WorkStreamTests.cs`.

use zametek_maths_graphs_primitives::WorkStream;

#[test]
fn work_stream_given_ctor_then_properties_set() {
    let work_stream = WorkStream::<i32>::new(1, "phase1", true);
    assert_eq!(work_stream.id, 1);
    assert_eq!(work_stream.name, "phase1");
    assert!(work_stream.is_phase);
}

#[test]
fn work_stream_given_clone_then_all_properties_preserved() {
    let work_stream = WorkStream::<i32>::new(1, "phase1", true);
    let clone = work_stream.clone();
    assert_eq!(clone.id, 1);
    assert_eq!(clone.name, "phase1");
    assert!(clone.is_phase);
}
