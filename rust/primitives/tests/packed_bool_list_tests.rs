//! Ports of `PackedBoolListTests.cs`.
//!
//! `PackedBoolList` stores one bit per flag instead of one byte, so these tests
//! concentrate on the boundaries where the packing arithmetic could go wrong:
//! lengths either side of a 64-bit word, flags in the highest bit of a word, and
//! round-tripping arbitrary patterns.
//!
//! Two C# tests have no counterpart here: the null-source guard (the Rust
//! constructors take an iterator, which cannot be null) and the negative-index
//! case of the out-of-range test (`get` takes a `usize`). The out-of-range test
//! itself is kept, but as `get` returning `None` rather than as a thrown
//! exception.

use zametek_maths_graphs_primitives::{
    PackedBoolList, Resource, ResourceSchedule, ScheduledActivity,
};

/// A small deterministic generator, so the "arbitrary pattern" cases are
/// reproducible without pulling in a random-number dependency. This is the same
/// xorshift the compilers' test corpus uses.
struct Xorshift(u64);

impl Xorshift {
    fn new(seed: u64) -> Self {
        Self(seed | 1)
    }

    fn next_bool(&mut self) -> bool {
        self.0 ^= self.0 << 13;
        self.0 ^= self.0 >> 7;
        self.0 ^= self.0 << 17;
        self.0 & 1 == 0
    }
}

#[test]
fn packed_bool_list_given_empty_source_then_is_empty() {
    let output: PackedBoolList = Vec::new().into();

    assert_eq!(output.len(), 0);
    assert!(output.is_empty());
    assert_eq!(output.iter().count(), 0);
}

#[test]
fn packed_bool_list_given_alternating_flags_then_round_trips_exactly() {
    for length in [1usize, 63, 64, 65, 127, 128, 129, 1000] {
        let source: Vec<bool> = (0..length).map(|x| x % 2 == 0).collect();

        let output: PackedBoolList = source.iter().copied().collect();

        assert_eq!(output.len(), length, "length {length}");
        assert_eq!(output, source, "length {length}");
    }
}

#[test]
fn packed_bool_list_given_random_flags_then_lookup_matches_source() {
    for length in [63usize, 64, 65, 200] {
        let mut rng = Xorshift::new(length as u64);
        let source: Vec<bool> = (0..length).map(|_| rng.next_bool()).collect();

        let output: PackedBoolList = source.clone().into();

        for (index, expected) in source.iter().enumerate() {
            assert_eq!(
                output.get(index),
                Some(*expected),
                "length {length}, index {index}"
            );
        }
    }
}

#[test]
fn packed_bool_list_given_flag_in_highest_bit_of_word_then_reads_back_true() {
    // Bit 63 is the top bit of the first word, and bit 64 the bottom bit of the
    // second - the exact boundary the shift and mask arithmetic has to get right.
    let mut source = vec![false; 130];
    source[63] = true;
    source[64] = true;
    source[129] = true;

    let output: PackedBoolList = source.into();

    assert_eq!(output.get(63), Some(true));
    assert_eq!(output.get(64), Some(true));
    assert_eq!(output.get(129), Some(true));
    assert_eq!(output.iter().filter(|x| *x).count(), 3);
}

#[test]
fn packed_bool_list_given_all_flags_set_then_all_read_back_true() {
    let output: PackedBoolList = std::iter::repeat_n(true, 100).collect();

    assert_eq!(output.len(), 100);
    assert!(output.iter().all(|x| x));
}

#[test]
fn packed_bool_list_given_lazy_source_of_unknown_length_then_grows_correctly() {
    // A filtered iterator reports a lower bound of zero, so this exercises the
    // path where the word vector has to grow as flags arrive rather than being
    // sized up front.
    let lazy_source = || (0..500).filter(|x| x % 3 != 0).map(|x| x % 2 == 0);
    let expected: Vec<bool> = lazy_source().collect();

    let output: PackedBoolList = lazy_source().collect();

    assert_eq!(output.len(), expected.len());
    assert_eq!(output, expected);
}

#[test]
fn packed_bool_list_given_index_out_of_range_then_returns_none() {
    let output: PackedBoolList = std::iter::repeat_n(true, 10).collect();

    assert_eq!(output.get(9), Some(true));
    assert_eq!(output.get(10), None);
    assert_eq!(output.get(11), None);
}

#[test]
fn packed_bool_list_given_equal_contents_built_differently_then_compares_equal() {
    // One is sized up front from a known length, the other grows from a lazy
    // source; the two must not differ in any unused trailing bits.
    let sized: PackedBoolList = vec![true, false, true].into();
    let grown: PackedBoolList = [true, false, true].into_iter().filter(|_| true).collect();

    assert_eq!(sized, grown);
    assert_eq!(sized, vec![true, false, true]);
    assert_ne!(sized, vec![true, false, true, false]);
}

#[test]
fn packed_bool_list_given_debug_format_then_renders_as_a_list_of_flags() {
    // The streams are compared against `vec![...]` literals throughout the test
    // suites, so a failing assertion has to print the flags, not the words.
    let output: PackedBoolList = vec![true, false, true].into();

    assert_eq!(format!("{output:?}"), "[true, false, true]");
}

#[test]
fn resource_schedule_given_allocation_streams_then_exposes_them_unchanged() {
    let resource_allocation = vec![true, false, true, true];
    let cost_allocation = vec![false, false, true, true];
    let billing_allocation = vec![true, true, false, false];
    let effort_allocation = vec![false, true, false, true];
    let activity_allocation = vec![true, true, true, false];

    let schedule: ResourceSchedule<i32, i32, i32> = ResourceSchedule {
        resource: None,
        scheduled_activities: Vec::new(),
        start_time: 0,
        finish_time: 4,
        resource_allocation: resource_allocation.clone().into(),
        cost_allocation: cost_allocation.clone().into(),
        billing_allocation: billing_allocation.clone().into(),
        effort_allocation: effort_allocation.clone().into(),
        activity_allocation: activity_allocation.clone().into(),
    };

    // Packing is a storage detail; the streams must read back exactly as supplied.
    assert_eq!(schedule.resource_allocation, resource_allocation);
    assert_eq!(schedule.cost_allocation, cost_allocation);
    assert_eq!(schedule.billing_allocation, billing_allocation);
    assert_eq!(schedule.effort_allocation, effort_allocation);
    assert_eq!(schedule.activity_allocation, activity_allocation);
}

#[test]
fn resource_schedule_given_clone_then_allocation_streams_survive_the_round_trip() {
    let mut rng = Xorshift::new(7);
    let allocation: Vec<bool> = (0..200).map(|_| rng.next_bool()).collect();

    let schedule: ResourceSchedule<i32, i32, i32> = ResourceSchedule {
        resource: None::<Resource<i32, i32>>,
        scheduled_activities: Vec::<ScheduledActivity<i32>>::new(),
        start_time: 0,
        finish_time: 200,
        resource_allocation: allocation.clone().into(),
        cost_allocation: allocation.clone().into(),
        billing_allocation: allocation.clone().into(),
        effort_allocation: allocation.clone().into(),
        activity_allocation: allocation.clone().into(),
    };

    let clone = schedule.clone();

    assert_eq!(clone.resource_allocation, allocation);
    assert_eq!(clone.activity_allocation, allocation);
}
