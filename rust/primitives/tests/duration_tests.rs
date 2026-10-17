//! Ports of `DurationEstimateTests.cs` and of the parts of `SimulationResultTests.cs`
//! that have a counterpart here.

use zametek_maths_graphs_primitives::{DurationDistribution, DurationEstimate, SimulationResult};

#[test]
fn new_given_values_that_break_the_rule_then_fails() {
    assert!(DurationEstimate::new(-1, 0, 0).is_err());
    assert!(DurationEstimate::new(3, 2, 5).is_err());
    assert!(DurationEstimate::new(1, 6, 5).is_err());
    assert!(DurationEstimate::new(0, 0, 100_001).is_err());
}

#[test]
fn new_given_the_limits_of_the_rule_then_accepts() {
    assert!(!DurationEstimate::new(0, 0, 0).unwrap().has_spread());
    assert!(DurationEstimate::new(0, 100_000, 100_000)
        .unwrap()
        .has_spread());
    assert_eq!(
        DurationEstimate::new(100_000, 100_000, 100_000)
            .unwrap()
            .expected_duration(),
        100_000
    );
}

#[test]
fn expected_duration_given_estimates_then_is_the_pert_mean_to_the_nearest_unit_with_halves_up() {
    let cases = [
        (4, 10, 10, 9, 1.0),
        (4, 10, 22, 11, 3.0),
        (2, 4, 6, 4, 0.6666666666666666),
        (0, 0, 3, 1, 0.5),
        (0, 0, 9, 2, 1.5),
        (0, 0, 2, 0, 0.3333333333333333),
    ];
    for (o, ml, p, expected, deviation) in cases {
        let estimate = DurationEstimate::new(o, ml, p).unwrap();
        assert_eq!(estimate.expected_duration(), expected, "{estimate}");
        assert!(
            (estimate.standard_deviation() - deviation).abs() < 1e-12,
            "{estimate}"
        );
    }
}

#[test]
fn fixed_given_a_duration_then_has_no_spread() {
    let estimate = DurationEstimate::fixed(7).unwrap();
    assert!(!estimate.has_spread());
    assert_eq!(estimate.expected_duration(), 7);
    assert_eq!(estimate.to_string(), "7/7/7");
}

#[test]
fn display_given_a_distribution_then_is_its_name() {
    assert_eq!(DurationDistribution::BetaPert.to_string(), "BetaPert");
    assert_eq!(
        DurationDistribution::ModifiedBetaPert.to_string(),
        "ModifiedBetaPert"
    );
}

#[test]
fn result_given_parts_that_do_not_match_then_fails() {
    // Two activities, two iterations: each iteration takes one word of bits.
    assert!(SimulationResult::new(
        vec![1, 2],
        vec![5, 6],
        Some(vec![0u64; 3]),
        vec![],
        None,
        vec![],
        vec![]
    )
    .is_err());
    assert!(SimulationResult::new(
        vec![1, 2],
        vec![5, 6],
        None,
        vec![],
        Some(vec![0, 0]),
        vec![],
        vec![true, true]
    )
    .is_err());
}

#[test]
fn result_given_the_bits_of_activities_then_reads_them_back() {
    // 70 activities take two words for each iteration.
    let ids: Vec<i32> = (1..=70).collect();
    let mut bits = vec![0u64; 6];
    bits[0] = 0b101;
    bits[2] = 1 << 5;
    bits[5] = 1 << 5;
    let result = SimulationResult::new(
        ids,
        vec![10, -1, 12],
        Some(bits),
        vec![],
        None,
        vec![],
        vec![],
    )
    .unwrap();

    assert_eq!(result.failed_iteration_count(), 1);
    assert_eq!(result.is_critical(0, 0), Some(true));
    assert_eq!(result.is_critical(0, 1), Some(false));
    assert_eq!(result.is_critical(0, 2), Some(true));
    assert_eq!(result.is_critical(1, 5), Some(true));
    assert_eq!(result.is_critical(2, 69), Some(true));
    assert_eq!(result.is_critical(2, 5), Some(false));
    assert_eq!(result.is_critical(3, 0), None);
    assert_eq!(result.critical_path_indexes(0), None);
}
