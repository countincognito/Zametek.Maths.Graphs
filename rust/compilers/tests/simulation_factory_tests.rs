//! Port of `ScheduleSimulatorFactoryTests.cs`. A simulation schedules a plan with
//! the builders that a factory of its caller gives it, and so with the engines
//! of those builders. These tests show that the simulator uses the builders (and
//! the engines) of the factory, in the first check of the plan and in every
//! iteration, that a factory with the standard engines gives the result of the
//! simulator that `new` makes, that an engine that schedules in another way
//! changes the result, and that a builder that is not empty is refused.
//!
//! The first check of the plan is a compile, and the compiler for it comes from
//! the same factory. These tests also show that the simulator asks the factory
//! for one compiler, that the check is a compile by the compiler that it gives,
//! and that the iterations never ask for a compiler.
//!
//! The C# tests of the type of the activity, and of a factory that is missing or
//! that gives null, have no counterpart. The Rust simulator has one type of
//! activity, and a factory, and what it gives, cannot be null.

mod simulation_plans;

use indexmap::IndexSet;
use simulation_plans::Plan;
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Arc;
use zametek_maths_graphs_compilers::contracts::{
    IResourceSchedulingEngine, IResourceSchedulingGraph, IVertexGraphFactory,
};
use zametek_maths_graphs_compilers::primitives::{
    Activity, DependentActivity, GraphError, Resource, ResourceSchedule, SimulationSettings,
    UnavailableResources,
};
use zametek_maths_graphs_compilers::{
    PriorityListResourceScheduler, ScheduleSimulator, VertexGraphBuilder,
    VertexGraphBuilderEngines, VertexGraphCompiler, VertexGraphFactory,
};

type Builder = VertexGraphBuilder<i32, i32, i32>;
type Compiler = VertexGraphCompiler<i32, i32, i32>;
type Simulator = ScheduleSimulator<i32, i32, i32>;
type StandardFactory = VertexGraphFactory<i32, i32, i32>;

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

// The standard factory, with the given scheduler in place of the standard one.
fn factory_of(scheduler: Scheduler) -> StandardFactory {
    VertexGraphFactory::with_engines(VertexGraphBuilderEngines {
        resource_scheduling_engine: Arc::new(scheduler),
        ..Default::default()
    })
}

// Passes every call to the factory that it wraps, and counts the builders and
// the compilers that it is asked for.
struct CountingFactory {
    inner: StandardFactory,
    builders: Arc<AtomicUsize>,
    compilers: Arc<AtomicUsize>,
}

impl CountingFactory {
    // The factory, and the counters of the builders and of the compilers.
    fn new(inner: StandardFactory) -> (Self, Arc<AtomicUsize>, Arc<AtomicUsize>) {
        let builders = Arc::new(AtomicUsize::new(0));
        let compilers = Arc::new(AtomicUsize::new(0));
        (
            Self {
                inner,
                builders: Arc::clone(&builders),
                compilers: Arc::clone(&compilers),
            },
            builders,
            compilers,
        )
    }
}

impl IVertexGraphFactory<i32, i32, i32> for CountingFactory {
    fn create_builder(&self) -> Builder {
        self.builders.fetch_add(1, Ordering::Relaxed);
        self.inner.create_builder()
    }

    fn create_compiler(&self) -> Compiler {
        self.compilers.fetch_add(1, Ordering::Relaxed);
        self.inner.create_compiler()
    }
}

// Makes the builders with one factory and the compilers with another, so that a
// test can give the compiler engines that are not the ones of the builders.
struct SplitFactory {
    builders: StandardFactory,
    compilers: StandardFactory,
}

impl IVertexGraphFactory<i32, i32, i32> for SplitFactory {
    fn create_builder(&self) -> Builder {
        self.builders.create_builder()
    }

    fn create_compiler(&self) -> Compiler {
        self.compilers.create_compiler()
    }
}

// A factory that does not do its job: its builders already hold an activity of
// the plan.
struct FilledBuilderFactory {
    activity: DependentActivity<i32, i32, i32>,
    inner: StandardFactory,
}

impl IVertexGraphFactory<i32, i32, i32> for FilledBuilderFactory {
    fn create_builder(&self) -> Builder {
        let mut builder = self.inner.create_builder();
        builder.add_activity(self.activity.clone());
        builder
    }

    fn create_compiler(&self) -> Compiler {
        self.inner.create_compiler()
    }
}

fn simulator_of(
    plan: &Plan,
    factory: impl IVertexGraphFactory<i32, i32, i32> + 'static,
) -> Simulator {
    ScheduleSimulator::with_factory(
        &plan.activities(),
        &plan.resources(true),
        &[],
        &plan.estimates(),
        factory,
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

// The finish time of a compile of the plan by a compiler of a factory with the
// given scheduler. This is what the check of a simulator gives when the
// compiler of its factory has that scheduler.
fn finish_of(plan: &Plan, scheduler: Scheduler) -> i32 {
    let mut compiler = factory_of(scheduler).create_compiler();
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

// -- The builders of the factory ----------------------------------------------

#[test]
fn run_given_a_factory_with_the_standard_engines_then_gives_the_result_of_the_simulator_that_new_makes(
) {
    let plan = simulation_plans::layered(40, 5, 4);
    let standard = standard_simulator_of(&plan);
    let with_factory = simulator_of(&plan, StandardFactory::new());

    let expected = standard.run(&settings_of(300, 4)).unwrap();
    let actual = with_factory.run(&settings_of(300, 4)).unwrap();

    assert_eq!(actual, expected);
    assert_eq!(
        with_factory.planned_finish_time(),
        standard.planned_finish_time()
    );
    assert!(with_factory.compilation_errors().is_empty());
}

#[test]
fn run_given_a_scheduler_that_counts_its_calls_then_the_simulator_uses_the_engines_of_the_factory_for_the_check_and_for_every_iteration(
) {
    let schedules = Arc::new(AtomicUsize::new(0));
    let (factory, builders, _) = CountingFactory::new(factory_of(counting(&schedules)));
    let simulator = simulator_of(&simulation_plans::layered(40, 5, 4), factory);

    // The first check of the plan is a compile, and a compile schedules the plan once.
    assert!(count(&schedules) > 0);
    let check_schedules = count(&schedules);

    // One thread asks for one builder, and each iteration schedules the plan once.
    let result = simulator.run(&settings_of(100, 1)).unwrap();

    assert_eq!(result.failed_iteration_count(), 0);
    assert_eq!(count(&builders), 1);
    assert_eq!(count(&schedules) - check_schedules, 100);
}

#[test]
fn run_given_many_threads_then_each_thread_has_a_builder_of_its_own() {
    let (factory, builders, _) = CountingFactory::new(StandardFactory::new());
    let simulator = simulator_of(&simulation_plans::layered(40, 5, 4), factory);

    simulator.run(&settings_of(640, 4)).unwrap();

    // One for each thread that the run starts. The iterations are 10 chunks of
    // 64, and so the run starts the 4 threads.
    assert_eq!(count(&builders), 4);
}

#[test]
fn run_given_a_scheduler_that_schedules_in_another_order_then_the_result_is_not_the_standard_one() {
    // Few resources for many activities, so that the order in which they are
    // scheduled decides the finish.
    let plan = simulation_plans::layered(100, 10, 3);
    let standard = standard_simulator_of(&plan)
        .run(&settings_of(120, 2))
        .unwrap();

    let simulator = simulator_of(&plan, factory_of(reversing()));
    assert!(simulator.compilation_errors().is_empty());
    let actual = simulator.run(&settings_of(120, 2)).unwrap();

    assert_ne!(actual.finish_times(), standard.finish_times());
}

// -- The compiler of the factory ----------------------------------------------

#[test]
fn with_factory_given_a_factory_then_asks_for_one_compiler_and_for_no_builder() {
    let (factory, builders, compilers) = CountingFactory::new(StandardFactory::new());

    let simulator = simulator_of(&simulation_plans::layered(40, 5, 4), factory);

    // The compiler comes with a builder of its own, and the iterations have not
    // started.
    assert_eq!(count(&compilers), 1);
    assert_eq!(count(&builders), 0);
    assert!(simulator.compilation_errors().is_empty());
}

#[test]
fn run_given_many_threads_then_the_factory_is_not_asked_for_another_compiler() {
    let (factory, builders, compilers) = CountingFactory::new(StandardFactory::new());
    let simulator = simulator_of(&simulation_plans::layered(40, 5, 4), factory);

    simulator.run(&settings_of(640, 4)).unwrap();

    // Only the check of the plan is a compile. An iteration schedules with the
    // builder of its thread.
    assert_eq!(count(&compilers), 1);
    assert!(count(&builders) > 0);
}

#[test]
fn with_factory_given_a_compiler_with_other_engines_then_the_check_of_the_plan_is_a_compile_by_that_compiler(
) {
    let of_the_builders = Arc::new(AtomicUsize::new(0));
    let of_the_compiler = Arc::new(AtomicUsize::new(0));

    let simulator = simulator_of(
        &simulation_plans::layered(40, 5, 4),
        SplitFactory {
            builders: factory_of(counting(&of_the_builders)),
            compilers: factory_of(counting(&of_the_compiler)),
        },
    );

    // The compile went through the compiler. The builders of the factory have
    // scheduled nothing yet.
    assert!(simulator.compilation_errors().is_empty());
    assert!(count(&of_the_compiler) > 0);
    assert_eq!(count(&of_the_builders), 0);
}

#[test]
fn with_factory_given_a_compiler_of_another_scheduler_then_the_planned_finish_is_the_finish_of_that_compiler(
) {
    // Few resources for many activities, so that the order in which they are
    // scheduled decides the finish.
    let plan = simulation_plans::layered(100, 10, 3);
    let standard = standard_simulator_of(&plan).planned_finish_time();
    let reversed = finish_of(&plan, reversing());
    assert_ne!(reversed, standard);

    // The builders of the factory have the standard engines, and the compiler
    // reverses.
    let simulator = simulator_of(
        &plan,
        SplitFactory {
            builders: StandardFactory::new(),
            compilers: factory_of(reversing()),
        },
    );

    assert_eq!(simulator.planned_finish_time(), reversed);
}

// -- A factory that does not do its job ---------------------------------------

#[test]
fn run_given_a_factory_that_gives_a_builder_that_is_not_empty_then_fails() {
    let plan = Plan::fixed(&[(1, 4, &[]), (2, 6, &[1])]);
    let first = plan.activities().remove(0);
    let simulator = simulator_of(
        &plan,
        FilledBuilderFactory {
            activity: first,
            inner: StandardFactory::new(),
        },
    );

    // The check of the plan passes, and an iteration needs a new, empty builder.
    assert!(simulator.compilation_errors().is_empty());
    let error = simulator.run(&settings_of(64, 1)).unwrap_err();

    assert!(error.message().contains("empty"), "{}", error.message());
}
