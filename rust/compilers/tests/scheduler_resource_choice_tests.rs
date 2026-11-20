//! Ports of `SchedulerResourceChoiceTests.cs`.
//!
//! Which resource an activity takes when it can use more than one. The scheduler
//! goes through the ready activities in the order of the priority list, and each
//! takes the first free resource in the allocation order that it can use. Then,
//! if a ready activity still waits while a resource is free, the activities that
//! started at that moment move to free resources to let it start. Up to 4.1.1
//! they did not move, and the activity waited while a resource did nothing. These
//! tests hold the rule, its limit, and what it must leave alone: the allocation
//! order with no contention, the activities that need all of their targets, and
//! the resources that are not explicit targets.

use indexmap::IndexMap;
use zametek_maths_graphs_compilers::VertexGraphCompiler;
use zametek_maths_graphs_primitives::{
    DependentActivity, GraphCompilation, InterActivityAllocationType, LogicalOperator, Resource,
};

type Compiler = VertexGraphCompiler<i32, i32, i32>;
type Act = DependentActivity<i32, i32, i32>;
type Res = Resource<i32, i32>;
type Compilation = GraphCompilation<i32, i32, i32>;

fn resource_of(id: i32, is_explicit_target: bool, allocation_order: i32) -> Res {
    Resource::new(
        id,
        Some(format!("R{id}")),
        is_explicit_target,
        false,
        InterActivityAllocationType::Direct,
        1.0,
        1.0,
        allocation_order,
        [],
    )
}

fn activity_of(
    id: i32,
    duration: i32,
    target_resources: &[i32],
    target_resource_operator: LogicalOperator,
    dependencies: &[i32],
) -> Act {
    let mut activity = Act::with_dependencies(id, duration, dependencies.iter().copied());
    activity.target_resource_operator = target_resource_operator;
    activity
        .target_resources
        .extend(target_resources.iter().copied());
    activity
}

fn compile(resources: &[Res], activities: &[Act]) -> (Compiler, Compilation) {
    let mut compiler = Compiler::new();
    for activity in activities {
        compiler.add_activity(activity.clone());
    }
    let compilation = compiler.compile_with_resources(resources).unwrap();
    assert!(
        compilation.compilation_errors.is_empty(),
        "{:?}",
        compilation.compilation_errors
    );
    (compiler, compilation)
}

// The schedule of each resource that has one, by the ID of the resource.
fn schedules_of(compilation: &Compilation) -> IndexMap<i32, Vec<(i32, i32, i32)>> {
    compilation
        .resource_schedules
        .iter()
        .filter(|x| !x.scheduled_activities.is_empty())
        .map(|x| {
            (
                x.resource.as_ref().expect("resource").id,
                x.scheduled_activities
                    .iter()
                    .map(|y| (y.id, y.start_time, y.finish_time))
                    .collect(),
            )
        })
        .collect()
}

fn compiled_activity(compilation: &Compilation, id: i32) -> &Act {
    compilation
        .dependent_activities
        .iter()
        .find(|x| x.id() == id)
        .unwrap_or_else(|| panic!("activity {id} was not compiled"))
}

// Activities 2 and 3 are ready at time 1, and 2 is first in the priority list.
// Activity 2 can use R1 or R2, and it takes R1, the first in the allocation
// order. Activity 3 can use only R1. Up to 4.1.1 it waited for a day while R2 did
// nothing: R1 had 1, 2 and 3, R2 had 4, and the plan finished at time 4. Now 2
// moves to R2, and 3 starts on R1.
#[test]
fn vertex_graph_compiler_given_activity_with_two_targets_and_another_that_can_use_only_the_first_then_the_first_gives_way(
) {
    for is_explicit_target in [true, false] {
        let (compiler, compilation) = compile(
            &[
                resource_of(1, is_explicit_target, 0),
                resource_of(2, is_explicit_target, 1),
            ],
            &[
                activity_of(1, 1, &[1], LogicalOperator::Or, &[]),
                activity_of(2, 1, &[1, 2], LogicalOperator::Or, &[1]),
                activity_of(3, 1, &[1], LogicalOperator::Or, &[1]),
                activity_of(4, 1, &[2], LogicalOperator::Or, &[2, 3]),
            ],
        );

        let schedules = schedules_of(&compilation);
        assert_eq!(
            schedules[&1],
            vec![(1, 0, 1), (3, 1, 2)],
            "is_explicit_target {is_explicit_target}"
        );
        assert_eq!(
            schedules[&2],
            vec![(2, 1, 2), (4, 2, 3)],
            "is_explicit_target {is_explicit_target}"
        );
        assert_eq!(compiler.finish_time(), 3);

        let allocated_to_resources_of = |id: i32| -> Vec<i32> {
            compiled_activity(&compilation, id)
                .allocated_to_resources
                .iter()
                .copied()
                .collect()
        };
        let resource_dependencies_of = |id: i32| -> Vec<i32> {
            compiled_activity(&compilation, id)
                .resource_dependencies
                .iter()
                .copied()
                .collect()
        };
        assert_eq!(allocated_to_resources_of(2), vec![2]);
        assert_eq!(allocated_to_resources_of(3), vec![1]);
        assert_eq!(resource_dependencies_of(3), vec![1]);
        assert_eq!(resource_dependencies_of(4), vec![2]);
    }
}

// The same with an activity that has no targets: it can use each resource that is
// not an explicit target, and it takes the first one in the allocation order. It
// moves to the second for the activity that can use only the first.
#[test]
fn vertex_graph_compiler_given_activity_with_no_targets_and_another_that_can_use_only_the_first_resource_then_the_first_gives_way(
) {
    let (compiler, compilation) = compile(
        &[resource_of(1, false, 0), resource_of(2, false, 1)],
        &[
            activity_of(1, 1, &[1], LogicalOperator::Or, &[]),
            activity_of(2, 1, &[], LogicalOperator::And, &[1]),
            activity_of(3, 1, &[1], LogicalOperator::Or, &[1]),
            activity_of(4, 1, &[2], LogicalOperator::Or, &[2, 3]),
        ],
    );

    let schedules = schedules_of(&compilation);
    assert_eq!(schedules[&1], vec![(1, 0, 1), (3, 1, 2)]);
    assert_eq!(schedules[&2], vec![(2, 1, 2), (4, 2, 3)]);
    assert_eq!(compiler.finish_time(), 3);
}

// Activity 2 needs R1 and R2. Activity 1 took R1 at the same moment, and it can
// also use R3, which is free. Up to 4.1.1 activity 2 waited until time 3. Now 1
// moves to R3, and 2 starts at once on R1 and R2.
#[test]
fn vertex_graph_compiler_given_activity_that_needs_all_of_its_targets_and_another_that_took_one_of_them_then_the_other_gives_way(
) {
    let (compiler, compilation) = compile(
        &[
            resource_of(1, true, 0),
            resource_of(2, true, 1),
            resource_of(3, true, 2),
        ],
        &[
            activity_of(1, 3, &[1, 3], LogicalOperator::Or, &[]),
            activity_of(2, 1, &[1, 2], LogicalOperator::And, &[]),
        ],
    );

    let schedules = schedules_of(&compilation);
    assert_eq!(schedules[&1], vec![(2, 0, 1)]);
    assert_eq!(schedules[&2], vec![(2, 0, 1)]);
    assert_eq!(schedules[&3], vec![(1, 0, 3)]);
    assert_eq!(compiler.finish_time(), 3);
}

// A move can need a chain of moves. Activity 3 can use only R1, which 1 took.
// Activity 1 can also use R2, which 2 took, and 2 can also use R3, which is free.
// Thus, 2 moves to R3, 1 moves to R2, and 3 starts on R1. Up to 4.1.1 activity 3
// waited until time 2, and R3 did nothing.
#[test]
fn vertex_graph_compiler_given_two_activities_that_must_both_move_then_a_chain_of_moves_frees_the_resource(
) {
    let (compiler, compilation) = compile(
        &[
            resource_of(1, true, 0),
            resource_of(2, true, 1),
            resource_of(3, true, 2),
        ],
        &[
            activity_of(1, 2, &[1, 2], LogicalOperator::Or, &[]),
            activity_of(2, 2, &[2, 3], LogicalOperator::Or, &[]),
            activity_of(3, 2, &[1], LogicalOperator::Or, &[]),
        ],
    );

    let schedules = schedules_of(&compilation);
    assert_eq!(schedules[&1], vec![(3, 0, 2)]);
    assert_eq!(schedules[&2], vec![(1, 0, 2)]);
    assert_eq!(schedules[&3], vec![(2, 0, 2)]);
    assert_eq!(compiler.finish_time(), 2);
}

// The limit of the rule: only an activity that started at the same moment moves.
// Activity 1 took R1 at time 0. Activity 2 can use only R1, and it cannot start
// before time 1. It waits until 1 finishes, and R2 does nothing, because the
// scheduler does not look ahead and it does not move an activity that has
// started.
#[test]
fn vertex_graph_compiler_given_activity_that_started_at_an_earlier_time_then_it_does_not_move() {
    let mut activity2 = activity_of(2, 1, &[1], LogicalOperator::Or, &[]);
    activity2.minimum_earliest_start_time = Some(1);

    let (compiler, compilation) = compile(
        &[resource_of(1, true, 0), resource_of(2, true, 1)],
        &[
            activity_of(1, 3, &[1, 2], LogicalOperator::Or, &[]),
            activity2,
        ],
    );

    let schedules = schedules_of(&compilation);
    assert_eq!(schedules[&1], vec![(1, 0, 3), (2, 3, 4)]);
    assert!(!schedules.contains_key(&2));
    assert_eq!(compiler.finish_time(), 4);
}

// With no other activity that wants a resource, an activity takes the first of
// its targets in the allocation order, whatever the IDs of the resources are.
// This is the setting of the user, and it must stay.
#[test]
fn vertex_graph_compiler_given_activity_with_two_targets_and_no_contention_then_it_takes_the_first_in_the_allocation_order(
) {
    for (allocation_order1, allocation_order2, expected_resource_id) in [(0, 1, 1), (1, 0, 2)] {
        let (_, compilation) = compile(
            &[
                resource_of(1, true, allocation_order1),
                resource_of(2, true, allocation_order2),
            ],
            &[
                activity_of(1, 2, &[1, 2], LogicalOperator::Or, &[]),
                activity_of(2, 2, &[1, 2], LogicalOperator::Or, &[1]),
            ],
        );

        let schedules = schedules_of(&compilation);
        assert_eq!(
            schedules.keys().copied().collect::<Vec<i32>>(),
            vec![expected_resource_id]
        );
        assert_eq!(schedules[&expected_resource_id], vec![(1, 0, 2), (2, 2, 4)]);
    }
}

// Two activities that can each use R1 or R2 are ready at the same time. The first
// in the priority list takes the first resource in the allocation order, and the
// second takes the other. Both start at once. Activity 3 makes activity 2 the one
// with the least slack, and so 2 is first in the priority list.
#[test]
fn vertex_graph_compiler_given_two_activities_with_the_same_two_targets_then_the_first_in_the_priority_list_takes_the_first_resource(
) {
    let (compiler, compilation) = compile(
        &[resource_of(1, true, 0), resource_of(2, true, 1)],
        &[
            activity_of(1, 3, &[1, 2], LogicalOperator::Or, &[]),
            activity_of(2, 5, &[1, 2], LogicalOperator::Or, &[]),
            activity_of(3, 1, &[1, 2], LogicalOperator::Or, &[1, 2]),
        ],
    );

    let schedules = schedules_of(&compilation);
    assert_eq!(schedules[&1], vec![(2, 0, 5), (3, 5, 6)]);
    assert_eq!(schedules[&2], vec![(1, 0, 3)]);
    assert_eq!(compiler.finish_time(), 6);
}

// An activity with no targets takes the first resource in the allocation order
// that is not an explicit target, and it never takes an explicit target.
#[test]
fn vertex_graph_compiler_given_activities_with_no_targets_then_they_take_the_resources_that_are_not_explicit_targets_in_the_allocation_order(
) {
    let (compiler, compilation) = compile(
        &[
            resource_of(1, true, 0),
            resource_of(2, false, 2),
            resource_of(3, false, 1),
        ],
        &[
            activity_of(1, 4, &[], LogicalOperator::And, &[]),
            activity_of(2, 3, &[], LogicalOperator::And, &[]),
            activity_of(3, 2, &[], LogicalOperator::And, &[]),
        ],
    );

    let schedules = schedules_of(&compilation);
    assert!(!schedules.contains_key(&1));
    assert_eq!(schedules[&3], vec![(1, 0, 4)]);
    assert_eq!(schedules[&2], vec![(2, 0, 3), (3, 3, 5)]);
    assert_eq!(compiler.finish_time(), 5);
}

// An activity that needs all of its targets starts when each of them is free, and
// it is on each of them.
#[test]
fn vertex_graph_compiler_given_activity_that_needs_all_of_its_targets_then_it_is_on_each_of_them() {
    for target_resource_operator in [LogicalOperator::And, LogicalOperator::ActiveAnd] {
        let (compiler, compilation) = compile(
            &[resource_of(1, true, 0), resource_of(2, true, 1)],
            &[
                activity_of(1, 3, &[2], LogicalOperator::Or, &[]),
                activity_of(2, 2, &[1, 2], target_resource_operator, &[]),
            ],
        );

        let schedules = schedules_of(&compilation);
        assert_eq!(
            schedules[&1],
            vec![(2, 3, 5)],
            "{target_resource_operator:?}"
        );
        assert_eq!(
            schedules[&2],
            vec![(1, 0, 3), (2, 3, 5)],
            "{target_resource_operator:?}"
        );
        let mut allocated_to_resources: Vec<i32> = compiled_activity(&compilation, 2)
            .allocated_to_resources
            .iter()
            .copied()
            .collect();
        allocated_to_resources.sort();
        assert_eq!(allocated_to_resources, vec![1, 2]);
        assert_eq!(compiler.finish_time(), 5);
    }
}

// Three activities want two resources, and each resource is taken. Activity 2 can
// use only R1, and it waits for it. No resource is free while it waits, and so no
// other choice of resource lets it start earlier.
#[test]
fn vertex_graph_compiler_given_more_ready_activities_than_resources_then_the_last_waits_and_no_resource_is_free(
) {
    let (compiler, compilation) = compile(
        &[resource_of(1, true, 0), resource_of(2, true, 1)],
        &[
            activity_of(1, 2, &[1, 2], LogicalOperator::Or, &[]),
            activity_of(2, 1, &[1], LogicalOperator::Or, &[]),
            activity_of(3, 1, &[2], LogicalOperator::Or, &[]),
        ],
    );

    let schedules = schedules_of(&compilation);
    assert_eq!(schedules[&1], vec![(1, 0, 2), (2, 2, 3)]);
    assert_eq!(schedules[&2], vec![(3, 0, 1)]);
    assert_eq!(compiler.finish_time(), 3);
}

// A small deterministic RNG for the random plans (the C# test uses
// `new Random(20261120)`, and Rust cannot reproduce its sequence). The plans are
// thus not the plans of the C# test. The test checks rules that each schedule
// must hold, and no values, and so other plans check the same thing.
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

    // `count` different resources from 1 to `resource_count`, in a random order.
    fn next_resources(&mut self, resource_count: i32, count: i32) -> Vec<i32> {
        let mut resources: Vec<i32> = (1..=resource_count).collect();
        for i in 0..count as usize {
            let j = self.next_range(i as i32, resource_count) as usize;
            resources.swap(i, j);
        }
        resources.truncate(count as usize);
        resources
    }
}

// Whatever resource each activity takes, the schedule holds the rules of the
// plan: no resource has two activities at once, each activity starts after the
// activities that it depends on, and each activity is on resources that it can
// use.
#[test]
fn vertex_graph_compiler_given_random_plans_with_targets_then_each_schedule_holds_the_rules_of_the_plan(
) {
    let mut random = Lcg::new(20261120);
    for plan in 0..300 {
        let resource_count = random.next_range(2, 5);
        let mut resources: Vec<Res> = Vec::new();
        for id in 1..=resource_count {
            let is_explicit_target = random.next_range(0, 2) == 0;
            let allocation_order = random.next_range(0, resource_count);
            resources.push(resource_of(id, is_explicit_target, allocation_order));
        }
        let any_open_resource = resources.iter().any(|x| !x.is_explicit_target);

        let count = random.next_range(4, 21);
        let mut activities: Vec<Act> = Vec::new();
        for id in 1..=count {
            let mut target_resource_operator = LogicalOperator::Or;
            let kind = random.next_range(0, 100);
            let targets: Vec<i32> = if kind < 30 && any_open_resource {
                Vec::new()
            } else if kind < 55 {
                vec![random.next_range(1, resource_count + 1)]
            } else if kind < 88 {
                let target_count = random.next_range(2, resource_count + 1);
                random.next_resources(resource_count, target_count)
            } else {
                let targets = random.next_resources(resource_count, 2);
                target_resource_operator = if random.next_range(0, 3) == 0 {
                    LogicalOperator::ActiveAnd
                } else {
                    LogicalOperator::And
                };
                targets
            };
            let mut dependencies: Vec<i32> = Vec::new();
            for dependency in 1..id {
                if random.next_range(0, count) < 2 {
                    dependencies.push(dependency);
                }
            }
            activities.push(activity_of(
                id,
                random.next_range(1, 6),
                &targets,
                target_resource_operator,
                &dependencies,
            ));
        }

        let (_, compilation) = compile(&resources, &activities);

        let compiled: IndexMap<i32, &Act> = compilation
            .dependent_activities
            .iter()
            .map(|x| (x.id(), x))
            .collect();
        let mut resources_of: IndexMap<i32, Vec<i32>> =
            activities.iter().map(|x| (x.id(), Vec::new())).collect();
        for schedule in &compilation.resource_schedules {
            let resource_id = schedule.resource.as_ref().expect("resource").id;
            let mut scheduled_activities: Vec<_> = schedule.scheduled_activities.iter().collect();
            scheduled_activities.sort_by_key(|x| x.start_time);
            let mut previous_finish_time: Option<i32> = None;
            for scheduled_activity in scheduled_activities {
                if let Some(previous_finish_time) = previous_finish_time {
                    assert!(
                        scheduled_activity.start_time >= previous_finish_time,
                        "plan {plan}, resource {resource_id}"
                    );
                }
                assert_eq!(
                    scheduled_activity.start_time,
                    compiled[&scheduled_activity.id]
                        .earliest_start_time
                        .unwrap_or(0),
                    "plan {plan}, activity {}",
                    scheduled_activity.id
                );
                resources_of[&scheduled_activity.id].push(resource_id);
                previous_finish_time = Some(scheduled_activity.finish_time);
            }
        }
        for activity in &activities {
            let id = activity.id();
            for dependency in &activity.dependencies {
                assert!(
                    compiled[&id].earliest_start_time.unwrap_or(0)
                        >= compiled[dependency].earliest_finish_time().unwrap_or(0),
                    "plan {plan}, activity {id} after {dependency}"
                );
            }

            let on = &resources_of[&id];
            if activity.target_resources.is_empty() {
                assert_eq!(on.len(), 1, "plan {plan}, activity {id}");
                let resource = resources
                    .iter()
                    .find(|x| x.id == on[0])
                    .expect("resource of the plan");
                assert!(!resource.is_explicit_target, "plan {plan}, activity {id}");
            } else if activity.target_resource_operator == LogicalOperator::Or {
                assert_eq!(on.len(), 1, "plan {plan}, activity {id}");
                assert!(
                    activity.target_resources.contains(&on[0]),
                    "plan {plan}, activity {id}"
                );
            } else {
                let mut on_sorted = on.clone();
                on_sorted.sort();
                let mut targets_sorted: Vec<i32> =
                    activity.target_resources.iter().copied().collect();
                targets_sorted.sort();
                assert_eq!(on_sorted, targets_sorted, "plan {plan}, activity {id}");
            }
        }
    }
}
