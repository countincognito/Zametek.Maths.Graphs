using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// The three estimates of an activity's duration, in whole time units: the optimistic
    /// duration (O), the most likely duration (ML) and the pessimistic duration (P).
    /// </summary>
    /// <remarks>
    /// The rule is 0 &lt;= O &lt;= ML &lt;= P, and each value is within
    /// <see cref="GraphLimits.MaximumTimeValue"/>. An activity with O = ML = P has no
    /// uncertainty, and a simulation gives it that duration in every iteration.
    /// </remarks>
    public readonly struct DurationEstimate
        : IEquatable<DurationEstimate>
    {
        /// <summary>
        /// Creates the estimates of a duration.
        /// </summary>
        /// <param name="optimistic">The optimistic duration (O).</param>
        /// <param name="mostLikely">The most likely duration (ML).</param>
        /// <param name="pessimistic">The pessimistic duration (P).</param>
        /// <exception cref="ArgumentOutOfRangeException">The values do not follow the rule 0 &lt;= O &lt;= ML &lt;= P, or one is above the maximum time value.</exception>
        public DurationEstimate(int optimistic, int mostLikely, int pessimistic)
        {
            if (optimistic < GraphLimits.MinimumTimeValue)
            {
                throw new ArgumentOutOfRangeException(nameof(optimistic), optimistic, @"The optimistic duration cannot be negative.");
            }
            if (mostLikely < optimistic)
            {
                throw new ArgumentOutOfRangeException(nameof(mostLikely), mostLikely, @"The most likely duration cannot be less than the optimistic duration.");
            }
            if (pessimistic < mostLikely)
            {
                throw new ArgumentOutOfRangeException(nameof(pessimistic), pessimistic, @"The pessimistic duration cannot be less than the most likely duration.");
            }
            if (pessimistic > GraphLimits.MaximumTimeValue)
            {
                throw new ArgumentOutOfRangeException(nameof(pessimistic), pessimistic, @"The pessimistic duration cannot be more than the maximum time value.");
            }
            Optimistic = optimistic;
            MostLikely = mostLikely;
            Pessimistic = pessimistic;
        }

        /// <summary>
        /// Estimates that give one duration with no uncertainty.
        /// </summary>
        /// <param name="duration">The duration.</param>
        public static DurationEstimate Fixed(int duration) => new DurationEstimate(duration, duration, duration);

        /// <summary>
        /// The optimistic duration (O).
        /// </summary>
        public int Optimistic { get; }

        /// <summary>
        /// The most likely duration (ML).
        /// </summary>
        public int MostLikely { get; }

        /// <summary>
        /// The pessimistic duration (P).
        /// </summary>
        public int Pessimistic { get; }

        /// <summary>
        /// Whether the duration is uncertain: O and P are not the same.
        /// </summary>
        public bool HasSpread => Optimistic != Pessimistic;

        /// <summary>
        /// The PERT expected duration, (O + 4 ML + P) / 6, rounded to the nearest whole time unit,
        /// with halves up. With O = ML = P it is ML.
        /// </summary>
        public int ExpectedDuration => (Optimistic + (4 * MostLikely) + Pessimistic + 3) / 6;

        /// <summary>
        /// The PERT standard deviation, (P - O) / 6.
        /// </summary>
        public double StandardDeviation => (Pessimistic - Optimistic) / 6.0;

        /// <inheritdoc/>
        public bool Equals(DurationEstimate other) =>
            Optimistic == other.Optimistic && MostLikely == other.MostLikely && Pessimistic == other.Pessimistic;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is DurationEstimate other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Optimistic, MostLikely, Pessimistic);

        /// <summary>
        /// Whether two sets of estimates are the same.
        /// </summary>
        public static bool operator ==(DurationEstimate left, DurationEstimate right) => left.Equals(right);

        /// <summary>
        /// Whether two sets of estimates are different.
        /// </summary>
        public static bool operator !=(DurationEstimate left, DurationEstimate right) => !left.Equals(right);

        /// <inheritdoc/>
        public override string ToString() => $@"{Optimistic}/{MostLikely}/{Pessimistic}";
    }
}
