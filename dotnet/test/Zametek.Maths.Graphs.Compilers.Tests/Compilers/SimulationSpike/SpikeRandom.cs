using System;

namespace Zametek.Maths.Graphs.Tests
{
    // A seeded generator and the four samplers of the spike.
    //
    // The generator is xoshiro256** seeded through splitmix64. It is fully specified and it
    // needs only 64-bit integer arithmetic, so the same code can be written in Rust and
    // give the same sequence. System.Random is not an option for that reason: its sequence
    // is not defined outside .NET. Phase 2 makes the real choice. This one stands in for it.
    internal sealed class SpikeRandom
    {
        private ulong m_S0;
        private ulong m_S1;
        private ulong m_S2;
        private ulong m_S3;

        internal SpikeRandom(ulong seed)
        {
            ulong x = seed;
            m_S0 = SplitMix(ref x);
            m_S1 = SplitMix(ref x);
            m_S2 = SplitMix(ref x);
            m_S3 = SplitMix(ref x);
        }

        private static ulong SplitMix(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

        internal ulong NextUInt64()
        {
            ulong result = Rotl(m_S1 * 5, 7) * 9;
            ulong t = m_S1 << 17;
            m_S2 ^= m_S0;
            m_S3 ^= m_S1;
            m_S1 ^= m_S2;
            m_S0 ^= m_S3;
            m_S2 ^= t;
            m_S3 = Rotl(m_S3, 45);
            return result;
        }

        // In [0, 1), with 53 random bits.
        internal double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

        // In (0, 1], for the logarithms.
        internal double NextDoubleOpen() => 1.0 - NextDouble();

        // Box-Muller. The second value is thrown away, which costs a little speed and keeps
        // the generator free of hidden state beyond its four words.
        internal double NextGaussian()
        {
            double u1 = NextDoubleOpen();
            double u2 = NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        // Marsaglia and Tsang. A shape under 1 is raised by one and corrected afterwards.
        internal double NextGamma(double shape)
        {
            if (shape < 1.0)
            {
                double boosted = NextGamma(shape + 1.0);
                return boosted * Math.Pow(NextDoubleOpen(), 1.0 / shape);
            }

            double d = shape - (1.0 / 3.0);
            double c = 1.0 / Math.Sqrt(9.0 * d);
            while (true)
            {
                double x = NextGaussian();
                double v = 1.0 + (c * x);
                if (v <= 0.0)
                {
                    continue;
                }
                v = v * v * v;
                double u = NextDoubleOpen();
                if (u < 1.0 - (0.0331 * x * x * x * x))
                {
                    return d * v;
                }
                if (Math.Log(u) < (0.5 * x * x) + (d * (1.0 - v + Math.Log(v))))
                {
                    return d * v;
                }
            }
        }

        internal double NextBeta(double alpha, double beta)
        {
            double a = NextGamma(alpha);
            double b = NextGamma(beta);
            return a / (a + b);
        }
    }

    internal enum SpikeDistribution
    {
        // The beta of the reference tool: its mean and its variance are those of the PERT
        // formulas, (O + 4 ML + P) / 6 and ((P - O) / 6) squared.
        BetaPertMomentMatched,

        // The usual modified PERT beta: its mode is ML and its shape value is 4.
        BetaPertShape4,

        Triangular,

        Normal,

        Uniform,
    }

    internal static class SpikeSamplers
    {
        internal static double Sample(SpikeDistribution distribution, SpikeRandom random, double optimistic, double mostLikely, double pessimistic)
        {
            switch (distribution)
            {
                case SpikeDistribution.BetaPertMomentMatched:
                    return BetaPertMomentMatched(random, optimistic, mostLikely, pessimistic);
                case SpikeDistribution.BetaPertShape4:
                    return BetaPertShaped(random, optimistic, mostLikely, pessimistic, 4.0);
                case SpikeDistribution.Triangular:
                    return Triangular(random, optimistic, mostLikely, pessimistic);
                case SpikeDistribution.Normal:
                    return Normal(random, optimistic, mostLikely, pessimistic);
                default:
                    return optimistic + (random.NextDouble() * (pessimistic - optimistic));
            }
        }

        // The mean and the standard deviation come from the PERT formulas, and the draw is
        // limited to the range from O to P. The reference tool does not limit it.
        internal static double Normal(SpikeRandom random, double optimistic, double mostLikely, double pessimistic)
        {
            double mean = (optimistic + (4.0 * mostLikely) + pessimistic) / 6.0;
            double standardDeviation = (pessimistic - optimistic) / 6.0;
            double draw = mean + (standardDeviation * random.NextGaussian());
            return Math.Min(pessimistic, Math.Max(optimistic, draw));
        }

        internal static double Triangular(SpikeRandom random, double minimum, double mode, double maximum)
        {
            if (maximum <= minimum)
            {
                return minimum;
            }
            double u = random.NextDouble();
            double fraction = (mode - minimum) / (maximum - minimum);
            return u < fraction
                ? minimum + Math.Sqrt(u * (maximum - minimum) * (mode - minimum))
                : maximum - Math.Sqrt((1.0 - u) * (maximum - minimum) * (maximum - mode));
        }

        // The algorithm of the reference tool: a beta on [O, P] whose mean and variance are
        // the PERT mean and the PERT variance.
        internal static double BetaPertMomentMatched(SpikeRandom random, double optimistic, double mostLikely, double pessimistic)
        {
            if (pessimistic <= optimistic)
            {
                return optimistic;
            }

            double mean = (optimistic + (4.0 * mostLikely) + pessimistic) / 6.0;
            double standardDeviation = (pessimistic - optimistic) / 6.0;

            // The reference tool falls back to a normal draw when the order is wrong.
            if (optimistic > mostLikely || mostLikely > pessimistic)
            {
                return mean + (standardDeviation * random.NextGaussian());
            }

            double alpha = (mean - optimistic) / (pessimistic - optimistic)
                * (((mean - optimistic) * (pessimistic - mean) / (standardDeviation * standardDeviation)) - 1.0);
            double beta = alpha * (pessimistic - mean) / (mean - optimistic);
            return optimistic + (random.NextBeta(alpha, beta) * (pessimistic - optimistic));
        }

        internal static double BetaPertShaped(SpikeRandom random, double optimistic, double mostLikely, double pessimistic, double shape)
        {
            if (pessimistic <= optimistic)
            {
                return optimistic;
            }
            double range = pessimistic - optimistic;
            double alpha = 1.0 + (shape * (mostLikely - optimistic) / range);
            double beta = 1.0 + (shape * (pessimistic - mostLikely) / range);
            return optimistic + (random.NextBeta(alpha, beta) * range);
        }
    }
}
