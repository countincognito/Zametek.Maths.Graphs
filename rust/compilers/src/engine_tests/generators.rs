//! Ports of `NextIdGeneratorTests.cs`, `PreviousIdGeneratorTests.cs`,
//! `EventGeneratorTests.cs`, `RemovableEventGeneratorTests.cs` and
//! `DummyActivityGeneratorTests.cs`.
//!
//! The C# `RemovableEventGenerator` accepts an injectable inner generator; the
//! Rust decorator wraps a fixed `EventGenerator`, so the "injected inner" and
//! "null inner" tests have no counterpart and are omitted.

use crate::contracts::{IActivityGenerator, IEventGenerator, IIdGenerator};
use crate::{
    DummyActivityGenerator, EventGenerator, NextIdGenerator, PreviousIdGenerator,
    RemovableEventGenerator,
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
