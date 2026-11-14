//! Tests of the simulator through its public API: that a result does not depend
//! on the number of threads, that the paths are the paths of the schedule, and
//! that a plan with errors is refused. The iteration against the compile is
//! tested in the crate, next to the runner. The numbers against the .NET library
//! are in `simulation_baseline_tests.rs`.

mod simulation_plans;

use simulation_plans::Plan;
use std::collections::HashMap;
use zametek_maths_graphs_compilers::primitives::{
    graph_limits, CriticalPathLink, DependentActivity, DurationDistribution, DurationEstimate,
    GraphCompilationErrorCode, InterActivityAllocationType, Resource, SimulationIterationFailure,
    SimulationResult, SimulationSettings, WorkStream,
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

fn resource(id: i32, is_explicit_target: bool) -> Resource<i32, i32> {
    Resource::new(
        id,
        Some(String::new()),
        is_explicit_target,
        false,
        InterActivityAllocationType::None,
        1.0,
        1.0,
        0,
        Vec::<i32>::new(),
    )
}

/// The paths of an iteration, as `Describe` in the .NET tests gives them: the IDs
/// of each path joined by `>`, with `~` before an activity that follows by a
/// resource link, and the paths joined by ` | `.
fn describe(result: &SimulationResult<i32>, iteration: usize) -> String {
    result
        .critical_path_indexes(iteration)
        .unwrap()
        .iter()
        .map(|x| {
            result.critical_paths()[*x]
                .steps
                .iter()
                .map(|s| {
                    if s.link == CriticalPathLink::Resource {
                        format!("~{}", s.activity_id)
                    } else {
                        s.activity_id.to_string()
                    }
                })
                .collect::<Vec<_>>()
                .join(">")
        })
        .collect::<Vec<_>>()
        .join(" | ")
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
fn run_given_a_resource_link_beside_a_dependency_then_the_step_is_a_dependency() {
    // 2 depends on 1, and one resource does them in turn. The plan already ties 2
    // to 1, and so the resource adds no link of its own.
    let plan = Plan::fixed(&[(1, 5, &[]), (2, 5, &[1])]);
    let result = fixed_simulator(&plan, &[resource(1, false)])
        .run(&SimulationSettings {
            iterations: 2,
            ..SimulationSettings::default()
        })
        .unwrap();

    assert_eq!(result.critical_paths().len(), 1);
    assert_eq!(describe(&result, 0), "1>2");
}

#[test]
fn run_given_a_resource_link_beside_a_chain_of_the_plan_through_milestones_then_they_are_one_path_with_a_dependency(
) {
    // 1, then the milestones in a row, then 10, and one resource does 1 and 10 in
    // turn. The plan ties 10 to 1 through the milestones, which are not on a
    // path. The link of the resource is the same tie, and so the two chains are
    // one path, as they are when 10 depends on 1 itself.
    for milestones in 1..=2 {
        let mut activities: Vec<(i32, i32, Vec<i32>)> = vec![(1, 5, vec![])];
        for i in 0..milestones {
            activities.push((2 + i, 0, vec![1 + i]));
        }
        activities.push((10, 5, vec![1 + milestones]));
        let borrowed: Vec<(i32, i32, &[i32])> = activities
            .iter()
            .map(|(id, d, deps)| (*id, *d, deps.as_slice()))
            .collect();
        let simulator = fixed_simulator(&Plan::fixed(&borrowed), &[resource(1, false)]);

        let result = simulator
            .run(&SimulationSettings {
                iterations: 2,
                ..SimulationSettings::default()
            })
            .unwrap();

        assert_eq!(
            simulator.planned_finish_time(),
            10,
            "{milestones} milestones"
        );
        assert_eq!(describe(&result, 0), "1>10", "{milestones} milestones");
        assert_eq!(result.critical_paths().len(), 1, "{milestones} milestones");
    }
}

#[test]
fn run_given_a_resource_link_beside_a_chain_of_the_plan_from_another_activity_then_both_are_paths()
{
    // Resource 1 does 1 and 3 in turn, and only those. Resource 2 does 4 at the
    // same time as 1. 3 depends on 4 through a milestone, and not on 1. Thus, the
    // link of the resource from 1 is a tie of its own, and it stays a path.
    let plan = Plan::fixed(&[(1, 5, &[]), (4, 5, &[]), (2, 0, &[4]), (3, 5, &[2])]);
    let mut activities = plan.activities();
    for activity in &mut activities {
        if activity.id() == 1 || activity.id() == 3 {
            activity.target_resources.insert(1);
        }
    }
    let simulator = ScheduleSimulator::new(
        &activities,
        &[resource(1, true), resource(2, false)],
        &[],
        &HashMap::new(),
    )
    .unwrap();

    let result = simulator
        .run(&SimulationSettings {
            iterations: 2,
            ..SimulationSettings::default()
        })
        .unwrap();

    assert_eq!(simulator.planned_finish_time(), 10);
    assert_eq!(describe(&result, 0), "1>~3 | 4>3");
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

type Act = DependentActivity<i32, i32, i32>;

// Activity 1 takes from 8 to 20 time units (8, 10, 20). The shape says where the
// maximum LFT is: on a milestone after activity 1 (12), on activity 1 itself
// (12), or on activity 1 with a minimum EST of 2 (14). Without the maximum, the
// finish time of an iteration is the time that the maximum checks. The plan has
// no resources. Gives the result, the limit and the place of the activity with
// the maximum.
fn run_plan_with_a_maximum_latest_finish_time(
    shape: &str,
    with_maximum: bool,
) -> (SimulationResult<i32>, i32, usize) {
    let maximum = |value: i32| with_maximum.then_some(value);
    let (activities, limit, constrained_index) = match shape {
        "milestone" => {
            let mut milestone = Act::with_dependencies(2, 0, [1]);
            milestone.maximum_latest_finish_time = maximum(12);
            (vec![Act::new(1, 10), milestone], 12, 1)
        }
        "activity" => {
            let mut activity = Act::new(1, 10);
            activity.maximum_latest_finish_time = maximum(12);
            (vec![activity], 12, 0)
        }
        "window" => {
            let mut activity = Act::new(1, 10);
            activity.minimum_earliest_start_time = Some(2);
            activity.maximum_latest_finish_time = maximum(14);
            (vec![activity], 14, 0)
        }
        _ => panic!("unknown shape {shape}"),
    };
    let estimates = HashMap::from([(1, DurationEstimate::new(8, 10, 20).unwrap())]);
    let simulator =
        ScheduleSimulator::<i32, i32, i32>::new(&activities, &[], &[], &estimates).unwrap();
    assert!(simulator.compilation_errors().is_empty());
    let result = simulator
        .run(&SimulationSettings {
            iterations: 500,
            seed: 1,
            ..SimulationSettings::default()
        })
        .unwrap();
    (result, limit, constrained_index)
}

// Port of `Run_Given_AMaximumLatestFinishTimeThatSomeIterationsMiss_Then_TheyFailWithInvalidConstraints`.
// An iteration whose durations cannot meet a maximum LFT once reported the
// maximum as its finish time. It fails now, as a compile with the same durations
// does, and the result says why and which maximum it missed.
#[test]
fn run_given_a_maximum_latest_finish_time_that_some_iterations_miss_then_they_fail_with_invalid_constraints(
) {
    for shape in ["milestone", "activity", "window"] {
        let (control, _, _) = run_plan_with_a_maximum_latest_finish_time(shape, false);
        let (result, limit, constrained_index) =
            run_plan_with_a_maximum_latest_finish_time(shape, true);

        assert_eq!(control.failed_iteration_count(), 0, "{shape}");
        assert!(control.has_failure_reasons(), "{shape}");
        assert!(result.has_failure_reasons(), "{shape}");

        let mut misses = 0;
        for i in 0..result.iteration_count() {
            assert_eq!(
                control.failure_reason(i),
                Some(SimulationIterationFailure::None),
                "{shape}, iteration {i}"
            );
            if control.finish_times()[i] > limit {
                misses += 1;
                assert_eq!(
                    result.finish_times()[i],
                    SimulationResult::<i32>::FAILED_FINISH_TIME,
                    "{shape}, iteration {i}"
                );
                assert_eq!(
                    result.failure_reason(i),
                    Some(SimulationIterationFailure::InvalidConstraints),
                    "{shape}, iteration {i}"
                );
                assert!(
                    result.critical_path_indexes(i).unwrap().is_empty(),
                    "{shape}, iteration {i}"
                );
            } else {
                assert_eq!(
                    result.finish_times()[i],
                    control.finish_times()[i],
                    "{shape}, iteration {i}"
                );
                assert_eq!(
                    result.failure_reason(i),
                    Some(SimulationIterationFailure::None),
                    "{shape}, iteration {i}"
                );
            }
        }

        // The same seed gives the same draws in the .NET library, which checks
        // the same number.
        assert_eq!(misses, 124, "{shape}");
        assert_eq!(result.failed_iteration_count(), misses, "{shape}");
        for a in 0..result.activity_ids().len() {
            assert_eq!(
                control.maximum_latest_finish_time_miss_count(a),
                Some(0),
                "{shape}"
            );
            assert_eq!(
                result.maximum_latest_finish_time_miss_count(a),
                Some(if a == constrained_index { misses } else { 0 }),
                "{shape}"
            );
        }
    }
}

// Port of `Run_Given_DurationsThatRunPastTheHorizon_Then_TheIterationsFailWithTheReason`.
// Two activities in sequence that can each take almost the whole horizon. When
// their sum runs past it, the resource scheduler cannot place the second one,
// and a plan with no resources runs past the horizon.
#[test]
fn run_given_durations_that_run_past_the_horizon_then_the_iterations_fail_with_the_reason() {
    let longest = graph_limits::MAXIMUM_TIME_VALUE - 1;
    for (with_resource, expected) in [
        (true, SimulationIterationFailure::ResourceSchedulingStall),
        (false, SimulationIterationFailure::MaximumTimeValueExceeded),
    ] {
        let resources = if with_resource {
            vec![resource(1, false)]
        } else {
            Vec::new()
        };
        let estimates = HashMap::from([
            (1, DurationEstimate::new(1, 1, longest).unwrap()),
            (2, DurationEstimate::new(1, 1, longest).unwrap()),
        ]);
        let simulator = ScheduleSimulator::<i32, i32, i32>::new(
            &[Act::new(1, 1), Act::with_dependencies(2, 1, [1])],
            &resources,
            &[],
            &estimates,
        )
        .unwrap();
        assert!(simulator.compilation_errors().is_empty());

        let result = simulator
            .run(&SimulationSettings {
                iterations: 200,
                seed: 1,
                distribution: DurationDistribution::Uniform,
                ..SimulationSettings::default()
            })
            .unwrap();

        assert!(result.failed_iteration_count() > 0, "{expected:?}");
        assert!(
            result.failed_iteration_count() < result.iteration_count(),
            "{expected:?}"
        );
        for i in 0..result.iteration_count() {
            if result.finish_times()[i] == SimulationResult::<i32>::FAILED_FINISH_TIME {
                assert_eq!(result.failure_reason(i), Some(expected), "iteration {i}");
            } else {
                assert!(
                    result.finish_times()[i] <= graph_limits::MAXIMUM_TIME_VALUE,
                    "iteration {i}"
                );
                assert_eq!(
                    result.failure_reason(i),
                    Some(SimulationIterationFailure::None),
                    "iteration {i}"
                );
            }
        }
        assert_eq!(result.maximum_latest_finish_time_miss_count(0), Some(0));
        assert_eq!(result.maximum_latest_finish_time_miss_count(1), Some(0));
    }
}

// Port of `Run_Given_AMilestoneInAPhaseAfterItsLastActivity_Then_EveryIterationFinishesWithTheMilestone`.
// The first scheduling pass of an iteration once failed for a milestone in a
// phase after the last activity of the phase. The phase now runs to the
// milestone, and each iteration finishes with it.
#[test]
fn run_given_a_milestone_in_a_phase_after_its_last_activity_then_every_iteration_finishes_with_the_milestone(
) {
    let mut activity1 = Act::new(1, 5);
    activity1.target_work_streams.insert(1);
    let mut activity2 = Act::with_dependencies(2, 0, [1]);
    activity2.minimum_earliest_start_time = Some(10);
    activity2.target_work_streams.insert(1);
    let resources = [
        Resource::new(
            1,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::Direct,
            1.0,
            1.0,
            0,
            Vec::<i32>::new(),
        ),
        Resource::new(
            2,
            Some(String::new()),
            true,
            false,
            InterActivityAllocationType::Indirect,
            1.0,
            1.0,
            0,
            vec![1],
        ),
    ];
    let estimates = HashMap::from([(1, DurationEstimate::new(3, 5, 9).unwrap())]);
    let simulator = ScheduleSimulator::<i32, i32, i32>::new(
        &[activity1, activity2],
        &resources,
        &[WorkStream::new(1, String::new(), true)],
        &estimates,
    )
    .unwrap();
    assert!(simulator.compilation_errors().is_empty());
    assert_eq!(simulator.planned_finish_time(), 10);

    let result = simulator
        .run(&SimulationSettings {
            iterations: 100,
            seed: 1,
            ..SimulationSettings::default()
        })
        .unwrap();

    assert_eq!(result.failed_iteration_count(), 0);
    assert!(result.finish_times().iter().all(|x| *x == 10));
}

// Port of `Ctor_Given_AnActivityThatMustFinishByTimeZero_Then_ThePlanReportsC0010AndTheRunThrows`.
// The first check of a simulation is a compile. An activity that must finish by
// time 0, with no activity that finishes after it, made that compile panic, and
// so the simulator could not be made. The check now reports the error of the
// compile, and a run fails, as for any plan with errors.
#[test]
fn new_given_an_activity_that_must_finish_by_time_zero_then_the_plan_reports_c0010_and_the_run_fails(
) {
    let mut activity = Act::new(1, 5);
    activity.maximum_latest_finish_time = Some(0);
    let estimates = HashMap::from([(1, DurationEstimate::new(3, 5, 9).unwrap())]);
    let simulator =
        ScheduleSimulator::<i32, i32, i32>::new(&[activity], &[], &[], &estimates).unwrap();

    let codes: Vec<GraphCompilationErrorCode> = simulator
        .compilation_errors()
        .iter()
        .map(|x| x.error_code)
        .collect();
    assert_eq!(codes, vec![GraphCompilationErrorCode::C0010]);
    assert!(simulator.run(&SimulationSettings::default()).is_err());
}

// Port of `Run_Given_AMilestoneThatMustFinishByTimeZeroAndCanTakeTime_Then_TheIterationsThatTakeTimeFail`.
// A milestone that must finish by time 0, but that can take time. The plan has
// no error. But an iteration that draws a duration pulls the activity back
// before time 0, and no activity finishes after it. Each such iteration must
// fail with InvalidConstraints, in each way to schedule it, and each other
// iteration finishes at time 0.
#[test]
fn run_given_a_milestone_that_must_finish_by_time_zero_and_can_take_time_then_the_iterations_that_take_time_fail(
) {
    for (with_resource, schedule_onto_synthetic_resources) in
        [(false, false), (false, true), (true, false)]
    {
        let case =
            format!("with_resource {with_resource}, synthetic {schedule_onto_synthetic_resources}");
        let mut activity = Act::new(1, 0);
        activity.maximum_latest_finish_time = Some(0);
        let resources = if with_resource {
            vec![Resource::new(
                1,
                Some(String::new()),
                false,
                false,
                InterActivityAllocationType::Direct,
                1.0,
                1.0,
                0,
                Vec::<i32>::new(),
            )]
        } else {
            Vec::new()
        };
        let estimates = HashMap::from([(1, DurationEstimate::new(0, 0, 3).unwrap())]);
        let simulator =
            ScheduleSimulator::<i32, i32, i32>::new(&[activity], &resources, &[], &estimates)
                .unwrap();
        assert!(simulator.compilation_errors().is_empty(), "{case}");

        let result = simulator
            .run(&SimulationSettings {
                iterations: 200,
                seed: 1,
                schedule_onto_synthetic_resources,
                ..SimulationSettings::default()
            })
            .unwrap();

        // Each way to schedule the iterations fails the same 73 of them: the ones
        // that draw a duration. The .NET test checks the same number, because
        // the same seed gives the same draws.
        assert_eq!(result.failed_iteration_count(), 73, "{case}");
        for i in 0..result.iteration_count() {
            if result.finish_times()[i] == SimulationResult::<i32>::FAILED_FINISH_TIME {
                assert_eq!(
                    result.failure_reason(i),
                    Some(SimulationIterationFailure::InvalidConstraints),
                    "{case}, iteration {i}"
                );
            } else {
                assert_eq!(result.finish_times()[i], 0, "{case}, iteration {i}");
            }
        }
        assert_eq!(
            result.maximum_latest_finish_time_miss_count(0),
            Some(result.failed_iteration_count()),
            "{case}"
        );
    }
}
