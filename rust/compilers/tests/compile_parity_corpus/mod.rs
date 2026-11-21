//! Random plans for the comparison of a whole compile between the two languages
//! - the counterpart of the C# `CompileParityCorpus`.
//!
//! The generator is the same as the C# one, value for value, and so the two
//! languages build the same plan for the same category and index. Each compiles
//! its plan, writes the result as one canonical text, and takes a hash of that
//! text. The hashes are in one baseline file, which C# writes and both check
//! (`compile_parity_tests.rs`).
//!
//! The plans come from the xorshift generator of [`crate::corpus_shapes`].
//!
//! A change here needs the same change in the C# file, and a new baseline. The
//! order of the calls to the generator is part of the format: one call more or
//! less on one side gives different plans.

#![allow(dead_code)]

use crate::corpus_shapes::DeterministicRandom;
use std::fmt::Write;
use zametek_maths_graphs_compilers::VertexGraphCompiler;
use zametek_maths_graphs_primitives::{
    DependentActivity, InterActivityAllocationType, LogicalOperator, Resource,
};

/// Plans with 2 to 4 resources and targets, and no constraints.
pub const RESOURCES: &str = "resources";

/// The same, with constraints (a minimum EST, a maximum LFT or a minimum free
/// slack) and milestones.
pub const RESOURCES_CONSTRAINED: &str = "resources-constrained";

/// The activities of `resources` with no targets, compiled with no resources.
pub const NO_RESOURCES: &str = "no-resources";

/// The activities of `resources-constrained` with no targets, compiled with no
/// resources.
pub const NO_RESOURCES_CONSTRAINED: &str = "no-resources-constrained";

/// In the order of the baseline file.
pub const CATEGORIES: [&str; 4] = [
    RESOURCES,
    RESOURCES_CONSTRAINED,
    NO_RESOURCES,
    NO_RESOURCES_CONSTRAINED,
];

pub struct ResourceSpec {
    pub id: i32,
    pub is_explicit_target: bool,
    pub allocation_order: i32,
}

pub struct ActivitySpec {
    pub id: i32,
    pub duration: i32,
    /// In ascending order.
    pub dependencies: Vec<i32>,
    /// In the order in which they are added to the activity. The order is part
    /// of the plan: the two languages must add the targets in the same order.
    pub target_resources: Vec<i32>,
    pub target_resource_operator: LogicalOperator,
    // At most one of the three constraints is set.
    pub minimum_earliest_start_time: Option<i32>,
    pub maximum_latest_finish_time: Option<i32>,
    pub minimum_free_slack: Option<i32>,
}

pub struct PlanSpec {
    pub category: String,
    pub index: i32,
    /// Empty for the categories with no resources. The compile gets them in
    /// this order.
    pub resources: Vec<ResourceSpec>,
    /// In ID order. The compile gets them in this order.
    pub activities: Vec<ActivitySpec>,
}

/// The result of the compile of one plan.
pub struct CompileResult {
    pub finish_time: i32,
    /// The error codes in order, with a comma between them, or `-` for none.
    pub error_codes: String,
    /// The canonical text of the whole result.
    pub text: String,
    /// The 64-bit FNV-1a hash of the text.
    pub hash: u64,
}

// -- Generator ---------------------------------------------------------------

/// The plan of one category and index. The same call gives the same plan each
/// time, in both languages.
pub fn generate(category: &str, index: i32) -> PlanSpec {
    let with_resources = category == RESOURCES || category == RESOURCES_CONSTRAINED;
    let constrained = category == RESOURCES_CONSTRAINED || category == NO_RESOURCES_CONSTRAINED;
    assert!(
        with_resources || category == NO_RESOURCES || category == NO_RESOURCES_CONSTRAINED,
        "unknown category: {category}"
    );

    // The multiplier is odd, and so each index has its own seed. The plans with
    // constraints have other seeds than the plans without. The first values of
    // an xorshift generator are much alike for seeds that are much alike, and so
    // the first eight are thrown away.
    let seed = (index as u32)
        .wrapping_add(1)
        .wrapping_mul(2_654_435_761)
        .wrapping_add(if constrained { 0x5BD1_E995 } else { 0 });
    let mut rng = DeterministicRandom::new(seed);
    for _ in 0..8 {
        rng.next(2);
    }

    // 2 to 4 resources. Each is an explicit target or not, and the allocation
    // orders can tie.
    let resource_count = rng.next_between(2, 5);
    let mut resources = Vec::new();
    let mut any_open_resource = false;
    for id in 1..=resource_count {
        let is_explicit_target = rng.next(2) == 0;
        let allocation_order = rng.next(resource_count);
        resources.push(ResourceSpec {
            id,
            is_explicit_target,
            allocation_order,
        });
        any_open_resource |= !is_explicit_target;
    }

    // 4 to 30 activities. Each earlier activity is a dependency with a chance of
    // 2 in the count, and of 0.3 at most.
    let count = rng.next_between(4, 31);
    let dependency_per_mille = (2000 / count).min(300);
    let mut activities = Vec::new();
    for id in 1..=count {
        let mut dependencies = Vec::new();
        for candidate in 1..id {
            if rng.next(1000) < dependency_per_mille {
                dependencies.push(candidate);
            }
        }

        let mut duration = rng.next_between(1, 6);

        // About 35% no targets, 25% one target, 30% two or more targets with OR,
        // and 10% two targets with AND or ACTIVE_AND. An activity with no targets
        // needs a resource that is not an explicit target. When the plan has
        // none, the activity gets one target, and so the compile never refuses
        // the plan with P0040.
        let mut target_resources = Vec::new();
        let mut target_resource_operator = LogicalOperator::And;
        let kind = rng.next(100);
        if kind < 35 && any_open_resource {
            // No targets.
        } else if kind < 60 {
            target_resources.push(rng.next_between(1, resource_count + 1));
            // With one target the operator changes nothing in the plan, but the
            // scheduler moves only an activity with OR to another resource.
            target_resource_operator = if rng.next(4) == 0 {
                LogicalOperator::And
            } else {
                LogicalOperator::Or
            };
        } else if kind < 90 {
            let target_count = rng.next_between(2, resource_count + 1);
            target_resources = pick_resources(&mut rng, resource_count, target_count);
            target_resource_operator = LogicalOperator::Or;
        } else {
            target_resources = pick_resources(&mut rng, resource_count, 2);
            target_resource_operator = if rng.next(3) == 0 {
                LogicalOperator::ActiveAnd
            } else {
                LogicalOperator::And
            };
        }

        let mut minimum_earliest_start_time = None;
        let mut maximum_latest_finish_time = None;
        let mut minimum_free_slack = None;
        if constrained {
            // About one activity in fifteen is a milestone.
            if rng.next(15) == 0 {
                duration = 0;
            }
            // About one activity in seven has one constraint. Never a minimum
            // free slack with a maximum LFT: the compile refuses that before it
            // starts (P0030).
            if rng.next(7) == 0 {
                let constraint = rng.next(3);
                if constraint == 0 {
                    minimum_earliest_start_time = Some(rng.next_between(1, 10));
                } else if constraint == 1 {
                    maximum_latest_finish_time = Some(rng.next_between(5, 60));
                } else {
                    minimum_free_slack = Some(rng.next_between(1, 4));
                }
            }
        }

        // The categories with no resources have the same activities with no
        // targets.
        if !with_resources {
            target_resources = Vec::new();
            target_resource_operator = LogicalOperator::And;
        }

        activities.push(ActivitySpec {
            id,
            duration,
            dependencies,
            target_resources,
            target_resource_operator,
            minimum_earliest_start_time,
            maximum_latest_finish_time,
            minimum_free_slack,
        });
    }

    PlanSpec {
        category: category.to_string(),
        index,
        resources: if with_resources {
            resources
        } else {
            Vec::new()
        },
        activities,
    }
}

/// `take` different resources of 1 to `resource_count`, in a random order: the
/// first steps of a Fisher-Yates shuffle.
fn pick_resources(rng: &mut DeterministicRandom, resource_count: i32, take: i32) -> Vec<i32> {
    let mut pool: Vec<i32> = (1..=resource_count).collect();
    for i in 0..take {
        let j = rng.next_between(i, resource_count);
        pool.swap(i as usize, j as usize);
    }
    pool.truncate(take as usize);
    pool
}

// -- Text --------------------------------------------------------------------

/// The plan as text, the same in both languages. A test that fails prints it, so
/// that the plans of the two sides can be put next to each other.
pub fn format_plan(plan: &PlanSpec) -> String {
    let mut output = String::new();
    let _ = writeln!(output, "plan {} {}", plan.category, plan.index);
    for resource in &plan.resources {
        let _ = writeln!(
            output,
            "resource {} explicit-target={} allocation-order={}",
            resource.id, resource.is_explicit_target, resource.allocation_order
        );
    }
    for activity in &plan.activities {
        let _ = writeln!(
            output,
            "activity {} duration={} dependencies={} targets={} operator={} min-est={} max-lft={} min-free-slack={}",
            activity.id,
            activity.duration,
            numbers(activity.dependencies.iter().copied()),
            numbers(activity.target_resources.iter().copied()),
            operator_name(activity.target_resource_operator),
            number(activity.minimum_earliest_start_time),
            number(activity.maximum_latest_finish_time),
            number(activity.minimum_free_slack)
        );
    }
    output
}

fn operator_name(logical_operator: LogicalOperator) -> &'static str {
    match logical_operator {
        LogicalOperator::And => "AND",
        LogicalOperator::Or => "OR",
        LogicalOperator::ActiveAnd => "ACTIVE_AND",
    }
}

/// `-` for a missing value.
fn number(value: Option<i32>) -> String {
    match value {
        Some(value) => value.to_string(),
        None => "-".to_string(),
    }
}

fn numbers(values: impl IntoIterator<Item = i32>) -> String {
    let joined: Vec<String> = values.into_iter().map(|x| x.to_string()).collect();
    format!("[{}]", joined.join(","))
}

fn sorted_numbers(values: impl IntoIterator<Item = i32>) -> String {
    let mut sorted: Vec<i32> = values.into_iter().collect();
    sorted.sort_unstable();
    numbers(sorted)
}

// -- Compile -----------------------------------------------------------------

/// Compiles the plan, and writes the whole result as one canonical text. The C#
/// original writes the same text for the same result, character for character:
///
/// ```text
/// errors C0010                     the error codes in order, with a comma between them; '-' for none
/// error C0010                      then each error: its code, and each line of its message after '| '
/// | Invalid activity constraints:
/// | 3 -> LatestFinishTime cannot be more than MaximumLatestFinishTime
/// |
/// finish 12                        the finish time of the compiler
/// activity 1 est=0 eft=3 lst=0 lft=3 free-slack=0 total-slack=0 allocated=[1] resource-dependencies=[] successors=[2,3]
/// schedule 2 finish=12 activities=[1:0:3,4:3:5]
/// ```
///
/// The activities are in ID order, and their three lists are sorted. The
/// schedules are in the order of the result: the ID of the resource (`-` for a
/// lane with no resource), the finish time of the schedule, and each scheduled
/// activity in the order of the result as id:start:finish. A missing value is
/// `-`. Each line ends with `\n`.
///
/// The line endings of an error message are made `\n`, and nothing else of it
/// changes: C# ends each line of a message with `Environment.NewLine`, which is
/// `"\r\n"` on Windows, and Rust ends it with `"\n"`.
pub fn compile(plan: &PlanSpec) -> CompileResult {
    let mut compiler: VertexGraphCompiler<i32, i32, i32> = VertexGraphCompiler::new();
    for spec in &plan.activities {
        let mut activity: DependentActivity<i32, i32, i32> = DependentActivity::with_dependencies(
            spec.id,
            spec.duration,
            spec.dependencies.iter().copied(),
        );
        activity.target_resource_operator = spec.target_resource_operator;
        activity.minimum_earliest_start_time = spec.minimum_earliest_start_time;
        activity.maximum_latest_finish_time = spec.maximum_latest_finish_time;
        activity.minimum_free_slack = spec.minimum_free_slack;
        for target_resource in &spec.target_resources {
            activity.target_resources.insert(*target_resource);
        }
        compiler.add_activity(activity);
    }

    let resources: Vec<Resource<i32, i32>> = plan
        .resources
        .iter()
        .map(|x| {
            Resource::new(
                x.id,
                Some(format!("R{}", x.id)),
                x.is_explicit_target,
                false,
                InterActivityAllocationType::Direct,
                1.0,
                1.0,
                x.allocation_order,
                [],
            )
        })
        .collect();

    // The C# compile throws where this one gives an error. Neither is a result
    // that the baseline can hold, and so it fails the test with the plan.
    let compilation = compiler
        .compile_with_resources(&resources)
        .unwrap_or_else(|error| panic!("the compile failed: {error}\n{}", format_plan(plan)));

    let error_codes = if compilation.compilation_errors.is_empty() {
        "-".to_string()
    } else {
        let codes: Vec<String> = compilation
            .compilation_errors
            .iter()
            .map(|x| format!("{:?}", x.error_code))
            .collect();
        codes.join(",")
    };

    let mut output = String::new();
    let _ = writeln!(output, "errors {error_codes}");
    for error in &compilation.compilation_errors {
        let _ = writeln!(output, "error {:?}", error.error_code);
        let message = error.error_message.replace("\r\n", "\n");
        for line in message.split('\n') {
            let _ = writeln!(output, "| {line}");
        }
    }

    let finish_time = compiler.finish_time();
    let _ = writeln!(output, "finish {finish_time}");

    let mut activities: Vec<&DependentActivity<i32, i32, i32>> =
        compilation.dependent_activities.iter().collect();
    activities.sort_by_key(|x| x.id());
    for activity in activities {
        let _ = writeln!(
            output,
            "activity {} est={} eft={} lst={} lft={} free-slack={} total-slack={} allocated={} resource-dependencies={} successors={}",
            activity.id(),
            number(activity.earliest_start_time),
            number(activity.earliest_finish_time()),
            number(activity.latest_start_time()),
            number(activity.latest_finish_time),
            number(activity.free_slack),
            number(activity.total_slack()),
            sorted_numbers(activity.allocated_to_resources.iter().copied()),
            sorted_numbers(activity.resource_dependencies.iter().copied()),
            sorted_numbers(activity.successors.iter().copied())
        );
    }

    for schedule in &compilation.resource_schedules {
        let resource_id = number(schedule.resource.as_ref().map(|x| x.id));
        let scheduled: Vec<String> = schedule
            .scheduled_activities
            .iter()
            .map(|x| format!("{}:{}:{}", x.id, x.start_time, x.finish_time))
            .collect();
        let _ = writeln!(
            output,
            "schedule {} finish={} activities=[{}]",
            resource_id,
            schedule.finish_time,
            scheduled.join(",")
        );
    }

    let hash = fnv1a64(&output);
    CompileResult {
        finish_time,
        error_codes,
        text: output,
        hash,
    }
}

/// The 64-bit FNV-1a hash of the UTF-8 bytes of the text. Written out here, and
/// in the same way in the C# original: a hash of a library can change, and then
/// the baseline is no longer valid.
pub fn fnv1a64(text: &str) -> u64 {
    let mut hash: u64 = 14_695_981_039_346_656_037;
    for value in text.bytes() {
        hash ^= u64::from(value);
        hash = hash.wrapping_mul(1_099_511_628_211);
    }
    hash
}

/// One line of the baseline file: category, index, finish time, error codes and
/// hash, with one space between them.
pub fn baseline_line(plan: &PlanSpec, result: &CompileResult) -> String {
    format!(
        "{} {} {} {} {:016x}",
        plan.category, plan.index, result.finish_time, result.error_codes, result.hash
    )
}
