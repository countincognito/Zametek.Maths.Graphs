//! Tests of the iteration runner against the compile. The runner is crate-private,
//! and so these tests live in the crate and not in `tests/`.
//!
//! The compile is the oracle. One runner takes the iterations in turn, as it does
//! in a simulation, so that any state that an iteration leaves behind shows in
//! the next one.

use super::iteration_runner::SimulationIterationRunner;
use super::random::SimulationRandom;
use crate::id_gen::PreviousIdGeneratorFactory;
use crate::vertex::{VertexGraphBuilder, VertexGraphCompiler};
use zametek_maths_graphs_primitives::{DependentActivity, InterActivityAllocationType, Resource};

// A layered plan of 30 activities, as in the tests of the simulator.
fn plan() -> Vec<DependentActivity<i32, i32, i32>> {
    (1..=30)
        .map(|id| {
            let layer = (id - 1) / 6;
            let mut dependencies = Vec::new();
            if layer > 0 {
                let previous_start = ((layer - 1) * 6) + 1;
                dependencies.push(previous_start + ((id * 7) % 6));
                dependencies.push(previous_start + ((id * 13) % 6));
            }
            DependentActivity::with_dependencies(id, 1 + (id % 9), dependencies)
        })
        .collect()
}

fn resources(count: i32) -> Vec<Resource<i32, i32>> {
    (1..=count)
        .map(|id| {
            Resource::new(
                id,
                Some(String::new()),
                false,
                false,
                InterActivityAllocationType::None,
                1.0,
                1.0,
                0,
                Vec::<i32>::new(),
            )
        })
        .collect()
}

fn assert_agrees_with_the_compile(resource_count: i32) {
    let activities = plan();
    let resources = resources(resource_count);
    let mut runner = SimulationIterationRunner::new(
        VertexGraphBuilder::new(PreviousIdGeneratorFactory::new(0)),
        &activities,
        &resources,
    )
    .unwrap();
    let ids: Vec<i32> = activities.iter().map(|x| x.id()).collect();
    let mut random = SimulationRandom::new(2024);

    for iteration in 0..60 {
        let durations: Vec<i32> = activities
            .iter()
            .map(|x| 1 + (random.next_u64() % (2 * x.duration as u64 + 1)) as i32)
            .collect();

        let finish = runner.run(&durations).unwrap();

        let mut compiler: VertexGraphCompiler<i32, i32, i32> = VertexGraphCompiler::new();
        for (activity, duration) in activities.iter().zip(&durations) {
            let mut copy = activity.clone();
            copy.duration = *duration;
            compiler.add_activity(copy);
        }
        let compilation = compiler.compile_with_resources(&resources).unwrap();
        assert!(compilation.compilation_errors.is_empty());
        assert_eq!(finish, compiler.finish_time(), "iteration {iteration}");

        for (id, expected) in ids.iter().zip(&compilation.dependent_activities) {
            // With no resources too: a compile gives the critical activities of
            // the plan alone, as an iteration does.
            let critical = runner.builder().activity(*id).unwrap().is_critical();
            assert_eq!(critical, expected.is_critical(), "iteration {iteration}");
        }
    }
}

#[test]
fn run_given_a_plan_with_resources_then_gives_the_finish_and_the_critical_activities_of_a_compile()
{
    assert_agrees_with_the_compile(3);
}

// Up to 4.1.1 there were two tests here. A compile with no resources linked the
// activities on its synthetic resources, and so it could mark more activities
// critical than an iteration did, unless the iteration was set to do the same.
// Now neither links them, and an iteration gives the critical activities of a
// compile.
#[test]
fn run_given_no_resources_then_gives_the_finish_and_the_critical_activities_of_a_compile() {
    assert_agrees_with_the_compile(0);
}

#[test]
fn new_given_a_builder_that_already_holds_activities_then_fails() {
    let activities = plan();
    let mut builder = VertexGraphBuilder::new(PreviousIdGeneratorFactory::new(0));
    builder.add_activity(activities[0].clone());

    let error = SimulationIterationRunner::new(builder, &activities, &resources(3))
        .err()
        .expect("a builder that holds activities is refused");

    assert!(error.message().contains("empty"), "{}", error.message());
}
