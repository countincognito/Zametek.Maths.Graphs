using System;

namespace Zametek.Maths.Graphs
{
    // The generator of random numbers for the simulation, and the algorithms that turn its bits
    // into the draws that the samplers need.
    //
    // The generator is xoshiro256** (Blackman and Vigna), with its state set from a seed by
    // splitmix64. Both algorithms are fully specified, and they need only 64-bit integer
    // arithmetic. The Rust port repeats them, and for one seed the two give one sequence.
    // System.Random is not an option: the sequence of that class is defined only in .NET, and is
    // not stable from one release of .NET to the next.
    //
    // Each draw takes a fixed number of steps for given bits, and uses only DeterministicMath and
    // the operations of IEEE 754. The same seed therefore gives the same draws in every language
    // and on every platform. The class has no state beyond the four words of the generator: the
    // normal sampler throws away its second value on purpose.
    internal sealed class SimulationRandom
    {
        private const ulong c_SplitMixIncrement = 0x9E3779B97F4A7C15UL;

        private ulong m_S0;
        private ulong m_S1;
        private ulong m_S2;
        private ulong m_S3;

        // A generator in a given state, for the tests that check the algorithm against its
        // published values.
        internal SimulationRandom(ulong s0, ulong s1, ulong s2, ulong s3)
        {
            m_S0 = s0;
            m_S1 = s1;
            m_S2 = s2;
            m_S3 = s3;
        }

        // The generator of one chunk of iterations. See ChunkSeed.
        internal SimulationRandom(ulong seed)
        {
            ulong x = seed;
            m_S0 = SplitMix(ref x);
            m_S1 = SplitMix(ref x);
            m_S2 = SplitMix(ref x);
            m_S3 = SplitMix(ref x);
        }

        // The seed of one chunk of iterations (the chunks count from 0). It is the output of
        // splitmix64 that follows the main seed, chunk steps further on. The seeds of the chunks
        // are therefore unrelated 64-bit numbers, and so the
        // generators of two chunks do not share a part of the same sequence. A result then does not
        // depend on the thread that runs a chunk, or on the order in which the chunks finish.
        internal static ulong ChunkSeed(ulong seed, int chunk)
        {
            ulong x = unchecked(seed + ((ulong)chunk * c_SplitMixIncrement));
            return SplitMix(ref x);
        }

        internal static ulong SplitMix(ref ulong x)
        {
            unchecked
            {
                x += c_SplitMixIncrement;
                ulong z = x;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));

        internal ulong NextUInt64()
        {
            unchecked
            {
                ulong result = RotateLeft(m_S1 * 5, 7) * 9;
                ulong t = m_S1 << 17;
                m_S2 ^= m_S0;
                m_S3 ^= m_S1;
                m_S1 ^= m_S2;
                m_S0 ^= m_S3;
                m_S2 ^= t;
                m_S3 = RotateLeft(m_S3, 45);
                return result;
            }
        }

        // In [0, 1), with 53 random bits. The number is a multiple of 2 to the power -53.
        internal double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

        // In (0, 1], for the logarithms.
        internal double NextDoubleOpen() => 1.0 - NextDouble();

        // The polar method of Marsaglia. It needs no sine or cosine, which the platforms do not
        // agree on. The second value of each pair is thrown away.
        internal double NextGaussian()
        {
            while (true)
            {
                double u = (2.0 * NextDouble()) - 1.0;
                double v = (2.0 * NextDouble()) - 1.0;
                double s = (u * u) + (v * v);
                if (s < 1.0 && s != 0.0)
                {
                    return u * Math.Sqrt(-2.0 * DeterministicMath.Ln(s) / s);
                }
            }
        }

        // Marsaglia and Tsang. A shape below 1 is raised by one, and the draw is then corrected
        // by a power of a uniform number.
        internal double NextGamma(double shape)
        {
            if (shape < 1.0)
            {
                double boosted = NextGamma(shape + 1.0);
                return boosted * DeterministicMath.Exp(DeterministicMath.Ln(NextDoubleOpen()) / shape);
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
                if (DeterministicMath.Ln(u) < (0.5 * x * x) + (d * (1.0 - v + DeterministicMath.Ln(v))))
                {
                    return d * v;
                }
            }
        }

        // A beta draw in (0, 1), from two gamma draws.
        internal double NextBeta(double alpha, double beta)
        {
            double a = NextGamma(alpha);
            double b = NextGamma(beta);
            return a / (a + b);
        }
    }
}
