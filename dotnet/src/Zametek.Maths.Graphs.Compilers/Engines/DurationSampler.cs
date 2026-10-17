using System;

namespace Zametek.Maths.Graphs
{
    // The draws of a duration, from the three estimates of an activity, for each distribution.
    //
    // A sampler gives a real number in the range from O to P. The simulation rounds it to whole
    // time units (RoundToWholeUnits), because the scheduler works in whole units and a result must
    // be the same as that of a compile. The samplers take their numbers from SimulationRandom, in
    // an order that is fixed by the code below: the Rust port draws in the same order.
    internal static class DurationSampler
    {
        // The shape value of the usual modified PERT beta.
        internal const double DefaultModifiedBetaShape = 4.0;

        // One draw. An activity with no spread (O = P) takes no number from the generator.
        internal static double Sample(
            DurationDistribution distribution,
            SimulationRandom random,
            DurationEstimate estimate,
            double modifiedBetaShape)
        {
            double optimistic = estimate.Optimistic;
            double mostLikely = estimate.MostLikely;
            double pessimistic = estimate.Pessimistic;

            if (!estimate.HasSpread)
            {
                return optimistic;
            }

            switch (distribution)
            {
                case DurationDistribution.BetaPert:
                    return BetaPert(random, optimistic, mostLikely, pessimistic);
                case DurationDistribution.ModifiedBetaPert:
                    return ModifiedBetaPert(random, optimistic, mostLikely, pessimistic, modifiedBetaShape);
                case DurationDistribution.Triangular:
                    return Triangular(random, optimistic, mostLikely, pessimistic);
                case DurationDistribution.Normal:
                    return Normal(random, optimistic, mostLikely, pessimistic);
                case DurationDistribution.Uniform:
                    return optimistic + (random.NextDouble() * (pessimistic - optimistic));
                default:
                    throw new ArgumentOutOfRangeException(nameof(distribution), distribution, @"The distribution is not known.");
            }
        }

        // A draw in whole time units: the nearest whole number, with halves up, in the range from
        // O to P. The range holds already, because a draw is within it, and the limit protects the
        // result against the last bit of a calculation.
        internal static int RoundToWholeUnits(double draw, DurationEstimate estimate)
        {
            double rounded = Math.Round(draw, MidpointRounding.AwayFromZero);
            if (rounded < estimate.Optimistic)
            {
                return estimate.Optimistic;
            }
            if (rounded > estimate.Pessimistic)
            {
                return estimate.Pessimistic;
            }
            return (int)rounded;
        }

        // The beta of the reference PERT tools: a beta distribution on the range from O to P
        // whose mean is the PERT mean, (O + 4 ML + P) / 6, and whose variance is the PERT variance,
        // ((P - O) / 6) squared. Its parameters follow from the method of moments.
        internal static double BetaPert(SimulationRandom random, double optimistic, double mostLikely, double pessimistic)
        {
            double range = pessimistic - optimistic;
            double mean = (optimistic + (4.0 * mostLikely) + pessimistic) / 6.0;
            double standardDeviation = range / 6.0;
            double fromOptimistic = mean - optimistic;
            double toPessimistic = pessimistic - mean;

            double alpha = fromOptimistic / range
                * ((fromOptimistic * toPessimistic / (standardDeviation * standardDeviation)) - 1.0);
            double beta = alpha * toPessimistic / fromOptimistic;
            return optimistic + (random.NextBeta(alpha, beta) * range);
        }

        // The modified PERT beta: a beta distribution on the range from O to P whose mode is ML.
        // A larger shape value draws closer to the mode.
        internal static double ModifiedBetaPert(
            SimulationRandom random,
            double optimistic,
            double mostLikely,
            double pessimistic,
            double shape)
        {
            double range = pessimistic - optimistic;
            double alpha = 1.0 + (shape * (mostLikely - optimistic) / range);
            double beta = 1.0 + (shape * (pessimistic - mostLikely) / range);
            return optimistic + (random.NextBeta(alpha, beta) * range);
        }

        internal static double Triangular(SimulationRandom random, double minimum, double mode, double maximum)
        {
            double range = maximum - minimum;
            double u = random.NextDouble();
            double fraction = (mode - minimum) / range;
            return u < fraction
                ? minimum + Math.Sqrt(u * range * (mode - minimum))
                : maximum - Math.Sqrt((1.0 - u) * range * (maximum - mode));
        }

        // The PERT mean and the PERT standard deviation, held to the range from O to P.
        internal static double Normal(SimulationRandom random, double optimistic, double mostLikely, double pessimistic)
        {
            double mean = (optimistic + (4.0 * mostLikely) + pessimistic) / 6.0;
            double standardDeviation = (pessimistic - optimistic) / 6.0;
            double draw = mean + (standardDeviation * random.NextGaussian());
            return Math.Min(pessimistic, Math.Max(optimistic, draw));
        }
    }
}
