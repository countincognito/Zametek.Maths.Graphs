//! Port of `GraphBuilderCloneIdGeneratorTests.cs`. A clone of a builder asks the
//! ID generator factory of its original for generators of its own, for the IDs
//! that the graph copy holds. Thus the generators of a clone are of the same
//! kind as the generators of the original, also when the kind is not one of the
//! library, and the clone gives the IDs that the original would give.

use indexmap::IndexSet;
use zametek_maths_graphs_compilers::contracts::{IIdGenerator, IIdGeneratorFactory};
use zametek_maths_graphs_compilers::primitives::DependentActivity;
use zametek_maths_graphs_compilers::{
    ArrowGraphBuilder, NextIdGeneratorFactory, PreviousIdGeneratorFactory, VertexGraphBuilder,
};

type Act = DependentActivity<i32, i32, i32>;
type VertexBuilder = VertexGraphBuilder<i32, i32, i32>;
type ArrowBuilder = ArrowGraphBuilder<i32, i32, i32>;

// -- Helpers ------------------------------------------------------------------

// A factory of a kind that the library does not have: its generators give the
// multiples of a step, over the highest ID that the graph holds.
struct StepIdGeneratorFactory {
    step: i32,
}

struct StepIdGenerator {
    step: i32,
    value: i32,
}

impl IIdGenerator<i32> for StepIdGenerator {
    fn generate(&mut self) -> i32 {
        self.value += self.step;
        self.value
    }
}

impl IIdGeneratorFactory<i32> for StepIdGeneratorFactory {
    fn create(&self, used_ids: &[i32]) -> Box<dyn IIdGenerator<i32>> {
        let highest = used_ids.iter().copied().max().unwrap_or(0).max(0);
        Box::new(StepIdGenerator {
            step: self.step,
            value: highest / self.step * self.step,
        })
    }
}

fn vertex_plan(factory: impl IIdGeneratorFactory<i32> + 'static) -> VertexBuilder {
    let mut builder = VertexBuilder::new(factory);
    assert!(builder.add_activity(Act::new(1, 3)));
    assert!(builder.add_activity_with_dependencies(Act::new(2, 5), IndexSet::from([1])));
    builder
}

// 3 needs 1 and 2, and 4 needs only 2, which needs a dummy activity.
fn arrow_plan(
    edge_factory: impl IIdGeneratorFactory<i32> + 'static,
    node_factory: impl IIdGeneratorFactory<i32> + 'static,
) -> ArrowBuilder {
    let mut builder = ArrowBuilder::new(edge_factory, node_factory);
    assert!(builder.add_activity(Act::new(1, 3)));
    assert!(builder.add_activity(Act::new(2, 5)));
    assert!(builder.add_activity_with_dependencies(Act::new(3, 2), IndexSet::from([1, 2])));
    assert!(builder.add_activity_with_dependencies(Act::new(4, 4), IndexSet::from([2])));
    builder
}

fn add_to_vertex(builder: &mut VertexBuilder) {
    assert!(builder.add_activity_with_dependencies(Act::new(3, 2), IndexSet::from([2])));
}

// The ID of the activity that is added to an arrow plan: no generator of these
// tests gives it.
const ARROW_ACTIVITY_ID: i32 = 1_003;

fn add_to_arrow(builder: &mut ArrowBuilder) {
    assert!(builder
        .add_activity_with_dependencies(Act::new(ARROW_ACTIVITY_ID, 1), IndexSet::from([3, 4])));
}

fn sorted(mut ids: Vec<i32>) -> Vec<i32> {
    ids.sort_unstable();
    ids
}

// The IDs that the second list has and the first does not, and that are not
// the ID of the activity that was added.
fn made_by_the_clone(first: &[i32], second: &[i32]) -> Vec<i32> {
    second
        .iter()
        .copied()
        .filter(|x| !first.contains(x) && *x != ARROW_ACTIVITY_ID)
        .collect()
}

// -- Vertex -------------------------------------------------------------------

#[test]
fn vertex_clone_and_a_factory_that_steps_up_then_the_clone_steps_up() {
    let mut original = vertex_plan(NextIdGeneratorFactory::new(1_000_000));
    assert_eq!(original.event_ids(), vec![1_000_001]);

    let mut clone = original.clone_builder().unwrap();
    add_to_vertex(&mut clone);

    // The new event is over the old one, and not under it, as a generator that
    // steps down would make it.
    assert_eq!(sorted(clone.event_ids()), vec![1_000_001, 1_000_002]);
}

#[test]
fn vertex_clone_and_the_default_factory_then_the_clone_steps_down_by_one() {
    let mut original = vertex_plan(PreviousIdGeneratorFactory::default());
    assert_eq!(original.event_ids(), vec![-1]);

    let mut clone = original.clone_builder().unwrap();
    add_to_vertex(&mut clone);

    // The next event is -2: there is no gap between the events of the clone.
    assert_eq!(sorted(clone.event_ids()), vec![-2, -1]);
}

#[test]
fn vertex_clone_of_an_empty_builder_then_the_clone_gives_the_ids_of_a_new_builder() {
    let mut original = VertexBuilder::new(PreviousIdGeneratorFactory::default());

    let mut clone = original.clone_builder().unwrap();
    assert!(clone.add_activity(Act::new(1, 3)));
    assert!(clone.add_activity_with_dependencies(Act::new(2, 5), IndexSet::from([1])));

    assert_eq!(clone.event_ids(), vec![-1]);
}

fn vertex_clone_gives_the_ids_that_the_original_gives(
    factory: impl IIdGeneratorFactory<i32> + 'static,
) {
    let mut original = vertex_plan(factory);
    let mut clone = original.clone_builder().unwrap();

    add_to_vertex(&mut original);
    add_to_vertex(&mut clone);

    assert_eq!(sorted(clone.event_ids()), sorted(original.event_ids()));
    assert_eq!(original.event_ids().len(), 2);
}

#[test]
fn vertex_clone_with_a_next_factory_then_the_clone_gives_the_ids_that_the_original_gives() {
    vertex_clone_gives_the_ids_that_the_original_gives(NextIdGeneratorFactory::new(1_000_000));
}

#[test]
fn vertex_clone_with_a_previous_factory_then_the_clone_gives_the_ids_that_the_original_gives() {
    vertex_clone_gives_the_ids_that_the_original_gives(PreviousIdGeneratorFactory::new(-1_000));
}

#[test]
fn vertex_clone_with_the_default_factory_then_the_clone_gives_the_ids_that_the_original_gives() {
    vertex_clone_gives_the_ids_that_the_original_gives(PreviousIdGeneratorFactory::default());
}

#[test]
fn vertex_clone_with_a_step_factory_then_the_clone_gives_the_ids_that_the_original_gives() {
    vertex_clone_gives_the_ids_that_the_original_gives(StepIdGeneratorFactory { step: 7 });
}

#[test]
fn vertex_clone_and_a_factory_of_another_kind_then_the_clone_has_a_generator_of_that_kind() {
    let mut original = vertex_plan(StepIdGeneratorFactory { step: 7 });
    assert_eq!(original.event_ids(), vec![7]);

    let mut clone = original.clone_builder().unwrap();
    add_to_vertex(&mut clone);

    assert_eq!(sorted(clone.event_ids()), vec![7, 14]);
}

#[test]
fn vertex_clone_then_the_generator_of_the_clone_is_not_the_generator_of_the_original() {
    let mut original = vertex_plan(PreviousIdGeneratorFactory::default());
    let mut clone = original.clone_builder().unwrap();

    add_to_vertex(&mut clone);
    assert!(clone.add_activity_with_dependencies(Act::new(4, 1), IndexSet::from([3])));
    add_to_vertex(&mut original);

    // The clone made two events, and that did not move the generator of the
    // original.
    assert_eq!(sorted(clone.event_ids()), vec![-3, -2, -1]);
    assert_eq!(sorted(original.event_ids()), vec![-2, -1]);
}

// -- Arrow --------------------------------------------------------------------

#[test]
fn arrow_clone_and_factories_that_step_up_then_what_the_clone_makes_steps_up() {
    let mut original = arrow_plan(
        NextIdGeneratorFactory::new(1_000_000),
        NextIdGeneratorFactory::new(2_000_000),
    );
    let highest_node = *original.node_ids().iter().max().unwrap();
    let highest_edge = *original.edge_ids().iter().max().unwrap();

    let mut clone = original.clone_builder().unwrap();
    add_to_arrow(&mut clone);

    let nodes = made_by_the_clone(&original.node_ids(), &clone.node_ids());
    assert!(!nodes.is_empty());
    assert!(nodes.iter().all(|x| *x > highest_node));
    let dummies = made_by_the_clone(&original.edge_ids(), &clone.edge_ids());
    assert!(!dummies.is_empty());
    assert!(dummies.iter().all(|x| *x > highest_edge));
}

#[test]
fn arrow_clone_and_the_default_factories_then_what_the_clone_makes_steps_down() {
    let mut original = arrow_plan(
        PreviousIdGeneratorFactory::default(),
        PreviousIdGeneratorFactory::default(),
    );
    let lowest_node = *original.node_ids().iter().min().unwrap();
    let lowest_edge = *original.edge_ids().iter().min().unwrap();

    let mut clone = original.clone_builder().unwrap();
    add_to_arrow(&mut clone);

    let nodes = made_by_the_clone(&original.node_ids(), &clone.node_ids());
    assert!(!nodes.is_empty());
    assert!(nodes.iter().all(|x| *x < lowest_node));
    let dummies = made_by_the_clone(&original.edge_ids(), &clone.edge_ids());
    assert!(!dummies.is_empty());
    assert!(dummies.iter().all(|x| *x < lowest_edge));
}

fn arrow_clone_gives_the_ids_that_the_original_gives(
    edge_factory: impl IIdGeneratorFactory<i32> + 'static,
    node_factory: impl IIdGeneratorFactory<i32> + 'static,
) {
    let mut original = arrow_plan(edge_factory, node_factory);
    let mut clone = original.clone_builder().unwrap();

    add_to_arrow(&mut original);
    add_to_arrow(&mut clone);

    assert_eq!(sorted(clone.node_ids()), sorted(original.node_ids()));
    assert_eq!(sorted(clone.edge_ids()), sorted(original.edge_ids()));
}

#[test]
fn arrow_clone_with_next_factories_then_the_clone_gives_the_ids_that_the_original_gives() {
    arrow_clone_gives_the_ids_that_the_original_gives(
        NextIdGeneratorFactory::new(1_000_000),
        NextIdGeneratorFactory::new(1_000_000),
    );
}

#[test]
fn arrow_clone_with_previous_factories_then_the_clone_gives_the_ids_that_the_original_gives() {
    arrow_clone_gives_the_ids_that_the_original_gives(
        PreviousIdGeneratorFactory::new(-1_000),
        PreviousIdGeneratorFactory::new(-1_000),
    );
}

#[test]
fn arrow_clone_with_the_default_factories_then_the_clone_gives_the_ids_that_the_original_gives() {
    arrow_clone_gives_the_ids_that_the_original_gives(
        PreviousIdGeneratorFactory::default(),
        PreviousIdGeneratorFactory::default(),
    );
}

#[test]
fn arrow_clone_with_step_factories_then_the_clone_gives_the_ids_that_the_original_gives() {
    arrow_clone_gives_the_ids_that_the_original_gives(
        StepIdGeneratorFactory { step: 7 },
        StepIdGeneratorFactory { step: 11 },
    );
}

#[test]
fn arrow_clone_and_factories_of_another_kind_then_the_clone_has_generators_of_that_kind() {
    let mut original = arrow_plan(
        StepIdGeneratorFactory { step: 5 },
        StepIdGeneratorFactory { step: 11 },
    );
    let highest_node = *original.node_ids().iter().max().unwrap();
    let highest_edge = *original.edge_ids().iter().max().unwrap();

    let mut clone = original.clone_builder().unwrap();
    add_to_arrow(&mut clone);

    // Every node of the original is a multiple of 11, and so is every node that
    // the clone made.
    assert!(original.node_ids().iter().all(|x| x % 11 == 0));
    let nodes = made_by_the_clone(&original.node_ids(), &clone.node_ids());
    assert!(!nodes.is_empty());
    assert!(nodes.iter().all(|x| *x > highest_node && x % 11 == 0));
    // The dummy activities are the edges that are not activities, and the same
    // holds for them with 5.
    let dummies = made_by_the_clone(&original.edge_ids(), &clone.edge_ids());
    assert!(!dummies.is_empty());
    assert!(dummies.iter().all(|x| *x > highest_edge && x % 5 == 0));
}
