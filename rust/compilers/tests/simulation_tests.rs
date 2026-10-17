//! Tests of the simulator through its public API: that a result does not depend
//! on the number of threads, that the paths are the paths of the schedule, and
//! that a plan with errors is refused. The iteration against the compile is
//! tested in the crate, next to the runner. The numbers against the .NET library
//! are in `simulation_baseline_tests.rs`.

mod simulation_plans;

use simulation_plans::Plan;
use std::collections::HashMap;
use zametek_maths_graphs_compilers::primitives::{
    CriticalPathLink, DurationEstimate, InterActivityAllocationType, Resource, SimulationSettings,
};
use zametek_maths_graphs_compilers::ScheduleSimulator;

fn simulator_of(plan: &Plan, use_resources: bool) -> ScheduleSimulator<i32, i32, i32> {
    ScheduleSimulator::new(
        &plan.activities(),
        &plan.resources(use_resources),
        &[],
        &plan.estimates(),
    )
    .unwrap()
}

fn fixed_simulator(
    plan: &Plan,
    resources: &[Resource<i32, i32>],
) -> ScheduleSimulator<i32, i32, i32> {
    ScheduleSimulator::new(&plan.activities(), resources, &[], &HashMap::new()).unwrap()
}

#[test]
fn run_given_the_same_settings_then_gives_the_same_result_for_one_thread_and_for_many() {
    let simulator = simulator_of(&simulation_plans::layered(40, 5, 4), true);
    let mut settings = SimulationSettings {
        iterations: 300,
        seed: 77,
        max_degree_of_parallelism: 1,
        ..SimulationSettings::default()
    };
    let one = simulator.run(&settings).unwrap();

    settings.max_degree_of_parallelism = 4;
    let four = simulator.run(&settings).unwrap();

    assert_eq!(four, one);
    assert_eq!(one.iteration_count(), 300);
    assert_eq!(one.failed_iteration_count(), 0);
}

#[test]
fn run_given_fewer_iterations_with_the_same_seed_then_gives_the_prefix() {
    let simulator = simulator_of(&simulation_plans::reference_sample(), false);
    let longer = simulator
        .run(&SimulationSettings {
            iterations: 300,
            seed: 9,
            ..SimulationSettings::default()
        })
        .unwrap();
    let shorter = simulator
        .run(&SimulationSettings {
            iterations: 128,
            seed: 9,
            ..SimulationSettings::default()
        })
        .unwrap();

    assert_eq!(shorter.finish_times(), &longer.finish_times()[..128]);
}

#[test]
fn run_given_the_reference_sample_then_has_the_finish_times_that_phase_zero_measured() {
    let simulator = simulator_of(&simulation_plans::reference_sample(), false);
    assert_eq!(simulator.planned_finish_time(), 52);

    let result = simulator
        .run(&SimulationSettings {
            iterations: 20_000,
            seed: 2026,
            ..SimulationSettings::default()
        })
        .unwrap();

    let count = result.finish_times().len() as f64;
    let mean = result
        .finish_times()
        .iter()
        .map(|x| f64::from(*x))
        .sum::<f64>()
        / count;
    let variance = result
        .finish_times()
        .iter()
        .map(|x| (f64::from(*x) - mean).powi(2))
        .sum::<f64>()
        / count;
    assert!((mean - 52.41).abs() < 0.1, "{mean}");
    assert!((variance.sqrt() - 1.71).abs() < 0.06, "{}", variance.sqrt());
}

#[test]
fn run_given_a_plan_with_no_spread_then_every_iteration_is_the_planned_finish() {
    let plan = Plan::fixed(&[(1, 4, &[]), (2, 6, &[1]), (3, 3, &[1])]);
    let simulator = fixed_simulator(&plan, &[]);

    let result = simulator
        .run(&SimulationSettings {
            iterations: 200,
            ..SimulationSettings::default()
        })
        .unwrap();

    assert_eq!(simulator.planned_finish_time(), 10);
    assert!(result.finish_times().iter().all(|x| *x == 10));
    assert_eq!(result.critical_paths().len(), 1);
    let ids: Vec<i32> = result.critical_paths()[0]
        .steps
        .iter()
        .map(|x| x.activity_id)
        .collect();
    assert_eq!(ids, vec![1, 2]);
}

#[test]
fn run_given_two_equal_chains_then_both_are_critical_paths_of_every_iteration() {
    let plan = Plan::fixed(&[(1, 2, &[]), (2, 5, &[1]), (3, 5, &[1]), (4, 3, &[2, 3])]);
    let result = fixed_simulator(&plan, &[])
        .run(&SimulationSettings {
            iterations: 10,
            ..SimulationSettings::default()
        })
        .unwrap();

    assert_eq!(result.critical_paths().len(), 2);
    for i in 0..10 {
        assert_eq!(result.critical_path_indexes(i).unwrap().len(), 2);
        assert_eq!(result.is_critical_path_list_complete(i), Some(true));
    }
}

#[test]
fn run_given_more_tied_paths_than_the_maximum_then_records_the_first_ones_and_says_so() {
    // Six stages, each with two activities of the same duration: 2 to the power 6 paths.
    let mut activities: Vec<(i32, i32, Vec<i32>)> = Vec::new();
    let (mut previous_a, mut previous_b) = (0, 0);
    for stage in 0..6 {
        let (a, b) = ((stage * 2) + 1, (stage * 2) + 2);
        let dependencies = if stage == 0 {
            vec![]
        } else {
            vec![previous_a, previous_b]
        };
        activities.push((a, 3, dependencies.clone()));
        activities.push((b, 3, dependencies));
        previous_a = a;
        previous_b = b;
    }
    let borrowed: Vec<(i32, i32, &[i32])> = activities
        .iter()
        .map(|(id, d, deps)| (*id, *d, deps.as_slice()))
        .collect();
    let simulator = fixed_simulator(&Plan::fixed(&borrowed), &[]);

    let all = simulator
        .run(&SimulationSettings {
            iterations: 3,
            maximum_critical_paths_per_iteration: 1000,
            ..SimulationSettings::default()
        })
        .unwrap();
    let few = simulator
        .run(&SimulationSettings {
            iterations: 3,
            maximum_critical_paths_per_iteration: 10,
            ..SimulationSettings::default()
        })
        .unwrap();

    assert_eq!(all.critical_path_indexes(0).unwrap().len(), 64);
    assert_eq!(all.is_critical_path_list_complete(0), Some(true));
    assert_eq!(few.critical_path_indexes(0).unwrap().len(), 10);
    assert_eq!(few.is_critical_path_list_complete(0), Some(false));
}

#[test]
fn run_given_chains_that_differ_only_by_a_milestone_then_they_are_one_path_without_the_milestone() {
    let plan = Plan::fixed(&[(1, 5, &[]), (2, 0, &[1]), (3, 5, &[1, 2])]);
    let result = fixed_simulator(&plan, &[])
        .run(&SimulationSettings {
            iterations: 4,
            ..SimulationSettings::default()
        })
        .unwrap();

    assert_eq!(result.critical_paths().len(), 1);
    let ids: Vec<i32> = result.critical_paths()[0]
        .steps
        .iter()
        .map(|x| x.activity_id)
        .collect();
    assert_eq!(ids, vec![1, 3]);
    assert_eq!(result.critical_path_indexes(0).unwrap().len(), 1);
}

#[test]
fn run_given_two_activities_that_share_one_resource_then_the_second_follows_by_a_resource_link() {
    let plan = Plan::fixed(&[(1, 5, &[]), (2, 5, &[])]);
    let resources = vec![Resource::new(
        1,
        Some(String::new()),
        false,
        false,
        InterActivityAllocationType::None,
        1.0,
        1.0,
        0,
        Vec::<i32>::new(),
    )];
    let simulator = fixed_simulator(&plan, &resources);

    let result = simulator
        .run(&SimulationSettings {
            iterations: 2,
            ..SimulationSettings::default()
        })
        .unwrap();

    assert_eq!(simulator.planned_finish_time(), 10);
    assert_eq!(result.critical_paths().len(), 1);
    let links: Vec<CriticalPathLink> = result.critical_paths()[0]
        .steps
        .iter()
        .map(|x| x.link)
        .collect();
    assert_eq!(
        links,
        vec![CriticalPathLink::None, CriticalPathLink::Resource]
    );
}

#[test]
fn run_given_no_recording_of_the_critical_activities_or_paths_then_has_only_the_finish_times() {
    let simulator = simulator_of(&simulation_plans::reference_sample(), false);
    let result = simulator
        .run(&SimulationSettings {
            iterations: 100,
            record_critical_activities: false,
            record_critical_paths: false,
            ..SimulationSettings::default()
        })
        .unwrap();

    assert!(!result.has_critical_activities());
    assert!(!result.has_critical_paths());
    assert_eq!(result.is_critical(0, 0), None);
    assert_eq!(result.critical_path_indexes(0), None);
    assert_eq!(result.finish_times().len(), 100);
}

#[test]
fn run_given_a_plan_with_errors_then_the_plan_reports_them_and_the_run_fails() {
    let plan = Plan::fixed(&[(1, 4, &[2]), (2, 6, &[1])]);
    let simulator = fixed_simulator(&plan, &[]);

    assert!(!simulator.compilation_errors().is_empty());
    assert!(simulator.run(&SimulationSettings::default()).is_err());
}

#[test]
fn new_given_an_estimate_of_an_activity_that_is_not_in_the_plan_then_fails() {
    let plan = Plan::fixed(&[(1, 4, &[])]);
    let mut estimates = HashMap::new();
    estimates.insert(99, DurationEstimate::new(1, 2, 3).unwrap());

    assert!(ScheduleSimulator::new(&plan.activities(), &[], &[], &estimates).is_err());
}

#[test]
fn new_given_two_activities_with_the_same_id_then_fails() {
    let plan = Plan::fixed(&[(1, 4, &[]), (1, 5, &[])]);

    assert!(ScheduleSimulator::new(&plan.activities(), &[], &[], &HashMap::new()).is_err());
}

#[test]
fn validate_given_settings_out_of_range_then_fails() {
    let ok = SimulationSettings::default();
    assert!(ok.validate().is_ok());
    assert!(SimulationSettings {
        iterations: 0,
        ..ok.clone()
    }
    .validate()
    .is_err());
    assert!(SimulationSettings {
        iterations: 1_000_001,
        ..ok.clone()
    }
    .validate()
    .is_err());
    assert!(SimulationSettings {
        modified_beta_shape: 0.0,
        ..ok.clone()
    }
    .validate()
    .is_err());
    assert!(SimulationSettings {
        modified_beta_shape: f64::NAN,
        ..ok.clone()
    }
    .validate()
    .is_err());
    assert!(SimulationSettings {
        maximum_critical_paths_per_iteration: 0,
        ..ok
    }
    .validate()
    .is_err());
}
