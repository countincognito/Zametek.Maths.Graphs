//! The draws of a duration, from the three estimates of an activity, for each
//! distribution. Port of `DurationSampler.cs`; the order of the operations is
//! the order of the C# code.

use super::random::SimulationRandom;
use zametek_maths_graphs_primitives::{DurationDistribution, DurationEstimate};

/// One draw. An activity with no spread (O = P) takes no number from the
/// generator.
pub(crate) fn sample(
    distribution: DurationDistribution,
    random: &mut SimulationRandom,
    estimate: DurationEstimate,
    modified_beta_shape: f64,
) -> f64 {
    let optimistic = f64::from(estimate.optimistic());
    let most_likely = f64::from(estimate.most_likely());
    let pessimistic = f64::from(estimate.pessimistic());

    if !estimate.has_spread() {
        return optimistic;
    }

    match distribution {
        DurationDistribution::BetaPert => beta_pert(random, optimistic, most_likely, pessimistic),
        DurationDistribution::ModifiedBetaPert => modified_beta_pert(
            random,
            optimistic,
            most_likely,
            pessimistic,
            modified_beta_shape,
        ),
        DurationDistribution::Triangular => {
            triangular(random, optimistic, most_likely, pessimistic)
        }
        DurationDistribution::Normal => normal(random, optimistic, most_likely, pessimistic),
        DurationDistribution::Uniform => {
            optimistic + (random.next_double() * (pessimistic - optimistic))
        }
    }
}

/// A draw in whole time units: the nearest whole number, with halves up, in the
/// range from O to P.
pub(crate) fn round_to_whole_units(draw: f64, estimate: DurationEstimate) -> i32 {
    let rounded = draw.round();
    if rounded < f64::from(estimate.optimistic()) {
        return estimate.optimistic();
    }
    if rounded > f64::from(estimate.pessimistic()) {
        return estimate.pessimistic();
    }
    rounded as i32
}

fn beta_pert(
    random: &mut SimulationRandom,
    optimistic: f64,
    most_likely: f64,
    pessimistic: f64,
) -> f64 {
    let range = pessimistic - optimistic;
    let mean = (optimistic + (4.0 * most_likely) + pessimistic) / 6.0;
    let standard_deviation = range / 6.0;
    let from_optimistic = mean - optimistic;
    let to_pessimistic = pessimistic - mean;

    let alpha = from_optimistic / range
        * ((from_optimistic * to_pessimistic / (standard_deviation * standard_deviation)) - 1.0);
    let beta = alpha * to_pessimistic / from_optimistic;
    optimistic + (random.next_beta(alpha, beta) * range)
}

fn modified_beta_pert(
    random: &mut SimulationRandom,
    optimistic: f64,
    most_likely: f64,
    pessimistic: f64,
    shape: f64,
) -> f64 {
    let range = pessimistic - optimistic;
    let alpha = 1.0 + (shape * (most_likely - optimistic) / range);
    let beta = 1.0 + (shape * (pessimistic - most_likely) / range);
    optimistic + (random.next_beta(alpha, beta) * range)
}

fn triangular(random: &mut SimulationRandom, minimum: f64, mode: f64, maximum: f64) -> f64 {
    let range = maximum - minimum;
    let u = random.next_double();
    let fraction = (mode - minimum) / range;
    if u < fraction {
        minimum + (u * range * (mode - minimum)).sqrt()
    } else {
        maximum - ((1.0 - u) * range * (maximum - mode)).sqrt()
    }
}

fn normal(
    random: &mut SimulationRandom,
    optimistic: f64,
    most_likely: f64,
    pessimistic: f64,
) -> f64 {
    let mean = (optimistic + (4.0 * most_likely) + pessimistic) / 6.0;
    let standard_deviation = (pessimistic - optimistic) / 6.0;
    let draw = mean + (standard_deviation * random.next_gaussian());
    pessimistic.min(optimistic.max(draw))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn round_to_whole_units_given_a_half_then_goes_up() {
        let estimate = DurationEstimate::new(0, 3, 10).unwrap();
        assert_eq!(round_to_whole_units(2.4999, estimate), 2);
        assert_eq!(round_to_whole_units(2.5, estimate), 3);
        assert_eq!(round_to_whole_units(0.5, estimate), 1);
    }

    #[test]
    fn sample_given_an_activity_with_no_spread_then_takes_nothing_from_the_generator() {
        let mut random = SimulationRandom::new(5);
        let mut untouched = SimulationRandom::new(5);
        let value = sample(
            DurationDistribution::BetaPert,
            &mut random,
            DurationEstimate::fixed(7).unwrap(),
            4.0,
        );
        assert_eq!(value, 7.0);
        assert_eq!(random.next_u64(), untouched.next_u64());
    }

    #[test]
    fn beta_pert_given_the_reference_activity_b_then_has_the_pert_mean_and_deviation() {
        let estimate = DurationEstimate::new(4, 5, 12).unwrap();
        let mut random = SimulationRandom::new(11);
        let count = 200_000;
        let draws: Vec<f64> = (0..count)
            .map(|_| sample(DurationDistribution::BetaPert, &mut random, estimate, 4.0))
            .collect();
        let mean = draws.iter().sum::<f64>() / f64::from(count);
        let variance =
            draws.iter().map(|x| (x - mean) * (x - mean)).sum::<f64>() / f64::from(count);
        assert!((mean - 6.0).abs() < 0.03);
        assert!((variance.sqrt() - 8.0 / 6.0).abs() < 0.02);
    }
}
