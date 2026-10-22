//! Port of `ScheduleSimulatorBuilderTests.cs`. A simulation schedules a plan with
//! the builder that its caller gives it, and so with the engines of that
//! builder. These tests show that the simulator uses the builder (and the
//! engines) of the builder factory, in the first check of the plan and in every
//! iteration, that a builder with the standard engines gives the result of the
//! simulator that `new` makes, that an engine that schedules in another way
//! changes the result, and that a builder that is not empty is refused.
//!
//! The first check of the plan is a compile, and the compiler for it comes from a
//! second factory of the caller, which is given the builder of the check. These
//! tests also show that the simulator asks that factory once, that the check is
//! a compile by the compiler that it gives, and that the iterations never ask
//! for a compiler.
//!
//! The C# tests of the type of the activity and of a factory that is null or
//! gives null have no counterpart. The Rust simulator has one type of activity,
//! and a closure cannot be null.

mod simulation_plans;

use indexmap::IndexSet;
use simulation_plans::Plan;
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Arc;
use zametek_maths_graphs_compilers::contracts::{
    IResourceSchedulingEngine, IResourceSchedulingGraph,
};
use zametek_maths_graphs_compilers::primitives::{
    Activity, GraphError, Resource, ResourceSchedule, SimulationSettings, UnavailableResources,
};
use zametek_maths_graphs_compilers::{
    PriorityListResourceScheduler, ScheduleSimulator, VertexGraphBuilder,
    VertexGraphBuilderEngines, VertexGraphCompiler,
};

type Builder = VertexGraphBuilder<i32, i32, i32>;
type Compiler = VertexGraphCompiler<i32, i32, i32>;
type Simulator = ScheduleSimulator<i32, i32, i32>;

// -- Helpers ------------------------------------------------------------------

// A resource scheduler that hands everything to the standard one. It counts the
// plans that it is asked to schedule, if it is given a counter, and it schedules
// in the reverse of the order of the priority list, if it is told to.
struct Scheduler {
    schedules: Option<Arc<AtomicUsize>>,
    reverse: bool,
}

impl IResourceSchedulingEngine<i32, i32, i32> for Scheduler {
    fn calculate_resource_schedules(
        &self,
        priority_list: &[i32],
        filtered_resources: &[Resource<i32, i32>],
        infinite_resources: bool,
        graph: &mut dyn IResourceSchedulingGraph<i32, i32, i32>,
    ) -> Result<Vec<ResourceSchedule<i32, i32, i32>>, GraphError> {
        if let Some(schedules) = &self.schedules {
            schedules.fetch_add(1, Ordering::Relaxed);
        }
        if self.reverse {
            let reversed: Vec<i32> = priority_list.iter().rev().copied().collect();
            PriorityListResourceScheduler.calculate_resource_schedules(
                &reversed,
                filtered_resources,
                infinite_resources,
                graph,
            )
        } else {
            PriorityListResourceScheduler.calculate_resource_schedules(
                priority_list,
                filtered_resources,
                infinite_resources,
                graph,
            )
        }
    }

    fn gather_unavailable_resources(
        &self,
        activities: &[&Activity<i32, i32, i32>],
        filtered_resources: &[Resource<i32, i32>],
    ) -> Vec<UnavailableResources<i32, i32>> {
        PriorityListResourceScheduler.gather_unavailable_resources(activities, filtered_resources)
    }

    fn replace_with_synthetic_resources(
        &self,
        resource_schedules: Vec<ResourceSchedule<i32, i32, i32>>,
    ) -> Vec<ResourceSchedule<i32, i32, i32>> {
        PriorityListResourceScheduler.replace_with_synthetic_resources(resource_schedules)
    }

    fn rebuild_aligned_resource_schedules(
        &self,
        resource_schedules: &[ResourceSchedule<i32, i32, i32>],
        infinite_resources: bool,
        graph: &dyn IResourceSchedulingGraph<i32, i32, i32>,
        final_activities: &[Activity<i32, i32, i32>],
        start_time: i32,
        finish_time: i32,
    ) -> Result<Vec<ResourceSchedule<i32, i32, i32>>, GraphError> {
        PriorityListResourceScheduler.rebuild_aligned_resource_schedules(
            resource_schedules,
            infinite_resources,
            graph,
            final_activities,
            start_time,
            finish_time,
        )
    }

    fn collect_indirect_resource_schedules(
        &self,
        filtered_resources: &[Resource<i32, i32>],
        scheduled_resources: &[ResourceSchedule<i32, i32, i32>],
        final_activities: &[Activity<i32, i32, i32>],
        start_time: i32,
        finish_time: i32,
    ) -> Result<Vec<ResourceSchedule<i32, i32, i32>>, GraphError> {
        PriorityListResourceScheduler.collect_indirect_resource_schedules(
            filtered_resources,
            scheduled_resources,
            final_activities,
            start_time,
            finish_time,
        )
    }

    fn get_resource_phases_used(
        &self,
        total_schedules: &[ResourceSchedule<i32, i32, i32>],
        workstreams_used: &IndexSet<i32>,
    ) -> IndexSet<i32> {
        PriorityListResourceScheduler.get_resource_phases_used(total_schedules, workstreams_used)
    }
}

// A scheduler that counts the plans that it schedules into the counter.
fn counting(schedules: &Arc<AtomicUsize>) -> Scheduler {
    Scheduler {
        schedules: Some(Arc::clone(schedules)),
        reverse: false,
    }
}

fn reversing() -> Scheduler {
    Scheduler {
        schedules: None,
        reverse: true,
    }
}

fn builder_of(scheduler: Scheduler) -> Builder {
    VertexGraphBuilder::with_engines(VertexGraphBuilderEngines {
        resource_scheduling_engine: Arc::new(scheduler),
        ..Default::default()
    })
}

// A builder with the standard engines.
fn standard_builder() -> Builder {
    VertexGraphBuilder::with_engines(VertexGraphBuilderEngines::default())
}

fn simulator_of(
    plan: &Plan,
    builder_factory: impl Fn() -> Builder + Send + Sync + 'static,
    compiler_factory: impl FnOnce(Builder) -> Compiler,
) -> Simulator {
    ScheduleSimulator::with_factories(
        &plan.activities(),
        &plan.resources(true),
        &[],
        &plan.estimates(),
        builder_factory,
        compiler_factory,
    )
    .unwrap()
}

// The simulator that `new` makes: the standard engines, and no factory.
fn standard_simulator_of(plan: &Plan) -> Simulator {
    ScheduleSimulator::new(
        &plan.activities(),
        &plan.resources(true),
        &[],
        &plan.estimates(),
    )
    .unwrap()
}

fn settings_of(iterations: usize, threads: usize) -> SimulationSettings {
    SimulationSettings {
        iterations,
        seed: 4242,
        max_degree_of_parallelism: threads,
        ..SimulationSettings::default()
    }
}

// The finish time of a compile of the plan by a compiler around a builder with
// the given scheduler. This is what the check of a simulator gives when its
// compiler has that scheduler.
fn finish_of(plan: &Plan, scheduler: Scheduler) -> i32 {
    let mut compiler = Compiler::with_builder(builder_of(scheduler));
    for activity in plan.activities() {
        compiler.add_activity(activity);
    }
    let compilation = compiler
        .compile_with_resources(&plan.resources(true))
        .unwrap();
    assert!(compilation.compilation_errors.is_empty());
    compiler.finish_time()
}

fn count(counter: &AtomicUsize) -> usize {
    counter.load(Ordering::Relaxed)
}

// -- The engines of the builder factory ---------------------------------------

#[test]
fn run_given_a_builder_with_the_standard_engines_then_gives_the_result_of_the_simulator_that_new_makes(
) {
    let plan = simulation_plans::layered(40, 5, 4);
    let standard = standard_simulator_of(&plan);
    let with_factories = simulator_of(&plan, standard_builder, Compiler::with_builder);

    let expected = standard.run(&settings_of(300, 4)).unwrap();
    let actual = with_factories.run(&settings_of(300, 4)).unwrap();

    assert_eq!(actual, expected);
    assert_eq!(
        with_factories.planned_finish_time(),
        standard.planned_finish_time()
    );
    assert!(with_factories.compilation_errors().is_empty());
}

#[test]
fn run_given_a_scheduler_that_counts_its_calls_then_the_simulator_uses_the_builder_of_the_factory_for_the_check_and_for_every_iteration(
) {
    let schedules = Arc::new(AtomicUsize::new(0));
    let builders = Arc::new(AtomicUsize::new(0));
    let simulator = simulator_of(
        &simulation_plans::layered(40, 5, 4),
        {
            let (schedules, builders) = (Arc::clone(&schedules), Arc::clone(&builders));
            move || {
                builders.fetch_add(1, Ordering::Relaxed);
                builder_of(counting(&schedules))
            }
        },
        Compiler::with_builder,
    );

    // The first check of the plan is a compile, and a compile schedules the plan once.
    assert_eq!(count(&builders), 1);
    assert!(count(&schedules) > 0);
    let check_schedules = count(&schedules);

    // One thread asks for one builder, and each iteration schedules the plan once.
    let result = simulator.run(&settings_of(100, 1)).unwrap();

    assert_eq!(result.failed_iteration_count(), 0);
    assert_eq!(count(&builders), 2);
    assert_eq!(count(&schedules) - check_schedules, 100);
}

#[test]
fn run_given_many_threads_then_each_thread_has_a_builder_of_its_own() {
    let builders = Arc::new(AtomicUsize::new(0));
    let simulator = simulator_of(
        &simulation_plans::layered(40, 5, 4),
        {
            let builders = Arc::clone(&builders);
            move || {
                builders.fetch_add(1, Ordering::Relaxed);
                standard_builder()
            }
        },
        Compiler::with_builder,
    );

    simulator.run(&settings_of(640, 4)).unwrap();

    // One for the check, and then one for each thread that the run starts. The
    // iterations are 10 chunks of 64, and so the run starts the 4 threads.
    assert_eq!(count(&builders), 1 + 4);
}

#[test]
fn run_given_a_scheduler_that_schedules_in_another_order_then_the_result_is_not_the_standard_one() {
    // Few resources for many activities, so that the order in which they are
    // scheduled decides the finish.
    let plan = simulation_plans::layered(100, 10, 3);
    let standard = standard_simulator_of(&plan)
        .run(&settings_of(120, 2))
        .unwrap();

    let simulator = simulator_of(&plan, || builder_of(reversing()), Compiler::with_builder);
    assert!(simulator.compilation_errors().is_empty());
    let actual = simulator.run(&settings_of(120, 2)).unwrap();

    assert_ne!(actual.finish_times(), standard.finish_times());
}

// -- The compiler of the compiler factory -------------------------------------

#[test]
fn with_factories_given_a_compiler_factory_then_asks_for_one_compiler_for_the_builder_of_the_check()
{
    let schedules = Arc::new(AtomicUsize::new(0));
    let builders = Arc::new(AtomicUsize::new(0));
    let compilers = Arc::new(AtomicUsize::new(0));
    let builders_made_when_asked = Arc::new(AtomicUsize::new(0));

    let simulator = simulator_of(
        &simulation_plans::layered(40, 5, 4),
        {
            let (schedules, builders) = (Arc::clone(&schedules), Arc::clone(&builders));
            move || {
                builders.fetch_add(1, Ordering::Relaxed);
                builder_of(counting(&schedules))
            }
        },
        {
            let (builders, compilers, asked) = (
                Arc::clone(&builders),
                Arc::clone(&compilers),
                Arc::clone(&builders_made_when_asked),
            );
            move |builder| {
                compilers.fetch_add(1, Ordering::Relaxed);
                asked.store(builders.load(Ordering::Relaxed), Ordering::Relaxed);
                Compiler::with_builder(builder)
            }
        },
    );

    // The builder of the check is the one that the builder factory made: it was
    // the only one made when the compiler was asked for, and the compile went
    // through it.
    assert_eq!(count(&compilers), 1);
    assert_eq!(count(&builders_made_when_asked), 1);
    assert_eq!(count(&builders), 1);
    assert!(count(&schedules) > 0);
    assert!(simulator.compilation_errors().is_empty());
}

#[test]
fn run_given_many_threads_then_the_compiler_factory_is_not_asked_again() {
    let builders = Arc::new(AtomicUsize::new(0));
    let compilers = Arc::new(AtomicUsize::new(0));
    let simulator = simulator_of(
        &simulation_plans::layered(40, 5, 4),
        {
            let builders = Arc::clone(&builders);
            move || {
                builders.fetch_add(1, Ordering::Relaxed);
                standard_builder()
            }
        },
        {
            let compilers = Arc::clone(&compilers);
            move |builder| {
                compilers.fetch_add(1, Ordering::Relaxed);
                Compiler::with_builder(builder)
            }
        },
    );

    simulator.run(&settings_of(640, 4)).unwrap();

    // Only the check of the plan is a compile. An iteration schedules with the
    // builder of its thread.
    assert_eq!(count(&compilers), 1);
    assert!(count(&builders) > 1);
}

#[test]
fn with_factories_given_a_compiler_over_another_builder_then_the_check_of_the_plan_is_a_compile_by_that_compiler(
) {
    let of_the_builder_factory = Arc::new(AtomicUsize::new(0));
    let of_the_compiler = Arc::new(AtomicUsize::new(0));

    let simulator = simulator_of(
        &simulation_plans::layered(40, 5, 4),
        {
            let schedules = Arc::clone(&of_the_builder_factory);
            move || builder_of(counting(&schedules))
        },
        {
            let schedules = Arc::clone(&of_the_compiler);
            move |_| Compiler::with_builder(builder_of(counting(&schedules)))
        },
    );

    // The compile went through the compiler. The builder that the builder
    // factory made has scheduled nothing.
    assert!(simulator.compilation_errors().is_empty());
    assert!(count(&of_the_compiler) > 0);
    assert_eq!(count(&of_the_builder_factory), 0);
}

#[test]
fn with_factories_given_a_compiler_of_another_scheduler_then_the_planned_finish_is_the_finish_of_that_compiler(
) {
    // Few resources for many activities, so that the order in which they are
    // scheduled decides the finish.
    let plan = simulation_plans::layered(100, 10, 3);
    let standard = standard_simulator_of(&plan).planned_finish_time();
    let reversed = finish_of(&plan, reversing());
    assert_ne!(reversed, standard);

    // The builder of the factory has the standard engines, and the compiler is
    // around a builder that reverses.
    let simulator = simulator_of(&plan, standard_builder, |_| {
        Compiler::with_builder(builder_of(reversing()))
    });

    assert_eq!(simulator.planned_finish_time(), reversed);
}

// -- A factory that does not do its job ---------------------------------------

#[test]
fn run_given_a_factory_that_gives_a_builder_that_is_not_empty_then_fails() {
    let plan = Plan::fixed(&[(1, 4, &[]), (2, 6, &[1])]);
    let first = plan.activities().remove(0);
    let simulator = simulator_of(
        &plan,
        move || {
            // A builder that already holds an activity of the plan.
            let mut builder = standard_builder();
            builder.add_activity(first.clone());
            builder
        },
        Compiler::with_builder,
    );

    // The check of the plan passes, and an iteration needs a new, empty builder.
    assert!(simulator.compilation_errors().is_empty());
    let error = simulator.run(&settings_of(64, 1)).unwrap_err();

    assert!(error.message().contains("empty"), "{}", error.message());
}
