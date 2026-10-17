//! The Monte Carlo schedule simulation - the counterpart of the C#
//! `ScheduleSimulator` and the classes beneath it. See [`ScheduleSimulator`].
//!
//! The simulation gives the same iterations as the .NET library for the same
//! seed. For that, every number that it draws is the same to the last bit: the
//! generator, the logarithm and the exponential are written out here, and
//! `compilers/tests/simulation_baseline_tests.rs` checks them, with whole
//! simulations, against the baseline file of the .NET tests.

mod deterministic_math;
mod iteration_runner;
mod random;
#[cfg(test)]
mod runner_tests;
mod sampler;
mod simulator;
mod tracer;

pub use simulator::{ScheduleSimulator, CHUNK_SIZE};

// The pieces that the baseline test checks one by one are re-exported for it
// under a name that says they are not a public contract.
#[doc(hidden)]
pub mod baseline_support {
    use super::{deterministic_math, random, sampler};
    use zametek_maths_graphs_primitives::{DurationDistribution, DurationEstimate};

    pub fn ln(x: f64) -> f64 {
        deterministic_math::ln(x)
    }

    pub fn exp(x: f64) -> f64 {
        deterministic_math::exp(x)
    }

    pub fn chunk_seed(seed: u64, chunk: usize) -> u64 {
        random::SimulationRandom::chunk_seed(seed, chunk)
    }

    /// A generator for the baseline test.
    pub struct Random(random::SimulationRandom);

    impl Random {
        pub fn new(seed: u64) -> Self {
            Self(random::SimulationRandom::new(seed))
        }

        pub fn next_u64(&mut self) -> u64 {
            self.0.next_u64()
        }

        pub fn next_double(&mut self) -> f64 {
            self.0.next_double()
        }

        pub fn next_gaussian(&mut self) -> f64 {
            self.0.next_gaussian()
        }

        pub fn next_gamma(&mut self, shape: f64) -> f64 {
            self.0.next_gamma(shape)
        }

        pub fn next_beta(&mut self, alpha: f64, beta: f64) -> f64 {
            self.0.next_beta(alpha, beta)
        }

        pub fn sample(
            &mut self,
            distribution: DurationDistribution,
            estimate: DurationEstimate,
            shape: f64,
        ) -> f64 {
            sampler::sample(distribution, &mut self.0, estimate, shape)
        }
    }

    pub fn round_to_whole_units(draw: f64, estimate: DurationEstimate) -> i32 {
        sampler::round_to_whole_units(draw, estimate)
    }
}
