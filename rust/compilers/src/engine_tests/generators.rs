//! Ports of `NextIdGeneratorTests.cs`, `PreviousIdGeneratorTests.cs`,
//! `NextIdGeneratorFactoryTests.cs`, `PreviousIdGeneratorFactoryTests.cs`,
//! `EventGeneratorTests.cs`, `RemovableEventGeneratorTests.cs` and
//! `DummyActivityGeneratorTests.cs`.
//!
//! The C# `RemovableEventGenerator` accepts an injectable inner generator; the
//! Rust decorator wraps a fixed `EventGenerator`, so the "injected inner" and
//! "null inner" tests have no counterpart and are omitted. The C#
//! `StandardIdGeneratorFactory` chooses by the type of the ID, because a `Guid`
//! has no previous value; every `Key` of this crate has one, and so the default
//! factory is the `PreviousIdGeneratorFactory`, and there is nothing to port.
//! The C# tests of a null sequence of IDs have no counterpart either: `create`
//! takes a slice.

use crate::contracts::{IActivityGenerator, IEventGenerator, IIdGenerator, IIdGeneratorFactory};
use crate::{
    DummyActivityGenerator, EventGenerator, NextIdGenerator, NextIdGeneratorFactory,
    PreviousIdGenerator, PreviousIdGeneratorFactory, RemovableEventGenerator,
};

#[test]
fn next_id_generator_with_default_initial_then_increments_from_one() {
    let mut generator = NextIdGenerator::<i32>::default();
    assert_eq!(generator.generate(), 1);
    assert_eq!(generator.generate(), 2);
    assert_eq!(generator.generate(), 3);
}

#[test]
fn next_id_generator_with_initial_value_then_returns_values_after_initial() {
    let mut generator = NextIdGenerator::new(10);
    assert_eq!(generator.generate(), 11);
    assert_eq!(generator.generate(), 12);
}

#[test]
fn previous_id_generator_with_default_initial_then_decrements_from_minus_one() {
    let mut generator = PreviousIdGenerator::<i32>::default();
    assert_eq!(generator.generate(), -1);
    assert_eq!(generator.generate(), -2);
    assert_eq!(generator.generate(), -3);
}

#[test]
fn previous_id_generator_with_initial_value_then_returns_values_before_initial() {
    let mut generator = PreviousIdGenerator::new(10);
    assert_eq!(generator.generate(), 9);
    assert_eq!(generator.generate(), 8);
}

#[test]
fn previous_id_generator_factory_with_no_ids_then_decrements_from_minus_one() {
    let factory = PreviousIdGeneratorFactory::<i32>::default();

    let mut generator = factory.create(&[]);

    assert_eq!(generator.generate(), -1);
    assert_eq!(generator.generate(), -2);
    assert_eq!(generator.generate(), -3);
}

#[test]
fn previous_id_generator_factory_with_initial_value_then_gives_values_before_it() {
    let factory = PreviousIdGeneratorFactory::new(10);

    let mut generator = factory.create(&[]);

    assert_eq!(generator.generate(), 9);
    assert_eq!(generator.generate(), 8);
}

#[test]
fn previous_id_generator_factory_with_ids_below_initial_then_starts_under_the_lowest_id() {
    let factory = PreviousIdGeneratorFactory::<i32>::default();

    let mut generator = factory.create(&[5, -7, 3]);

    assert_eq!(generator.generate(), -8);
    assert_eq!(generator.generate(), -9);
}

#[test]
fn previous_id_generator_factory_with_ids_all_above_initial_then_starts_from_initial() {
    // The IDs are those of activities, which are positive. The new IDs stay
    // negative.
    let factory = PreviousIdGeneratorFactory::<i32>::default();

    let mut generator = factory.create(&[5, 3, 100]);

    assert_eq!(generator.generate(), -1);
    assert_eq!(generator.generate(), -2);
}

#[test]
fn previous_id_generator_factory_with_ids_all_above_a_non_zero_initial_then_starts_from_initial() {
    let factory = PreviousIdGeneratorFactory::new(10);

    let mut generator = factory.create(&[20, 30]);

    assert_eq!(generator.generate(), 9);
}

#[test]
fn previous_id_generator_factory_with_ids_below_a_non_zero_initial_then_starts_under_the_lowest_id()
{
    let factory = PreviousIdGeneratorFactory::new(10);

    let mut generator = factory.create(&[4, 20]);

    assert_eq!(generator.generate(), 3);
}

#[test]
fn previous_id_generator_factory_created_twice_then_the_generators_share_no_state() {
    let factory = PreviousIdGeneratorFactory::<i32>::default();
    let mut first = factory.create(&[]);
    let mut second = factory.create(&[]);

    assert_eq!(first.generate(), -1);
    assert_eq!(first.generate(), -2);

    assert_eq!(second.generate(), -1);
}

#[test]
fn next_id_generator_factory_with_no_ids_then_increments_from_one() {
    let factory = NextIdGeneratorFactory::<i32>::default();

    let mut generator = factory.create(&[]);

    assert_eq!(generator.generate(), 1);
    assert_eq!(generator.generate(), 2);
    assert_eq!(generator.generate(), 3);
}

#[test]
fn next_id_generator_factory_with_initial_value_then_gives_values_after_it() {
    let factory = NextIdGeneratorFactory::new(10);

    let mut generator = factory.create(&[]);

    assert_eq!(generator.generate(), 11);
    assert_eq!(generator.generate(), 12);
}

#[test]
fn next_id_generator_factory_with_ids_above_initial_then_starts_over_the_highest_id() {
    let factory = NextIdGeneratorFactory::<i32>::default();

    let mut generator = factory.create(&[5, 17, 3]);

    assert_eq!(generator.generate(), 18);
    assert_eq!(generator.generate(), 19);
}

#[test]
fn next_id_generator_factory_with_ids_all_below_initial_then_starts_from_initial() {
    let factory = NextIdGeneratorFactory::<i32>::default();

    let mut generator = factory.create(&[-4, -2]);

    assert_eq!(generator.generate(), 1);
}

#[test]
fn next_id_generator_factory_with_ids_all_below_a_non_zero_initial_then_starts_from_initial() {
    let factory = NextIdGeneratorFactory::new(100);

    let mut generator = factory.create(&[20, 30]);

    assert_eq!(generator.generate(), 101);
}

#[test]
fn next_id_generator_factory_with_ids_above_a_non_zero_initial_then_starts_over_the_highest_id() {
    let factory = NextIdGeneratorFactory::new(100);

    let mut generator = factory.create(&[20, 130]);

    assert_eq!(generator.generate(), 131);
}

#[test]
fn next_id_generator_factory_created_twice_then_the_generators_share_no_state() {
    let factory = NextIdGeneratorFactory::<i32>::default();
    let mut first = factory.create(&[]);
    let mut second = factory.create(&[]);

    assert_eq!(first.generate(), 1);
    assert_eq!(first.generate(), 2);

    assert_eq!(second.generate(), 1);
}

#[test]
fn event_generator_then_returns_event_with_id_and_not_removable() {
    let output = IEventGenerator::<i32>::generate(&EventGenerator, 5);
    assert_eq!(output.id(), 5);
    assert_eq!(output.earliest_finish_time, None);
    assert_eq!(output.latest_finish_time, None);
    assert!(!output.can_be_removed());
}

#[test]
fn event_generator_with_finish_times_then_returns_event_with_those_times_and_not_removable() {
    let output = IEventGenerator::<i32>::generate_with_times(&EventGenerator, 5, Some(3), Some(7));
    assert_eq!(output.id(), 5);
    assert_eq!(output.earliest_finish_time, Some(3));
    assert_eq!(output.latest_finish_time, Some(7));
    assert!(!output.can_be_removed());
}

#[test]
fn removable_event_generator_then_returns_removable_event() {
    let generator = RemovableEventGenerator::new();
    let output = IEventGenerator::<i32>::generate(&generator, 5);
    assert_eq!(output.id(), 5);
    assert!(output.can_be_removed());
}

#[test]
fn removable_event_generator_with_finish_times_then_returns_removable_event_with_those_times() {
    let generator = RemovableEventGenerator::new();
    let output = IEventGenerator::<i32>::generate_with_times(&generator, 5, Some(3), Some(7));
    assert_eq!(output.id(), 5);
    assert_eq!(output.earliest_finish_time, Some(3));
    assert_eq!(output.latest_finish_time, Some(7));
    assert!(output.can_be_removed());
}

#[test]
fn dummy_activity_generator_then_returns_removable_zero_duration_activity_with_given_id() {
    let output = IActivityGenerator::<i32, i32, i32>::generate(&DummyActivityGenerator, 7);
    assert_eq!(output.id(), 7);
    assert_eq!(output.duration, 0);
    assert!(output.can_be_removed());
}
