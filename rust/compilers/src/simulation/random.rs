//! The generator of random numbers for the simulation, and the algorithms that
//! turn its bits into the draws that the samplers need. Port of
//! `SimulationRandom.cs`.
//!
//! The generator is xoshiro256** (Blackman and Vigna), with its state set from
//! a seed by splitmix64. Both are fully specified and need only 64-bit integer
//! arithmetic, and the C# library repeats them, so that for one seed the two
//! give one sequence. Each draw uses only the functions of
//! `deterministic_math` and the operations of IEEE 754, and so it gives the
//! same bits in every language. There is no state beyond the four words of the
//! generator: the normal draw throws away its second value on purpose.

use super::deterministic_math;

const SPLIT_MIX_INCREMENT: u64 = 0x9E37_79B9_7F4A_7C15;

pub(crate) struct SimulationRandom {
    s0: u64,
    s1: u64,
    s2: u64,
    s3: u64,
}

/// One step of splitmix64.
pub(crate) fn split_mix(x: &mut u64) -> u64 {
    *x = x.wrapping_add(SPLIT_MIX_INCREMENT);
    let mut z = *x;
    z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
    z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
    z ^ (z >> 31)
}

impl SimulationRandom {
    /// A generator in a given state, for the tests that check the algorithm
    /// against its published values.
    #[cfg(test)]
    pub(crate) fn from_state(s0: u64, s1: u64, s2: u64, s3: u64) -> Self {
        Self { s0, s1, s2, s3 }
    }

    /// The generator of one chunk of iterations. See [`Self::chunk_seed`].
    pub(crate) fn new(seed: u64) -> Self {
        let mut x = seed;
        let s0 = split_mix(&mut x);
        let s1 = split_mix(&mut x);
        let s2 = split_mix(&mut x);
        let s3 = split_mix(&mut x);
        Self { s0, s1, s2, s3 }
    }

    /// The seed of one chunk of iterations (the chunks count from 0): the
    /// output of splitmix64 that follows the main seed, `chunk` steps further
    /// on.
    pub(crate) fn chunk_seed(seed: u64, chunk: usize) -> u64 {
        let mut x = seed.wrapping_add((chunk as u64).wrapping_mul(SPLIT_MIX_INCREMENT));
        split_mix(&mut x)
    }

    pub(crate) fn next_u64(&mut self) -> u64 {
        let result = self.s1.wrapping_mul(5).rotate_left(7).wrapping_mul(9);
        let t = self.s1 << 17;
        self.s2 ^= self.s0;
        self.s3 ^= self.s1;
        self.s1 ^= self.s2;
        self.s0 ^= self.s3;
        self.s2 ^= t;
        self.s3 = self.s3.rotate_left(45);
        result
    }

    /// In [0, 1), with 53 random bits.
    pub(crate) fn next_double(&mut self) -> f64 {
        (self.next_u64() >> 11) as f64 * (1.0 / 9007199254740992.0)
    }

    /// In (0, 1], for the logarithms.
    pub(crate) fn next_double_open(&mut self) -> f64 {
        1.0 - self.next_double()
    }

    /// The polar method of Marsaglia. The second value of each pair is thrown
    /// away.
    pub(crate) fn next_gaussian(&mut self) -> f64 {
        loop {
            let u = (2.0 * self.next_double()) - 1.0;
            let v = (2.0 * self.next_double()) - 1.0;
            let s = (u * u) + (v * v);
            if s < 1.0 && s != 0.0 {
                return u * (-2.0 * deterministic_math::ln(s) / s).sqrt();
            }
        }
    }

    /// Marsaglia and Tsang. A shape below 1 is raised by one, and the draw is
    /// then corrected by a power of a uniform number.
    pub(crate) fn next_gamma(&mut self, shape: f64) -> f64 {
        if shape < 1.0 {
            let boosted = self.next_gamma(shape + 1.0);
            let u = self.next_double_open();
            return boosted * deterministic_math::exp(deterministic_math::ln(u) / shape);
        }

        let d = shape - (1.0 / 3.0);
        let c = 1.0 / (9.0 * d).sqrt();
        loop {
            let x = self.next_gaussian();
            let mut v = 1.0 + (c * x);
            if v <= 0.0 {
                continue;
            }
            v = v * v * v;
            let u = self.next_double_open();
            if u < 1.0 - (0.0331 * x * x * x * x) {
                return d * v;
            }
            if deterministic_math::ln(u)
                < (0.5 * x * x) + (d * (1.0 - v + deterministic_math::ln(v)))
            {
                return d * v;
            }
        }
    }

    /// A beta draw in (0, 1), from two gamma draws.
    pub(crate) fn next_beta(&mut self, alpha: f64, beta: f64) -> f64 {
        let a = self.next_gamma(alpha);
        let b = self.next_gamma(beta);
        a / (a + b)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn split_mix_given_seed_zero_then_gives_the_published_values() {
        let mut x = 0u64;
        assert_eq!(split_mix(&mut x), 0xE220A8397B1DCDAF);
        assert_eq!(split_mix(&mut x), 0x6E789E6AA1B965F4);
        assert_eq!(split_mix(&mut x), 0x06C45D188009454F);
        assert_eq!(split_mix(&mut x), 0xF88BB8A8724C81EC);
    }

    #[test]
    fn next_u64_given_the_state_one_two_three_four_then_gives_the_published_values() {
        let mut random = SimulationRandom::from_state(1, 2, 3, 4);
        assert_eq!(random.next_u64(), 11520);
        assert_eq!(random.next_u64(), 0);
        assert_eq!(random.next_u64(), 1509978240);
        assert_eq!(random.next_u64(), 1215971899390074240);
    }

    #[test]
    fn next_gaussian_given_many_draws_then_has_mean_zero_and_variance_one() {
        let mut random = SimulationRandom::new(3);
        let count = 200_000;
        let (mut sum, mut sum_of_squares) = (0.0, 0.0);
        for _ in 0..count {
            let value = random.next_gaussian();
            sum += value;
            sum_of_squares += value * value;
        }
        let mean = sum / f64::from(count);
        let variance = (sum_of_squares / f64::from(count)) - (mean * mean);
        assert!(mean.abs() < 0.01);
        assert!((variance - 1.0).abs() < 0.02);
    }

    #[test]
    fn next_gamma_given_a_shape_then_has_mean_equal_to_the_shape() {
        for shape in [0.5, 0.667, 1.0, 2.5, 9.0] {
            let mut random = SimulationRandom::new(4);
            let count = 200_000;
            let sum: f64 = (0..count).map(|_| random.next_gamma(shape)).sum();
            assert!(
                (sum / f64::from(count) - shape).abs() < shape * 0.02,
                "{shape}"
            );
        }
    }

    #[test]
    fn chunk_seed_given_many_chunks_then_gives_a_different_seed_to_each() {
        let mut seeds: Vec<u64> = (0..10_000)
            .map(|x| SimulationRandom::chunk_seed(42, x))
            .collect();
        seeds.sort_unstable();
        seeds.dedup();
        assert_eq!(seeds.len(), 10_000);
    }
}
