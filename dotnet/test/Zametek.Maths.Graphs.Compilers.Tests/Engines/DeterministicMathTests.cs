using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class DeterministicMathTests
    {
        // Numbers from 1e-300 to 1e300, in a fixed order, and with no use of a random generator.
        private static double[] Spread(int count)
        {
            var values = new double[count];
            for (int i = 0; i < count; i++)
            {
                double mantissa = 1.0 + ((i * 7919) % 1000) / 1000.0;
                int exponent = ((i * 31) % 601) - 300;
                values[i] = mantissa * Math.Pow(10.0, exponent);
            }
            return values;
        }

        [Fact]
        public void Ln_Given_ManyPositiveNumbers_Then_IsWithinAFewUnitsOfTheLastPlaceOfMathLog()
        {
            foreach (double x in Spread(2000))
            {
                double expected = Math.Log(x);
                double actual = DeterministicMath.Ln(x);
                Math.Abs(actual - expected).ShouldBeLessThanOrEqualTo(
                    Math.Max(Math.Abs(expected) * 8e-16, 1e-300),
                    $@"Ln({x:R})");
            }
        }

        [Fact]
        public void Ln_Given_NumbersNearOne_Then_IsAccurateInTheAbsoluteSense()
        {
            // The relative error of a logarithm near one is large for any method that is
            // accurate to the last place of its argument, and so this holds the absolute error.
            for (int i = -2000; i <= 2000; i++)
            {
                double x = 1.0 + (i * 1e-4);
                if (x <= 0.0)
                {
                    continue;
                }
                Math.Abs(DeterministicMath.Ln(x) - Math.Log(x)).ShouldBeLessThanOrEqualTo(4e-16, $@"Ln({x:R})");
            }
        }

        [Fact]
        public void Ln_Given_ASubnormalNumber_Then_IsTheLogarithm()
        {
            // The smallest positive double, and one a hundred times larger, are subnormal.
            DeterministicMath.Ln(double.Epsilon).ShouldBe(Math.Log(double.Epsilon), 1e-12);
            DeterministicMath.Ln(double.Epsilon * 100.0).ShouldBe(Math.Log(double.Epsilon * 100.0), 1e-12);
        }

        [Fact]
        public void Ln_Given_OneOrTheEdgesOfTheDomain_Then_HasTheUsualValue()
        {
            DeterministicMath.Ln(1.0).ShouldBe(0.0);
            DeterministicMath.Ln(0.0).ShouldBe(double.NegativeInfinity);
            double.IsNaN(DeterministicMath.Ln(-1.0)).ShouldBeTrue();
            double.IsNaN(DeterministicMath.Ln(double.NaN)).ShouldBeTrue();
            DeterministicMath.Ln(double.PositiveInfinity).ShouldBe(double.PositiveInfinity);
        }

        [Fact]
        public void Exp_Given_ManyNumbers_Then_IsWithinAFewUnitsOfTheLastPlaceOfMathExp()
        {
            for (int i = 0; i <= 4000; i++)
            {
                double x = -700.0 + (i * 0.35);
                double expected = Math.Exp(x);
                double actual = DeterministicMath.Exp(x);
                Math.Abs(actual - expected).ShouldBeLessThanOrEqualTo(expected * 8e-16, $@"Exp({x:R})");
            }
        }

        [Fact]
        public void Exp_Given_NumbersNearZero_Then_IsAccurate()
        {
            for (int i = -1000; i <= 1000; i++)
            {
                double x = i * 1e-3;
                DeterministicMath.Exp(x).ShouldBe(Math.Exp(x), Math.Exp(x) * 4e-16);
            }
        }

        [Fact]
        public void Exp_Given_TheEdgesOfTheRange_Then_IsZeroOrInfinity()
        {
            DeterministicMath.Exp(0.0).ShouldBe(1.0);
            DeterministicMath.Exp(710.0).ShouldBe(double.PositiveInfinity);
            DeterministicMath.Exp(-746.0).ShouldBe(0.0);
            DeterministicMath.Exp(double.NegativeInfinity).ShouldBe(0.0);
            double.IsNaN(DeterministicMath.Exp(double.NaN)).ShouldBeTrue();

            // The last numbers that a double can hold: the result is close to the largest and the
            // smallest double, and it is not zero or infinity.
            DeterministicMath.Exp(709.0).ShouldBe(Math.Exp(709.0), Math.Exp(709.0) * 1e-15);
            DeterministicMath.Exp(-740.0).ShouldBeGreaterThan(0.0);
        }

        [Fact]
        public void ExpAndLn_Given_ANumber_Then_AreInverse()
        {
            foreach (double x in Spread(500))
            {
                DeterministicMath.Exp(DeterministicMath.Ln(x)).ShouldBe(x, x * 4e-13);
            }
        }

        [Fact]
        public void Ln_Given_TheSameNumberTwice_Then_GivesTheSameBits()
        {
            foreach (double x in Spread(200))
            {
                BitConverter.DoubleToInt64Bits(DeterministicMath.Ln(x))
                    .ShouldBe(BitConverter.DoubleToInt64Bits(DeterministicMath.Ln(x)));
            }
        }
    }
}
