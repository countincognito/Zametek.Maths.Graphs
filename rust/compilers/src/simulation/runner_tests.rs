//! Tests of the iteration runner against the compile. The runner is crate-private,
//! and so these tests live in the crate and not in `tests/`.
//!
//! The compile is the oracle. One runner takes the iterations in turn, as it does
//! in a simulation, so that any state that an iteration leaves behind shows in
//! the next one.

use super::iteration_runner::SimulationIterationRunner;
use super::random::SimulationRandom;
use crate::id_gen::PreviousIdGenerator;
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

fn assert_agrees_with_the_compile(resource_count: i32, onto_synthetic: bool) {
    let activities = plan();
    let resources = resources(resource_count);
    let mut runner = SimulationIterationRunner::new(
        VertexGraphBuilder::new(PreviousIdGenerator::new(0)),
        &activities,
        &resources,
        onto_synthetic,
    )
    .unwrap();
    let ids: Vec<i32> = activities.iter().map(|x| x.id()).collect();
    let shortcut = resource_count == 0 && !onto_synthetic;
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
            let critical = runner.builder().activity(*id).unwrap().is_critical();
            if shortcut {
                // With no resources the compile may mark more activities critical,
                // and never fewer.
                assert!(!critical || expected.is_critical(), "iteration {iteration}");
            } else {
                assert_eq!(critical, expected.is_critical(), "iteration {iteration}");
            }
        }
    }
}

#[test]
fn run_given_a_plan_with_resources_then_gives_the_finish_and_the_critical_activities_of_a_compile()
{
    assert_agrees_with_the_compile(3, false);
}

#[test]
fn run_given_no_resources_and_the_compile_way_then_gives_the_critical_activities_of_a_compile() {
    assert_agrees_with_the_compile(0, true);
}

#[test]
fn run_given_no_resources_and_the_shortcut_then_gives_the_finish_of_a_compile() {
    assert_agrees_with_the_compile(0, false);
}

#[test]
fn new_given_a_builder_that_already_holds_activities_then_fails() {
    let activities = plan();
    let mut builder = VertexGraphBuilder::new(PreviousIdGenerator::new(0));
    builder.add_activity(activities[0].clone());

    let error = SimulationIterationRunner::new(builder, &activities, &resources(3), false)
        .err()
        .expect("a builder that holds activities is refused");

    assert!(error.message().contains("empty"), "{}", error.message());
}
