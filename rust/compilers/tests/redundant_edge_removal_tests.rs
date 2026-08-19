//! Port of `RedundantEdgeRemovalTests.cs`.
//!
//! Regression tests for the walk that orders dummy edges for removal
//! (`get_dummy_edges_in_descending_order`, reached through
//! `remove_redundant_dummy_edges`, which `calculate_critical_path` runs first).
//!
//! The C# original carried two defects that this port never had, and these tests
//! exist so it never acquires them. It descended through every edge *occurrence*
//! rather than every edge, so it enumerated each distinct path from the start
//! node instead of visiting each edge once: on the branching graph below - 150
//! activities over 30 layers, 297 event nodes and 416 edges - that is 3.5 x 10^8
//! steps per walk, and the three walks the removal performs took a minute
//! between them. It was also recursive, so its depth was the length of the
//! longest path, which a deep chain overflows.
//!
//! The C# tests measure allocation, which is deterministic and separates the two
//! regimes by three orders of magnitude either side of the threshold. Here the
//! established idiom is the watchdog used by the scheduler guardrail tests -
//! counting allocations would mean a global allocator, and that is per test
//! binary rather than per test - and it serves as well, because the guarded walk
//! finishes in milliseconds against minutes without it.

use indexmap::IndexSet;
use std::sync::mpsc;
use std::thread;
use std::time::Duration;
use zametek_maths_graphs_compilers::{ArrowGraphBuilder, NextIdGenerator};
use zametek_maths_graphs_primitives::DependentActivity;

type Act = DependentActivity<i32, i32, i32>;

/// Enough depth for the path count to explode, while staying a graph the limits
/// accept.
const BRANCHING_ACTIVITY_COUNT: i32 = 150;
const BRANCHING_LAYER_COUNT: i32 = 30;

/// The scale the other deep-graph tests use; an arrow graph mints roughly two
/// event nodes per activity, so this is a walk about 20,000 deep.
const CHAIN_LENGTH: i32 = 10_000;

/// Runs the given operation on a worker so that a regression fails the test after
/// a timeout instead of running for minutes.
///
/// The worker is deliberately detached rather than joined: if the walk regresses
/// it takes far longer than any test should wait, and the test must fail on the
/// timeout instead of blocking for it.
fn run_with_watchdog<T, F>(func: F) -> T
where
    F: FnOnce() -> T + Send + 'static,
    T: Send + 'static,
{
    let (sender, receiver) = mpsc::channel();
    thread::spawn(move || {
        let _ = sender.send(func());
    });
    match receiver.recv_timeout(Duration::from_secs(30)) {
        Ok(value) => value,
        Err(_) => panic!(
            "the redundant-edge walk did not finish within 30 seconds, which means it is enumerating paths rather than edges"
        ),
    }
}

/// Each activity depends on two in the previous layer, which is what gives the
/// graph more distinct start-to-end paths than it has edges.
fn build_branching_graph() -> ArrowGraphBuilder<i32, i32, i32> {
    let mut builder = ArrowGraphBuilder::<i32, i32, i32>::new(
        NextIdGenerator::new(1_000_000),
        NextIdGenerator::new(0),
    );

    let per_layer = BRANCHING_ACTIVITY_COUNT / BRANCHING_LAYER_COUNT;
    for id in 1..=BRANCHING_ACTIVITY_COUNT {
        let layer = (id - 1) / per_layer;
        let mut dependencies: IndexSet<i32> = IndexSet::new();
        if layer > 0 {
            let previous_start = ((layer - 1) * per_layer) + 1;
            dependencies.insert(previous_start + ((id * 7) % per_layer));
            dependencies.insert(previous_start + ((id * 13) % per_layer));
        }
        builder.add_activity_with_dependencies(Act::new(id, 1 + (id % 9)), dependencies);
    }

    builder
}

#[test]
fn arrow_branching_graph_when_calculating_critical_path_then_does_not_enumerate_every_path() {
    run_with_watchdog(|| {
        let mut builder = build_branching_graph();
        assert!(builder.transitive_reduction().expect("reduction succeeds"));

        builder
            .calculate_critical_path()
            .expect("critical path succeeds");
    });
}

#[test]
fn arrow_very_deep_chain_when_calculating_critical_path_then_reaches_the_far_end() {
    let mut builder = ArrowGraphBuilder::<i32, i32, i32>::new(
        NextIdGenerator::new(CHAIN_LENGTH),
        NextIdGenerator::new(0),
    );

    builder.add_activity(Act::new(1, 1));
    for id in 2..=CHAIN_LENGTH {
        builder.add_activity_with_dependencies(Act::new(id, 1), IndexSet::from([id - 1]));
    }

    // Reducing first is what the compiler does, and it is deliberate here rather
    // than incidental: calculating the critical path on a graph that was never
    // reduced is slow for an unrelated reason, recorded against
    // `redirect_dummy_edges`.
    assert!(builder.transitive_reduction().expect("reduction succeeds"));

    builder
        .calculate_critical_path()
        .expect("critical path succeeds");

    // A chain of unit durations, so the last activity starts once every earlier
    // one has finished - which also shows the walk reached the far end of the
    // graph.
    let last = builder
        .activities()
        .find(|x| x.id() == CHAIN_LENGTH)
        .expect("the final activity is present");
    assert_eq!(last.earliest_start_time, Some(CHAIN_LENGTH - 1));
}
