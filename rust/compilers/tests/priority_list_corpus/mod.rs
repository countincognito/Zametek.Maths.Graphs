//! Port of `PriorityListCorpus.cs`.
//!
//! A deterministic corpus of graphs used to prove that changes to the
//! priority-list calculation (or to the critical-path engine underneath it)
//! leave its output untouched. The priority list decides which activity is
//! offered a resource first, so it determines the final schedule: an
//! optimisation that alters it is a behaviour change, not an optimisation.
//!
//! The shapes are chosen to stress the parts of the calculation most likely to
//! reorder under a rewrite - depth, width, density, and above all ties, since
//! activities sharing a total slack value are separated only by the secondary
//! ordering.
//!
//! Determinism matters more than statistical quality here, because the output is
//! compared against a committed baseline. The generator therefore uses its own
//! xorshift generator, specified down to the arithmetic, so that this port
//! produces byte-identical graphs to the C# original - which is what lets the
//! two languages be checked against the same baseline file.

#![allow(dead_code)]

use indexmap::IndexSet;
use zametek_maths_graphs_compilers::{NextIdGenerator, VertexGraphBuilder};
use zametek_maths_graphs_primitives::DependentActivity;

pub type Builder = VertexGraphBuilder<i32, i32, i32>;

pub struct Case {
    pub name: String,
    pub graph_builder: Builder,
}

impl Case {
    fn new(name: String, graph_builder: Builder) -> Self {
        Self {
            name,
            graph_builder,
        }
    }
}

/// Small xorshift32 generator: fully specified, so the corpus is reproducible on
/// any runtime and the committed baseline stays valid.
///
/// The shifts wrap in exactly the way C#'s `uint` does, so the sequence matches
/// the original generator value for value.
struct DeterministicRandom {
    state: u32,
}

impl DeterministicRandom {
    fn new(seed: u32) -> Self {
        Self {
            state: if seed == 0 { 1 } else { seed },
        }
    }

    fn next_u32(&mut self) -> u32 {
        let mut x = self.state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        self.state = x;
        x
    }

    fn next(&mut self, max_exclusive: i32) -> i32 {
        (self.next_u32() % (max_exclusive as u32)) as i32
    }

    fn next_between(&mut self, min_inclusive: i32, max_exclusive: i32) -> i32 {
        min_inclusive + self.next(max_exclusive - min_inclusive)
    }

    fn next_chance(&mut self, percent: i32) -> bool {
        self.next(100) < percent
    }
}

pub fn generate() -> Vec<Case> {
    let mut cases = Vec::new();

    for size in [5, 12, 40, 90] {
        cases.push(Case::new(format!("chain-{size}"), chain(size, false)));
        cases.push(Case::new(format!("chain-tied-{size}"), chain(size, true)));
        cases.push(Case::new(format!("fan-out-{size}"), fan_out(size)));
        cases.push(Case::new(format!("fan-in-{size}"), fan_in(size)));
        cases.push(Case::new(format!("diamonds-{size}"), diamonds(size)));
    }

    for size in [20, 60, 120] {
        for layers in [3, 6, 12] {
            cases.push(Case::new(
                format!("layered-{size}-{layers}"),
                layered_core(size, layers, (size * 31 + layers) as u32, false),
            ));
            cases.push(Case::new(
                format!("layered-tied-{size}-{layers}"),
                layered_core(size, layers, (size * 17 + layers) as u32, true),
            ));
        }
    }

    for size in [15, 45, 100] {
        for density in [10, 30, 60] {
            cases.push(Case::new(
                format!("random-{size}-{density}"),
                random_dag(size, density, (size * 13 + density) as u32),
            ));
        }
    }

    for size in [10, 35, 80] {
        cases.push(Case::new(
            format!("disconnected-{size}"),
            disconnected(size, (size * 7) as u32),
        ));
        cases.push(Case::new(
            format!("zero-durations-{size}"),
            with_zero_durations(size, (size * 11) as u32),
        ));
        cases.push(Case::new(
            format!("constrained-{size}"),
            with_time_constraints(size, (size * 19) as u32),
        ));
    }

    cases
}

// -- Shapes ------------------------------------------------------------------

fn create_builder() -> Builder {
    VertexGraphBuilder::new(NextIdGenerator::new(0))
}

fn deps(ids: impl IntoIterator<Item = i32>) -> IndexSet<i32> {
    ids.into_iter().collect()
}

/// 1 -> 2 -> 3 -> ... : maximum depth, minimum width.
fn chain(size: i32, uniform_duration: bool) -> Builder {
    let mut graph_builder = create_builder();
    for id in 1..=size {
        let duration = if uniform_duration { 5 } else { 1 + (id % 9) };
        let dependencies = if id == 1 { deps([]) } else { deps([id - 1]) };
        graph_builder
            .add_activity_with_dependencies(DependentActivity::new(id, duration), dependencies);
    }
    graph_builder
}

/// One root that everything else depends on: minimum depth, maximum width, and
/// every dependent shares the same slack, so the whole graph is one large tie.
fn fan_out(size: i32) -> Builder {
    let mut graph_builder = create_builder();
    graph_builder.add_activity_with_dependencies(DependentActivity::new(1, 5), deps([]));
    for id in 2..=size {
        graph_builder.add_activity_with_dependencies(DependentActivity::new(id, 5), deps([1]));
    }
    graph_builder
}

/// Many independent activities converging on a single sink.
fn fan_in(size: i32) -> Builder {
    let mut graph_builder = create_builder();
    let mut sources: IndexSet<i32> = IndexSet::new();
    for id in 1..size {
        graph_builder
            .add_activity_with_dependencies(DependentActivity::new(id, 1 + (id % 7)), deps([]));
        sources.insert(id);
    }
    graph_builder.add_activity_with_dependencies(DependentActivity::new(size, 4), sources);
    graph_builder
}

/// Repeated split-and-join: the two middle activities of each diamond tie on slack.
fn diamonds(size: i32) -> Builder {
    let mut graph_builder = create_builder();
    let mut id = 1;
    let mut previous_join = 0;
    while id + 3 <= size {
        let split = id;
        id += 1;
        let left = id;
        id += 1;
        let right = id;
        id += 1;
        let join = id;
        id += 1;
        let split_dependencies = if previous_join == 0 {
            deps([])
        } else {
            deps([previous_join])
        };
        graph_builder
            .add_activity_with_dependencies(DependentActivity::new(split, 2), split_dependencies);
        graph_builder
            .add_activity_with_dependencies(DependentActivity::new(left, 3), deps([split]));
        graph_builder
            .add_activity_with_dependencies(DependentActivity::new(right, 3), deps([split]));
        graph_builder
            .add_activity_with_dependencies(DependentActivity::new(join, 2), deps([left, right]));
        previous_join = join;
    }
    while id <= size {
        let dependencies = if previous_join == 0 {
            deps([])
        } else {
            deps([previous_join])
        };
        graph_builder.add_activity_with_dependencies(DependentActivity::new(id, 2), dependencies);
        previous_join = id;
        id += 1;
    }
    graph_builder
}

/// Layered DAG: each activity depends on up to two activities in the layer
/// above. With `uniform_duration` every duration is identical so that total
/// slack ties are widespread - the case most likely to expose an ordering change.
fn layered_core(size: i32, layers: i32, seed: u32, uniform_duration: bool) -> Builder {
    let mut graph_builder = create_builder();
    let mut rng = DeterministicRandom::new(seed);
    let per_layer = (size / layers).max(1);

    for id in 1..=size {
        let layer = (id - 1) / per_layer;
        let mut dependencies: IndexSet<i32> = IndexSet::new();
        if layer > 0 {
            let previous_start = ((layer - 1) * per_layer) + 1;
            let previous_end = (layer * per_layer).min(size);
            if previous_end >= previous_start {
                dependencies.insert(rng.next_between(previous_start, previous_end + 1));
                dependencies.insert(rng.next_between(previous_start, previous_end + 1));
            }
        }
        let duration = if uniform_duration {
            4
        } else {
            rng.next_between(1, 10)
        };
        graph_builder
            .add_activity_with_dependencies(DependentActivity::new(id, duration), dependencies);
    }
    graph_builder
}

/// Every activity may depend on any lower-numbered activity, which keeps the
/// graph acyclic while varying density.
fn random_dag(size: i32, density_percent: i32, seed: u32) -> Builder {
    let mut graph_builder = create_builder();
    let mut rng = DeterministicRandom::new(seed);
    for id in 1..=size {
        let mut dependencies: IndexSet<i32> = IndexSet::new();
        for candidate in 1..id {
            if rng.next_chance(density_percent) {
                dependencies.insert(candidate);
            }
        }
        let duration = rng.next_between(1, 8);
        graph_builder
            .add_activity_with_dependencies(DependentActivity::new(id, duration), dependencies);
    }
    graph_builder
}

/// Several independent components plus isolated activities, so the calculation
/// has to cope with more than one critical path at a time.
fn disconnected(size: i32, seed: u32) -> Builder {
    let mut graph_builder = create_builder();
    let mut rng = DeterministicRandom::new(seed);
    let mut component_start = 1;
    for id in 1..=size {
        let starts_new_component = id == component_start;
        let mut dependencies: IndexSet<i32> = IndexSet::new();
        if !starts_new_component && rng.next_chance(70) {
            dependencies.insert(rng.next_between(component_start, id));
        }
        let duration = rng.next_between(1, 8);
        graph_builder
            .add_activity_with_dependencies(DependentActivity::new(id, duration), dependencies);
        if rng.next_chance(25) {
            component_start = id + 1;
        }
    }
    graph_builder
}

/// Zero-duration activities count as dummies, so they never enter the priority
/// list - but they still carry dependencies through the graph.
fn with_zero_durations(size: i32, seed: u32) -> Builder {
    let mut graph_builder = create_builder();
    let mut rng = DeterministicRandom::new(seed);
    for id in 1..=size {
        let mut dependencies: IndexSet<i32> = IndexSet::new();
        if id > 1 {
            dependencies.insert(rng.next_between(1, id));
        }
        let duration = if rng.next_chance(30) {
            0
        } else {
            rng.next_between(1, 8)
        };
        graph_builder
            .add_activity_with_dependencies(DependentActivity::new(id, duration), dependencies);
    }
    graph_builder
}

/// Time constraints alter earliest start and latest finish times, which feed the
/// slack values the selection is based on. The constraints are kept mutually
/// consistent so that the graph still compiles.
fn with_time_constraints(size: i32, seed: u32) -> Builder {
    let mut graph_builder = create_builder();
    let mut rng = DeterministicRandom::new(seed);
    for id in 1..=size {
        let mut dependencies: IndexSet<i32> = IndexSet::new();
        if id > 1 {
            dependencies.insert(rng.next_between(1, id));
        }
        let duration = rng.next_between(1, 8);
        let mut activity = DependentActivity::new(id, duration);

        // Never set minimum_free_slack and maximum_latest_finish_time together -
        // the compiler rejects that combination as a contradictory constraint.
        let choice = rng.next(4);
        if choice == 0 {
            activity.minimum_earliest_start_time = Some(rng.next_between(0, 20));
        } else if choice == 1 {
            activity.minimum_free_slack = Some(rng.next_between(0, 5));
        } else if choice == 2 {
            // Generous enough that it stays satisfiable for a graph this size.
            activity.maximum_latest_finish_time = Some(5_000 + rng.next_between(0, 500));
        }

        graph_builder.add_activity_with_dependencies(activity, dependencies);
    }
    graph_builder
}
