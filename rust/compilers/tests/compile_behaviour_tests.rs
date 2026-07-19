//! Ports of `Zametek.Maths.Graphs.Compilers.Tests/Compilers/CompileBehaviourTests.cs`:
//! end-to-end behaviour guards — critical-path order-independence (the shuffle
//! hook), scheduling through a dummy chain, and degenerate inputs.

use indexmap::IndexSet;
use zametek_maths_graphs_compilers::{
    NextIdGenerator, PreviousIdGenerator, VertexGraphBuilder, VertexGraphCompiler,
};
use zametek_maths_graphs_primitives::{DependentActivity, InterActivityAllocationType, Resource};

type Compiler = VertexGraphCompiler<i32, i32, i32>;
type Builder = VertexGraphBuilder<i32, i32, i32>;
type Act = DependentActivity<i32, i32, i32>;

// A small deterministic RNG for building the random DAGs (the C# test uses
// `new Random(seed)`; only determinism matters, not the exact sequence).
struct Lcg {
    state: u64,
}

impl Lcg {
    fn new(seed: u64) -> Self {
        Self {
            state: seed
                .wrapping_mul(6364136223846793005)
                .wrapping_add(1442695040888963407),
        }
    }

    fn next_u32(&mut self) -> u32 {
        self.state = self
            .state
            .wrapping_mul(6364136223846793005)
            .wrapping_add(1442695040888963407);
        (self.state >> 33) as u32
    }

    fn next_range(&mut self, low: i32, high: i32) -> i32 {
        low + (self.next_u32() % (high - low) as u32) as i32
    }

    fn next_bool(&mut self, numerator: u32, denominator: u32) -> bool {
        self.next_u32() % denominator < numerator
    }
}

fn build_and_calculate(specs: &[(i32, i32, Vec<i32>)], shuffle: bool) -> Builder {
    let mut builder = Builder::new(NextIdGenerator::new(0));
    builder.shuffle_processing_order = shuffle;
    for (id, duration, deps) in specs {
        builder.add_activity_with_dependencies(
            Act::new(*id, *duration),
            deps.iter().copied().collect::<IndexSet<i32>>(),
        );
    }
    builder.calculate_critical_path().unwrap();
    builder
}

#[test]
fn vertex_graph_builder_given_random_dags_then_critical_path_is_independent_of_processing_order() {
    for seed in 0..50u64 {
        let mut rng = Lcg::new(seed);
        let activity_count = rng.next_range(10, 40);

        // Build the specs once so both builders receive identical input;
        // dependencies only ever point at lower IDs, guaranteeing an acyclic graph.
        let mut specs: Vec<(i32, i32, Vec<i32>)> = Vec::new();
        for id in 1..=activity_count {
            let mut deps = Vec::new();
            for dep in 1..id {
                if rng.next_bool(1, 5) {
                    deps.push(dep);
                }
            }
            specs.push((id, rng.next_range(1, 6), deps));
        }

        let ordered = build_and_calculate(&specs, false);
        let shuffled = build_and_calculate(&specs, true);

        for (id, _, _) in &specs {
            let a = ordered.activity(*id).unwrap();
            let b = shuffled.activity(*id).unwrap();
            assert_eq!(
                b.earliest_start_time, a.earliest_start_time,
                "seed {seed}, activity {id} EST"
            );
            assert_eq!(
                b.latest_finish_time, a.latest_finish_time,
                "seed {seed}, activity {id} LFT"
            );
            assert_eq!(
                b.free_slack, a.free_slack,
                "seed {seed}, activity {id} FreeSlack"
            );
            assert_eq!(
                b.total_slack(),
                a.total_slack(),
                "seed {seed}, activity {id} TotalSlack"
            );
        }
    }
}

#[test]
fn vertex_graph_compiler_given_dependency_through_dummy_chain_then_schedule_respects_it() {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 5)); // A
    compiler.add_activity(Act::with_dependencies(2, 0, [1])); // B (zero-duration link) dep A
    compiler.add_activity(Act::with_dependencies(3, 5, [2])); // C dep B (strong dependency: A)

    let resource = Resource::new(
        1,
        Some("R1".to_string()),
        false,
        false,
        InterActivityAllocationType::None,
        0.0,
        0.0,
        0,
        [],
    );
    let compilation = compiler.compile_with_resources(&[resource]).unwrap();

    assert!(compilation.compilation_errors.is_empty());

    let a = compilation
        .dependent_activities
        .iter()
        .find(|x| x.id() == 1)
        .unwrap();
    let c = compilation
        .dependent_activities
        .iter()
        .find(|x| x.id() == 3)
        .unwrap();

    assert_eq!(a.earliest_finish_time(), Some(5));
    // C's only real dependency is A, reached transitively through the dummy B;
    // it must not start before A finishes.
    assert_eq!(c.earliest_start_time, Some(5));
}

#[test]
fn vertex_graph_compiler_given_empty_graph_then_compiles_to_empty_result() {
    let mut compiler = Compiler::new();

    let compilation = compiler.compile().unwrap();

    assert!(compilation.compilation_errors.is_empty());
    assert!(compilation.dependent_activities.is_empty());
    assert!(compilation.resource_schedules.is_empty());
}

#[test]
fn vertex_graph_compiler_given_single_isolated_activity_then_compiles_with_zero_start() {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 5));

    let compilation = compiler.compile().unwrap();

    assert!(compilation.compilation_errors.is_empty());
    assert_eq!(compilation.dependent_activities.len(), 1);
    let a = &compilation.dependent_activities[0];
    assert_eq!(a.earliest_start_time, Some(0));
    assert_eq!(a.earliest_finish_time(), Some(5));
    assert_eq!(a.latest_finish_time, Some(5));
}

#[test]
fn vertex_graph_compiler_given_all_zero_duration_chain_then_compiles_without_error() {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 0));
    compiler.add_activity(Act::with_dependencies(2, 0, [1]));
    compiler.add_activity(Act::with_dependencies(3, 0, [2]));

    let compilation = compiler.compile().unwrap();

    assert!(compilation.compilation_errors.is_empty());
    for activity in &compilation.dependent_activities {
        assert_eq!(activity.earliest_start_time, Some(0));
        assert_eq!(activity.earliest_finish_time(), Some(0));
    }
}

#[test]
fn vertex_graph_builder_given_clone_then_graph_equal() {
    let mut builder = Builder::new(PreviousIdGenerator::new(0));
    builder.add_activity(Act::new(1, 3));
    builder.add_activity_with_dependencies(Act::new(2, 5), IndexSet::from([1]));
    builder.add_activity_with_dependencies(Act::new(3, 2), IndexSet::from([1, 2]));

    let mut clone = builder.clone_builder().unwrap();

    let graph_a = builder.to_graph().unwrap();
    let graph_b = clone.to_graph().unwrap();
    assert_eq!(graph_a, graph_b);
}

#[test]
fn vertex_graph_builder_given_ancestor_lookup_then_full_transitive_closure() {
    let mut builder = Builder::new(PreviousIdGenerator::new(0));
    builder.add_activity(Act::new(1, 3));
    builder.add_activity_with_dependencies(Act::new(2, 5), IndexSet::from([1]));
    builder.add_activity_with_dependencies(Act::new(3, 2), IndexSet::from([2]));

    let lookup = builder.get_ancestor_nodes_lookup().unwrap();
    assert!(lookup[&1].is_empty());
    assert_eq!(lookup[&2].iter().copied().collect::<Vec<i32>>(), vec![1]);
    let mut ancestors3: Vec<i32> = lookup[&3].iter().copied().collect();
    ancestors3.sort();
    assert_eq!(ancestors3, vec![1, 2]);
}

#[test]
fn vertex_graph_builder_given_unsatisfied_dependencies_then_no_ancestor_lookup() {
    let mut builder = Builder::new(PreviousIdGenerator::new(0));
    builder.add_activity_with_dependencies(Act::new(2, 5), IndexSet::from([1]));

    assert!(!builder.all_dependencies_satisfied());
    assert_eq!(builder.invalid_dependencies(), vec![1]);
    assert!(builder.get_ancestor_nodes_lookup().is_none());
}

#[test]
fn vertex_graph_builder_given_transitive_reduction_then_redundant_edge_removed() {
    let mut builder = Builder::new(PreviousIdGenerator::new(0));
    builder.add_activity(Act::new(1, 3));
    builder.add_activity_with_dependencies(Act::new(2, 5), IndexSet::from([1]));
    builder.add_activity_with_dependencies(Act::new(3, 2), IndexSet::from([1, 2]));

    assert_eq!(builder.edge_ids().len(), 3);
    assert!(builder.transitive_reduction());
    assert_eq!(builder.edge_ids().len(), 2);
    // Activity 3 now depends only on activity 2 within the graph.
    assert_eq!(builder.activity_dependency_ids(3), vec![2]);
}
