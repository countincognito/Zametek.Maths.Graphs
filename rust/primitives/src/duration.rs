use crate::error::GraphError;
use crate::graph_limits;
use std::fmt;

/// The distribution from which a simulation draws the duration of an activity
/// that has a spread, from its optimistic (O), most likely (ML) and
/// pessimistic (P) durations - the counterpart of the C# `DurationDistribution`.
///
/// Every distribution lies within the range from O to P.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub enum DurationDistribution {
    /// A beta distribution on the range from O to P, with the PERT mean,
    /// (O + 4 ML + P) / 6, and the PERT variance, ((P - O) / 6) squared. This
    /// is the beta of the reference PERT tools, and it matches the expected
    /// duration and the standard deviation that a plan shows for an activity.
    #[default]
    BetaPert = 0,
    /// The modified PERT beta distribution on the range from O to P: its mode
    /// is ML, and its shape value (4 by default) sets how tightly it is drawn
    /// around the mode. Its mean is the PERT mean, but its variance is
    /// (mean - O) (P - mean) / (shape + 3).
    ModifiedBetaPert = 1,
    /// A triangular distribution with its minimum at O, its mode at ML and its
    /// maximum at P.
    Triangular = 2,
    /// A normal distribution with the PERT mean and the PERT standard
    /// deviation, (P - O) / 6, held to the range from O to P.
    Normal = 3,
    /// A uniform distribution on the range from O to P.
    Uniform = 4,
}

impl fmt::Display for DurationDistribution {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let name = match self {
            DurationDistribution::BetaPert => "BetaPert",
            DurationDistribution::ModifiedBetaPert => "ModifiedBetaPert",
            DurationDistribution::Triangular => "Triangular",
            DurationDistribution::Normal => "Normal",
            DurationDistribution::Uniform => "Uniform",
        };
        f.write_str(name)
    }
}

/// The three estimates of an activity's duration, in whole time units: the
/// optimistic duration (O), the most likely duration (ML) and the pessimistic
/// duration (P) - the counterpart of the C# `DurationEstimate`.
///
/// The rule is `0 <= O <= ML <= P`, and each value is within
/// [`graph_limits::MAXIMUM_TIME_VALUE`]. An activity with O = ML = P has no
/// uncertainty, and a simulation gives it that duration in every iteration.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct DurationEstimate {
    optimistic: i32,
    most_likely: i32,
    pessimistic: i32,
}

impl DurationEstimate {
    /// Creates the estimates of a duration. Fails when the values do not
    /// follow the rule, or when one is above the maximum time value.
    pub fn new(optimistic: i32, most_likely: i32, pessimistic: i32) -> Result<Self, GraphError> {
        if optimistic < graph_limits::MINIMUM_TIME_VALUE {
            return Err(GraphError::new(
                "The optimistic duration cannot be negative.",
            ));
        }
        if most_likely < optimistic {
            return Err(GraphError::new(
                "The most likely duration cannot be less than the optimistic duration.",
            ));
        }
        if pessimistic < most_likely {
            return Err(GraphError::new(
                "The pessimistic duration cannot be less than the most likely duration.",
            ));
        }
        if pessimistic > graph_limits::MAXIMUM_TIME_VALUE {
            return Err(GraphError::new(
                "The pessimistic duration cannot be more than the maximum time value.",
            ));
        }
        Ok(Self {
            optimistic,
            most_likely,
            pessimistic,
        })
    }

    /// Estimates that give one duration with no uncertainty.
    pub fn fixed(duration: i32) -> Result<Self, GraphError> {
        Self::new(duration, duration, duration)
    }

    /// The optimistic duration (O).
    pub fn optimistic(&self) -> i32 {
        self.optimistic
    }

    /// The most likely duration (ML).
    pub fn most_likely(&self) -> i32 {
        self.most_likely
    }

    /// The pessimistic duration (P).
    pub fn pessimistic(&self) -> i32 {
        self.pessimistic
    }

    /// Whether the duration is uncertain: O and P are not the same.
    pub fn has_spread(&self) -> bool {
        self.optimistic != self.pessimistic
    }

    /// The PERT expected duration, (O + 4 ML + P) / 6, rounded to the nearest
    /// whole time unit, with halves up. With O = ML = P it is ML.
    pub fn expected_duration(&self) -> i32 {
        (self.optimistic + (4 * self.most_likely) + self.pessimistic + 3) / 6
    }

    /// The PERT standard deviation, (P - O) / 6.
    pub fn standard_deviation(&self) -> f64 {
        (self.pessimistic - self.optimistic) as f64 / 6.0
    }
}

impl fmt::Display for DurationEstimate {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            f,
            "{}/{}/{}",
            self.optimistic, self.most_likely, self.pessimistic
        )
    }
}
