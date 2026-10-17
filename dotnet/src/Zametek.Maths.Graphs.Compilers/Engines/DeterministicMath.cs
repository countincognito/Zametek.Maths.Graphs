using System;

namespace Zametek.Maths.Graphs
{
    // The natural logarithm and the exponential, for the simulation, written so that the same
    // arguments give the same bits in every language and on every platform.
    //
    // Math.Log and Math.Exp call the maths library of the platform, and the platforms do not
    // agree in the last bit. A simulation that must give the same iterations in .NET and in Rust,
    // for the same seed, cannot use them. The functions here use only addition, subtraction,
    // multiplication, division and the bit patterns of a double. IEEE 754 fixes the result of each
    // of those to the bit, and neither .NET nor Rust fuses a multiplication with an addition unless
    // the code says so. The order of the operations below is therefore part of the contract: the
    // Rust port repeats it exactly, and a shared file of expected values checks both.
    //
    // The two functions are accurate to about one unit in the last place. That is enough for
    // sampling, and it is checked against Math.Log and Math.Exp in the tests.
    internal static class DeterministicMath
    {
        // ln 2 in two parts. The high part has 21 trailing zero bits in its significand, so that
        // the product of it and any integer of 21 bits or fewer is exact.
        private const double c_Ln2High = 6.93147180369123816490e-01;
        private const double c_Ln2Low = 1.90821492927058770002e-10;

        private const double c_InverseLn2 = 1.44269504088896338700e+00;
        private const double c_SquareRootOfTwo = 1.41421356237309504880e+00;

        // exp(x) is larger than the largest double above this value, and smaller than the
        // smallest subnormal double below the other.
        private const double c_ExpOverflow = 709.78271289338397;
        private const double c_ExpUnderflow = -745.13321910194122;

        private const long c_ExponentMask = 0x7FF0000000000000L;
        private const long c_SignificandMask = 0x000FFFFFFFFFFFFFL;
        private const long c_ExponentOfOne = 0x3FF0000000000000L;

        // The natural logarithm of x. The result is NaN for a negative x or a NaN, negative
        // infinity for zero, and infinity for infinity.
        internal static double Ln(double x)
        {
            if (!(x > 0.0))
            {
                return x == 0.0 ? double.NegativeInfinity : double.NaN;
            }
            if (double.IsPositiveInfinity(x))
            {
                return x;
            }

            int k = 0;
            long bits = BitConverter.DoubleToInt64Bits(x);
            if ((bits & c_ExponentMask) == 0)
            {
                // A subnormal number: scale it into the normal range first (2 to the power 54).
                x *= 18014398509481984.0;
                bits = BitConverter.DoubleToInt64Bits(x);
                k = -54;
            }
            k += (int)((bits >> 52) & 0x7FF) - 1023;

            // x is m times 2 to the power k, with m in [1, 2). Move m into [sqrt(2) / 2, sqrt(2)]
            // so that the series below is short.
            double m = BitConverter.Int64BitsToDouble((bits & c_SignificandMask) | c_ExponentOfOne);
            if (m > c_SquareRootOfTwo)
            {
                m *= 0.5;
                k += 1;
            }

            // ln(m) = 2 s (1 + z / 3 + z^2 / 5 + ...), with s = f / (2 + f), z = s s and f = m - 1.
            // The subtraction is exact, because m is in [1/2, 2].
            double f = m - 1.0;
            double s = f / (2.0 + f);
            double z = s * s;
            double series = 1.0 / 25.0;
            series = (1.0 / 23.0) + (z * series);
            series = (1.0 / 21.0) + (z * series);
            series = (1.0 / 19.0) + (z * series);
            series = (1.0 / 17.0) + (z * series);
            series = (1.0 / 15.0) + (z * series);
            series = (1.0 / 13.0) + (z * series);
            series = (1.0 / 11.0) + (z * series);
            series = (1.0 / 9.0) + (z * series);
            series = (1.0 / 7.0) + (z * series);
            series = (1.0 / 5.0) + (z * series);
            series = (1.0 / 3.0) + (z * series);
            series = 1.0 + (z * series);
            double lnm = 2.0 * s * series;

            double e = k;
            return (e * c_Ln2High) + ((e * c_Ln2Low) + lnm);
        }

        // e to the power x. The result is zero below the range of a double, and infinity above it.
        internal static double Exp(double x)
        {
            if (double.IsNaN(x))
            {
                return x;
            }
            if (x > c_ExpOverflow)
            {
                return double.PositiveInfinity;
            }
            if (x < c_ExpUnderflow)
            {
                return 0.0;
            }

            // x = k ln 2 + r, with |r| at most about 0.35.
            double kd = Math.Floor((x * c_InverseLn2) + 0.5);
            int k = (int)kd;
            double r = (x - (kd * c_Ln2High)) - (kd * c_Ln2Low);

            // 1 + r (1 + r / 2 (1 + r / 3 (1 + ...))), to the term r^14 / 14!.
            double series = 1.0;
            for (int n = 14; n >= 1; n--)
            {
                series = 1.0 + ((r / n) * series);
            }

            // The scaling by 2 to the power k, in two steps when k is outside the range of one
            // power of two. Both steps are exact, except at the very end of the subnormal range.
            int first = k / 2;
            int second = k - first;
            return series * PowerOfTwo(first) * PowerOfTwo(second);
        }

        // 2 to the power n, for n in [-1022, 1023].
        private static double PowerOfTwo(int n) => BitConverter.Int64BitsToDouble((long)(n + 1023) << 52);
    }
}
