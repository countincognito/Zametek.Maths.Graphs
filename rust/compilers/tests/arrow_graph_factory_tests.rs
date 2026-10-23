//! Port of `ArrowGraphFactoryTests.cs`. The standard factory of
//! Activity-on-Arrow graphs makes new, independent builders and compilers.
//! They have the engines of the bundle that the factory was made from (the
//! standard engines, for `new`), each builder asks the ID generator factories of
//! the bundle for generators of its own, and many threads can use the factory.
//!
//! The C# tests of a null bundle, of a bundle with a null engine, and of a
//! bundle that is changed after the factory is made have no counterpart: a
//! bundle in Rust cannot hold null, and it is moved into the factory.

mod counting_engines;

use counting_engines::counted_arrow_engines;
use indexmap::IndexSet;
use std::sync::Arc;
use zametek_maths_graphs_compilers::contracts::IArrowGraphFactory;
use zametek_maths_graphs_compilers::primitives::{
    DependentActivity, InterActivityAllocationType, Resource,
};
use zametek_maths_graphs_compilers::{
    ArrowGraphBuilder, ArrowGraphBuilderEngines, ArrowGraphCompiler, ArrowGraphFactory,
    NextIdGeneratorFactory,
};

type Act = DependentActivity<i32, i32, i32>;
type Builder = ArrowGraphBuilder<i32, i32, i32>;
type Compiler = ArrowGraphCompiler<i32, i32, i32>;

// -- Helpers ------------------------------------------------------------------

fn one_resource() -> Vec<Resource<i32, i32>> {
    vec![Resource::new(
        1,
        Some(String::new()),
        false,
        false,
        InterActivityAllocationType::None,
        0.0,
        0.0,
        0,
        Vec::<i32>::new(),
    )]
}

// A small plan in which 3 needs 1 and 2, and 4 needs only 2. The arrow network
// needs a dummy activity for that, and the plan finishes at 9.
const PLAN_FINISH: i32 = 9;

fn add_plan_to_builder(builder: &mut Builder) {
    assert!(builder.add_activity(Act::new(1, 3)));
    assert!(builder.add_activity(Act::new(2, 5)));
    assert!(builder.add_activity_with_dependencies(Act::new(3, 2), IndexSet::from([1, 2])));
    assert!(builder.add_activity_with_dependencies(Act::new(4, 4), IndexSet::from([2])));
}

fn add_plan_to_compiler(compiler: &mut Compiler) {
    assert!(compiler.add_activity(Act::new(1, 3)));
    assert!(compiler.add_activity(Act::new(2, 5)));
    assert!(compiler.add_activity(Act::with_dependencies(3, 2, [1, 2])));
    assert!(compiler.add_activity(Act::with_dependencies(4, 4, [2])));
}

// Takes a builder through every engine: the events, the dummy activities, the
// finding of cycles, the reduction, the critical path and the scheduling.
fn drive_builder(builder: &mut Builder) {
    add_plan_to_builder(builder);
    assert!(builder.transitive_reduction().unwrap());
    builder.calculate_critical_path().unwrap();
    let schedules = builder
        .calculate_resource_schedules_by_priority_list(&one_resource())
        .unwrap();
    assert!(!schedules.is_empty());
}

// Takes a compiler through the engines that a compile uses. An arrow compile
// does not schedule resources.
fn drive_compiler(compiler: &mut Compiler) {
    add_plan_to_compiler(compiler);
    compiler.compile().unwrap();
}

// -- The bundle ---------------------------------------------------------------

#[test]
fn the_factory_is_send_and_sync() {
    fn check<T: Send + Sync>() {}
    check::<ArrowGraphFactory<i32, i32, i32>>();
}

#[test]
fn with_engines_given_a_bundle_then_a_builder_uses_every_engine_of_it() {
    let (bundle, counters) = counted_arrow_engines();
    let factory = ArrowGraphFactory::with_engines(bundle);

    drive_builder(&mut factory.create_builder());

    assert!(counters.idle().is_empty(), "idle: {:?}", counters.idle());
}

#[test]
fn with_engines_given_a_bundle_then_a_compiler_uses_the_engines_of_it_that_a_compile_uses() {
    let (bundle, counters) = counted_arrow_engines();
    let factory = ArrowGraphFactory::with_engines(bundle);

    drive_compiler(&mut factory.create_compiler());

    // An arrow compile reduces the graph and finds the critical path, and it
    // schedules no resources.
    assert_eq!(counters.idle(), vec!["resource_scheduling_engine"]);
}

#[test]
fn with_engines_given_a_bundle_then_all_the_builders_share_its_engines() {
    let (bundle, counters) = counted_arrow_engines();
    let factory = ArrowGraphFactory::with_engines(bundle);

    drive_builder(&mut factory.create_builder());
    let after_first = counters.calls_to("critical_path_engine");
    drive_builder(&mut factory.create_builder());

    // The second builder called the engine that the first had called, and not an
    // engine of its own.
    assert!(after_first > 0);
    assert!(counters.calls_to("critical_path_engine") > after_first);
}

#[test]
fn with_engines_given_a_bundle_with_id_generator_factories_then_the_builders_have_generators_from_them(
) {
    let factory = ArrowGraphFactory::with_engines(ArrowGraphBuilderEngines {
        edge_id_generator_factory: Arc::new(NextIdGeneratorFactory::new(1_000_000)),
        node_id_generator_factory: Arc::new(NextIdGeneratorFactory::new(2_000_000)),
        ..Default::default()
    });

    let mut builder = factory.create_builder();
    add_plan_to_builder(&mut builder);

    // The generators of the builder step up from 1,000,000 and from 2,000,000,
    // and not down from 0 as the standard ones do. The activities (1 to 4) are
    // the only edges with IDs below 1,000,000.
    assert!(!builder.node_ids().is_empty());
    assert!(builder.node_ids().iter().all(|x| *x > 2_000_000));
    let dummies: Vec<i32> = builder
        .edge_ids()
        .into_iter()
        .filter(|x| ![1, 2, 3, 4].contains(x))
        .collect();
    assert!(!dummies.is_empty());
    assert!(dummies.iter().all(|x| *x > 1_000_000));
}

#[test]
fn with_engines_given_a_bundle_then_each_builder_asks_the_id_generator_factories_for_one_generator_each(
) {
    let (bundle, counters) = counted_arrow_engines();
    let factory = ArrowGraphFactory::with_engines(bundle);

    factory.create_builder();
    assert_eq!(counters.calls_to("edge_id_generator_factory"), 1);
    assert_eq!(counters.calls_to("node_id_generator_factory"), 1);
    factory.create_builder();
    assert_eq!(counters.calls_to("edge_id_generator_factory"), 2);
    assert_eq!(counters.calls_to("node_id_generator_factory"), 2);
    factory.create_compiler();
    assert_eq!(counters.calls_to("edge_id_generator_factory"), 3);
    assert_eq!(counters.calls_to("node_id_generator_factory"), 3);
}

#[test]
fn clone_builder_then_the_clone_asks_the_same_id_generator_factories() {
    let (bundle, counters) = counted_arrow_engines();
    let factory = ArrowGraphFactory::with_engines(bundle);
    let mut builder = factory.create_builder();
    add_plan_to_builder(&mut builder);
    let edge_calls_before_clone = counters.calls_to("edge_id_generator_factory");
    let node_calls_before_clone = counters.calls_to("node_id_generator_factory");

    builder.clone_builder().unwrap();

    assert_eq!(
        counters.calls_to("edge_id_generator_factory"),
        edge_calls_before_clone + 1
    );
    assert_eq!(
        counters.calls_to("node_id_generator_factory"),
        node_calls_before_clone + 1
    );
}

// -- The builders and the compilers -------------------------------------------

#[test]
fn create_builder_then_gives_a_new_empty_builder_each_time() {
    let factory = ArrowGraphFactory::<i32, i32, i32>::new();

    let first = factory.create_builder();
    let second = factory.create_builder();

    assert!(first.activity_ids().is_empty());
    assert!(second.activity_ids().is_empty());
}

#[test]
fn create_builder_then_the_graph_of_one_builder_is_not_the_graph_of_the_other() {
    let factory = ArrowGraphFactory::<i32, i32, i32>::new();
    let mut first = factory.create_builder();
    let second = factory.create_builder();

    add_plan_to_builder(&mut first);

    assert!(first.activity_ids().contains(&1));
    assert!(second.activity_ids().is_empty());
}

#[test]
fn create_builder_then_each_builder_has_id_generators_of_its_own() {
    let factory = ArrowGraphFactory::<i32, i32, i32>::new();
    let mut first = factory.create_builder();
    let mut second = factory.create_builder();

    add_plan_to_builder(&mut first);
    add_plan_to_builder(&mut second);

    // The same plan gives the same edges (the dummy activities have IDs from the
    // generator) and the same nodes. If the builders shared generators, the
    // second would carry on from where the first stopped.
    let (mut first_edges, mut second_edges) = (first.edge_ids(), second.edge_ids());
    let (mut first_nodes, mut second_nodes) = (first.node_ids(), second.node_ids());
    first_edges.sort_unstable();
    second_edges.sort_unstable();
    first_nodes.sort_unstable();
    second_nodes.sort_unstable();
    assert_eq!(second_edges, first_edges);
    assert_eq!(second_nodes, first_nodes);
}

#[test]
fn new_then_a_compiler_compiles_a_plan() {
    let factory = ArrowGraphFactory::<i32, i32, i32>::new();

    let mut compiler = factory.create_compiler();
    drive_compiler(&mut compiler);

    assert_eq!(compiler.finish_time(), PLAN_FINISH);
}

#[test]
fn new_compiler_then_its_builder_is_made_as_the_standard_factory_makes_one() {
    let mut made = ArrowGraphFactory::<i32, i32, i32>::new().create_compiler();
    let mut by_default = Compiler::new();

    drive_compiler(&mut made);
    drive_compiler(&mut by_default);

    // The same plan gives the same edges and nodes, and the same finish. The
    // nodes have IDs from the standard ID generator factory, which steps down from 0.
    let (mut made_edges, mut default_edges) =
        (made.builder().edge_ids(), by_default.builder().edge_ids());
    let (mut made_nodes, mut default_nodes) =
        (made.builder().node_ids(), by_default.builder().node_ids());
    made_edges.sort_unstable();
    default_edges.sort_unstable();
    made_nodes.sort_unstable();
    default_nodes.sort_unstable();
    assert_eq!(default_edges, made_edges);
    assert_eq!(default_nodes, made_nodes);
    assert!(default_nodes.iter().all(|x| *x < 0));
    assert_eq!(by_default.finish_time(), made.finish_time());
}

#[test]
fn create_compiler_then_gives_a_new_compiler_with_a_builder_of_its_own() {
    let factory = ArrowGraphFactory::<i32, i32, i32>::new();
    let mut first = factory.create_compiler();
    let mut second = factory.create_compiler();

    drive_compiler(&mut first);
    assert!(second.add_activity(Act::new(1, 3)));
    second.compile().unwrap();

    // The plan of the second compiler is one activity, and the first compiler's
    // plan is not in it.
    assert_eq!(first.finish_time(), PLAN_FINISH);
    assert_eq!(second.finish_time(), 3);
}

// -- Many threads -------------------------------------------------------------

#[test]
fn many_threads_then_each_compiler_is_its_own_and_gives_the_same_result() {
    let (bundle, counters) = counted_arrow_engines();
    let factory = ArrowGraphFactory::with_engines(bundle);

    let finishes: Vec<i32> = std::thread::scope(|scope| {
        let handles: Vec<_> = (0..8)
            .map(|_| {
                scope.spawn(|| {
                    (0..8)
                        .map(|_| {
                            let mut compiler = factory.create_compiler();
                            drive_compiler(&mut compiler);
                            compiler.finish_time()
                        })
                        .collect::<Vec<i32>>()
                })
            })
            .collect();
        handles
            .into_iter()
            .flat_map(|handle| handle.join().unwrap())
            .collect()
    });

    // The engines of the bundle are shared by all the compilers, and they do not
    // disturb each other.
    assert_eq!(finishes.len(), 64);
    assert!(finishes.iter().all(|x| *x == PLAN_FINISH));
    assert_eq!(counters.idle(), vec!["resource_scheduling_engine"]);
}
