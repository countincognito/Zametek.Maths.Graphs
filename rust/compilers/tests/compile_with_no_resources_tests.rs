//! Ports of `CompileWithNoResourcesTests.cs`.
//!
//! A compile with no resources. No activity waits for a resource, and so the
//! result is the critical path of the plan alone. The scheduler still puts the
//! activities on lanes (schedules that have no resource), which show how many
//! activities run at the same time. Up to 4.1.1 the compile made each lane a
//! synthetic resource: an activity depended on the activity before it on its
//! lane, and that changed the times of the plan. These tests hold that the lanes
//! change nothing, that no lane has two activities at once, and that a compile
//! with real resources keeps its resource dependencies.

use indexmap::{IndexMap, IndexSet};
use zametek_maths_graphs_compilers::contracts::IVertexGraphFactory;
use zametek_maths_graphs_compilers::{VertexGraphBuilder, VertexGraphCompiler, VertexGraphFactory};
use zametek_maths_graphs_primitives::{
    DependentActivity, GraphCompilation, GraphCompilationErrorCode, InterActivityAllocationType,
    Resource, ScheduledActivity,
};

type Compiler = VertexGraphCompiler<i32, i32, i32>;
type Builder = VertexGraphBuilder<i32, i32, i32>;
type Act = DependentActivity<i32, i32, i32>;
type Compilation = GraphCompilation<i32, i32, i32>;

// The EST, the EFT, the LST, the LFT and the free slack of an activity.
type Times = (
    Option<i32>,
    Option<i32>,
    Option<i32>,
    Option<i32>,
    Option<i32>,
);

fn compiler_of(activities: &[Act]) -> Compiler {
    let mut compiler = Compiler::new();
    for activity in activities {
        compiler.add_activity(activity.clone());
    }
    compiler
}

// The plain critical path of the same activities: the builder alone, with no
// scheduler and no lanes.
fn plain_critical_path_of(activities: &[Act]) -> Builder {
    let mut builder: Builder = VertexGraphFactory::new().create_builder();
    for activity in activities {
        let clone = activity.clone();
        let dependencies: IndexSet<i32> = clone
            .dependencies
            .iter()
            .chain(clone.planning_dependencies.iter())
            .copied()
            .collect();
        builder.add_activity_with_dependencies(clone, dependencies);
    }
    builder.calculate_critical_path().unwrap();
    assert!(builder.back_fill_isolated_nodes());
    builder
}

fn lanes_of(compilation: &Compilation) -> Vec<Vec<(i32, i32, i32)>> {
    compilation
        .resource_schedules
        .iter()
        .map(|x| {
            x.scheduled_activities
                .iter()
                .map(|y| (y.id, y.start_time, y.finish_time))
                .collect()
        })
        .collect()
}

fn times_of(activity: &Act) -> Times {
    (
        activity.earliest_start_time,
        activity.earliest_finish_time(),
        activity.latest_start_time(),
        activity.latest_finish_time,
        activity.free_slack,
    )
}

fn compiled_of(compilation: &Compilation) -> IndexMap<i32, &Act> {
    compilation
        .dependent_activities
        .iter()
        .map(|x| (x.id(), x))
        .collect()
}

fn ids_of(ids: &IndexSet<i32>) -> Vec<i32> {
    ids.iter().copied().collect()
}

// The result of a compile with no resources is the critical path of the plan,
// and the lanes hold each activity that takes time once, at its earliest start
// time, with no two activities at once.
fn assert_is_the_plain_critical_path_with_lanes(
    compiler: &Compiler,
    compilation: &Compilation,
    activities: &[Act],
    custom_message: &str,
) {
    let plain = plain_critical_path_of(activities);
    let compiled = compiled_of(compilation);

    assert_eq!(
        compiler.finish_time(),
        plain.finish_time(),
        "{custom_message}"
    );
    for expected in plain.activities() {
        let id = expected.id();
        assert_eq!(
            times_of(compiled[&id]),
            times_of(expected),
            "{custom_message} activity {id}"
        );
        assert_eq!(
            compiled[&id].is_critical(),
            expected.is_critical(),
            "{custom_message} activity {id}"
        );
        assert!(
            compiled[&id].resource_dependencies.is_empty(),
            "{custom_message} activity {id}"
        );
    }

    let mut lane_of: IndexMap<i32, i32> = IndexMap::new();
    let mut lane_number = 0;
    for schedule in &compilation.resource_schedules {
        lane_number += 1;
        assert!(schedule.resource.is_none(), "{custom_message}");
        let mut previous: Option<&ScheduledActivity<i32>> = None;
        for scheduled_activity in &schedule.scheduled_activities {
            let id = scheduled_activity.id;
            assert_eq!(
                scheduled_activity.start_time,
                compiled[&id].earliest_start_time.unwrap_or(0),
                "{custom_message} activity {id}"
            );
            if let Some(previous) = previous {
                assert!(
                    scheduled_activity.start_time >= previous.finish_time,
                    "{custom_message} activities {} and {id}",
                    previous.id
                );
            }
            assert!(!lane_of.contains_key(&id), "{custom_message} activity {id}");
            lane_of.insert(id, lane_number);
            previous = Some(scheduled_activity);
        }
    }

    // An activity is allocated to the number of its lane, and a milestone is on
    // no lane.
    for (id, activity) in &compiled {
        if activity.duration > 0 {
            assert!(
                lane_of.contains_key(id),
                "{custom_message} activity {id} is on no lane"
            );
            assert_eq!(
                ids_of(&activity.allocated_to_resources),
                vec![lane_of[id]],
                "{custom_message} activity {id}"
            );
        } else {
            assert!(!lane_of.contains_key(id), "{custom_message} activity {id}");
            assert!(
                activity.allocated_to_resources.is_empty(),
                "{custom_message} activity {id}"
            );
        }
    }
}

// Activities 2 and 4 have no connection. The scheduler puts 4 after 2 on one
// lane, because the lane is free when 4 can start. Up to 4.1.1 the compile made
// that a dependency: 4 had the resource dependency 2, and 2 had LST 3, LFT 4 and
// free slack 1. The plan gives 2 the whole time to the finish: LST 4, LFT 5 and
// free slack 4.
#[test]
fn vertex_graph_compiler_given_no_resources_and_two_unrelated_activities_on_one_lane_then_the_times_are_those_of_the_plan(
) {
    let activities = [
        Act::new(1, 5),
        Act::new(2, 1),
        Act::new(3, 2),
        Act::with_dependencies(4, 1, [3]),
    ];
    let mut compiler = compiler_of(&activities);

    let compilation = compiler.compile().unwrap();

    assert!(
        compilation.compilation_errors.is_empty(),
        "{:?}",
        compilation.compilation_errors
    );
    assert_eq!(compiler.finish_time(), 5);
    assert_eq!(
        lanes_of(&compilation),
        vec![vec![(1, 0, 5)], vec![(2, 0, 1), (4, 2, 3)], vec![(3, 0, 2)],]
    );

    let compiled = compiled_of(&compilation);
    assert!(compiled[&4].resource_dependencies.is_empty());
    assert_eq!(ids_of(&compiled[&4].dependencies), vec![3]);
    assert_eq!(
        times_of(compiled[&2]),
        (Some(0), Some(1), Some(4), Some(5), Some(4))
    );
    assert_eq!(ids_of(&compiled[&2].allocated_to_resources), vec![2]);
    assert_eq!(ids_of(&compiled[&4].allocated_to_resources), vec![2]);

    assert_is_the_plain_critical_path_with_lanes(&compiler, &compilation, &activities, "");
}

// Four activities with no dependencies, and activity 2 must finish by time 58.
// The scheduler holds an activity with a maximum LFT back to the latest start
// that meets it, and so it puts 2 after 1 on one lane, at time 55. Up to 4.1.1
// the compile made that a dependency: 2 started at time 1, and the plan finished
// at time 4. The plan alone lets each activity start at time 0, and it finishes
// with its longest activity, at time 3. Activity 2 is on a lane of its own,
// because each lane of the scheduler has an activity at time 0.
#[test]
fn vertex_graph_compiler_given_no_resources_and_maximum_latest_finish_time_then_the_finish_is_that_of_the_plan(
) {
    let activities = [
        Act::new(1, 1),
        {
            let mut activity = Act::new(2, 3);
            activity.maximum_latest_finish_time = Some(58);
            activity
        },
        Act::new(3, 3),
        Act::new(4, 2),
    ];
    let mut compiler = compiler_of(&activities);

    let compilation = compiler.compile().unwrap();

    assert!(
        compilation.compilation_errors.is_empty(),
        "{:?}",
        compilation.compilation_errors
    );
    assert_eq!(compiler.finish_time(), 3);
    assert!(compilation
        .dependent_activities
        .iter()
        .all(|x| x.earliest_start_time == Some(0)));
    assert!(compiled_of(&compilation)[&2]
        .resource_dependencies
        .is_empty());
    assert_eq!(
        lanes_of(&compilation),
        vec![
            vec![(1, 0, 1)],
            vec![(3, 0, 3)],
            vec![(4, 0, 2)],
            vec![(2, 0, 3)],
        ]
    );

    assert_is_the_plain_critical_path_with_lanes(&compiler, &compilation, &activities, "");
}

// The case that the synthetic resources were made for (November 2024). Activity
// 2 must finish by time 4, and the scheduler holds it back to start at time 2,
// after activity 5 on one lane. The plan lets 2 start at time 1, when 5 is not
// finished. Up to 4.1.1 the dependency on 5 moved 2 to time 2. Now 2 starts at
// time 1, on a lane of its own, and no lane has two activities at once.
#[test]
fn vertex_graph_compiler_given_no_resources_and_activity_held_back_by_maximum_latest_finish_time_then_no_lane_has_two_activities_at_once(
) {
    let activities = [
        Act::new(1, 1),
        {
            let mut activity = Act::with_dependencies(2, 2, [1]);
            activity.minimum_earliest_start_time = Some(1);
            activity.maximum_latest_finish_time = Some(4);
            activity
        },
        Act::with_dependencies(3, 2, [1]),
        Act::with_dependencies(4, 2, [1]),
        Act::new(5, 2),
    ];
    let mut compiler = compiler_of(&activities);

    let compilation = compiler.compile().unwrap();

    assert!(
        compilation.compilation_errors.is_empty(),
        "{:?}",
        compilation.compilation_errors
    );
    assert_eq!(
        lanes_of(&compilation),
        vec![
            vec![(1, 0, 1), (3, 1, 3)],
            vec![(5, 0, 2)],
            vec![(4, 1, 3)],
            vec![(2, 1, 3)],
        ]
    );

    assert_is_the_plain_critical_path_with_lanes(&compiler, &compilation, &activities, "");
}

// A small deterministic RNG for the random plans (the C# test uses
// `new Random(20261120)`, and Rust cannot reproduce its sequence). The plans are
// thus not the plans of the C# test. The test checks each plan against the plain
// critical path of the builder for the same plan, and it checks rules that the
// lanes must hold. It checks no values of its own, and so other plans check the
// same thing.
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

    // A value from `low` up to, but not including, `high`.
    fn next_range(&mut self, low: i32, high: i32) -> i32 {
        low + (self.next_u32() % (high - low) as u32) as i32
    }
}

// Whatever the plan is, with dependencies, a minimum EST, a maximum LFT or a
// minimum free slack: the times, the slack, the critical activities and the
// finish of a compile with no resources are those of the critical path of the
// plan, no activity has a resource dependency, and no lane has two activities at
// once.
#[test]
fn vertex_graph_compiler_given_random_plans_with_no_resources_then_the_result_is_the_plain_critical_path(
) {
    let mut random = Lcg::new(20261120);
    let mut compared = 0;
    for plan in 0..200 {
        let count = random.next_range(3, 16);
        let mut activities: Vec<Act> = Vec::new();
        for id in 1..=count {
            let mut dependencies: Vec<i32> = Vec::new();
            for dependency in 1..id {
                if random.next_range(0, count) < 2 {
                    dependencies.push(dependency);
                }
            }
            // One activity in ten is a milestone.
            let duration = if random.next_range(0, 10) == 0 {
                0
            } else {
                random.next_range(1, 6)
            };
            let mut activity = Act::with_dependencies(id, duration, dependencies);
            match random.next_range(0, 12) {
                0 => activity.minimum_earliest_start_time = Some(random.next_range(1, 10)),
                1 => activity.maximum_latest_finish_time = Some(random.next_range(10, 40)),
                2 => activity.minimum_free_slack = Some(random.next_range(1, 4)),
                _ => {}
            }
            activities.push(activity);
        }
        let mut compiler = compiler_of(&activities);

        let compilation = compiler.compile().unwrap();

        // A plan that cannot meet a maximum LFT reports C0010, and it still has
        // its times and its lanes.
        assert!(
            compilation
                .compilation_errors
                .iter()
                .all(|x| x.error_code == GraphCompilationErrorCode::C0010),
            "plan {plan}: {:?}",
            compilation.compilation_errors
        );
        assert_is_the_plain_critical_path_with_lanes(
            &compiler,
            &compilation,
            &activities,
            &format!("plan {plan}"),
        );
        compared += 1;
    }
    assert_eq!(compared, 200);
}

// A compile with a real resource is not a compile with no resources: the one
// resource does the two activities in turn, the second depends on the first, and
// the first has no slack. This must stay.
#[test]
fn vertex_graph_compiler_given_one_resource_and_two_unrelated_activities_then_the_second_depends_on_the_first(
) {
    let mut compiler = compiler_of(&[Act::new(1, 5), Act::new(2, 1)]);

    let compilation = compiler
        .compile_with_resources(&[Resource::new(
            1,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::Direct,
            1.0,
            1.0,
            0,
            [],
        )])
        .unwrap();

    assert!(
        compilation.compilation_errors.is_empty(),
        "{:?}",
        compilation.compilation_errors
    );
    assert_eq!(compilation.resource_schedules.len(), 1);
    assert_eq!(
        compilation.resource_schedules[0]
            .resource
            .as_ref()
            .expect("resource")
            .id,
        1
    );
    assert_eq!(lanes_of(&compilation), vec![vec![(1, 0, 5), (2, 5, 6)]]);

    let compiled = compiled_of(&compilation);
    assert_eq!(ids_of(&compiled[&2].resource_dependencies), vec![1]);
    assert!(compiled[&2].dependencies.is_empty());
    assert_eq!(ids_of(&compiled[&1].allocated_to_resources), vec![1]);
    assert_eq!(ids_of(&compiled[&2].allocated_to_resources), vec![1]);
    assert_eq!(
        times_of(compiled[&1]),
        (Some(0), Some(5), Some(0), Some(5), Some(0))
    );
    assert_eq!(
        times_of(compiled[&2]),
        (Some(5), Some(6), Some(5), Some(6), Some(0))
    );
    assert_eq!(compiler.finish_time(), 6);
}
