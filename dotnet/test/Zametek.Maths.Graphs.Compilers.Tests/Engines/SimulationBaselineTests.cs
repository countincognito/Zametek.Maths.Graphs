using Shouldly;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // The numbers that the Rust port must give, bit for bit.
    //
    // The simulation promises one thing across languages: for one seed, the Rust port gives the
    // same draws, and so the same iterations, as this library. A promise like that needs a test
    // that fails on the last bit, and so the file SimulationBaseline.txt holds the exact bit
    // patterns of the logarithm, the exponential, the generator, the draws of every sampler,
    // and later the iterations of a whole simulation. The Rust tests read the same file and
    // compute the same lines.
    //
    // If this test fails, a change has altered a number that the Rust port must also give. Change
    // the Rust port to match, and then regenerate the file with the explicit RegenerateBaseline
    // test below. Regenerate it only when the numbers are meant to change: a stored result of a
    // simulation (a seed and its finish times) is then no longer the result of that seed.
    public class SimulationBaselineTests
    {
        private static readonly string s_BaselineRelativePath =
            Path.Combine(@"Compilers", @"TestFiles", @"SimulationBaseline.txt");

        #region Lines

        private static string Hex(double value) =>
            BitConverter.DoubleToInt64Bits(value).ToString(@"X16", CultureInfo.InvariantCulture);

        private static string Hex(ulong value) => value.ToString(@"X16", CultureInfo.InvariantCulture);

        private static string Line(string name, IEnumerable<string> values) => $@"{name}: {string.Join(@",", values)}";

        // The arguments of the logarithm and of the exponential. They come from integer
        // arithmetic only, so that the file does not depend on the generator that it checks.
        private static IEnumerable<double> LnArguments()
        {
            yield return 1.0;
            yield return 2.0;
            yield return 0.5;
            yield return 10.0;
            yield return 1e-10;
            yield return 1e10;
            yield return 1e-300;
            yield return 1e300;
            yield return 4.9406564584124654e-324;
            yield return 2.2250738585072009e-308;
            yield return 1.4142135623730951;
            yield return 0.7071067811865476;
            for (int i = 1; i <= 120; i++)
            {
                yield return i * 0.0137;
            }
            double power = 9.5367431640625e-7;
            for (int i = 0; i < 40; i++)
            {
                yield return power * (1.0 + (i * 0.021));
                power *= 2.0;
            }
        }

        private static IEnumerable<double> ExpArguments()
        {
            yield return 0.0;
            yield return 1.0;
            yield return -1.0;
            yield return 0.5;
            yield return 700.0;
            yield return -700.0;
            yield return 709.0;
            yield return -745.0;
            for (int i = -100; i <= 100; i++)
            {
                yield return i * 0.731;
            }
        }

        private static IEnumerable<string> BuildCurrentLines()
        {
            yield return Line(@"ln", LnArguments().Select(x => $@"{Hex(x)}={Hex(DeterministicMath.Ln(x))}"));
            yield return Line(@"exp", ExpArguments().Select(x => $@"{Hex(x)}={Hex(DeterministicMath.Exp(x))}"));

            foreach (ulong seed in new ulong[] { 0UL, 1UL, 42UL, 0xDEADBEEFCAFEF00DUL, ulong.MaxValue })
            {
                var random = new SimulationRandom(seed);
                yield return Line($@"uint64 seed={Hex(seed)}", Enumerable.Range(0, 8).Select(_ => Hex(random.NextUInt64())));
                random = new SimulationRandom(seed);
                yield return Line($@"double seed={Hex(seed)}", Enumerable.Range(0, 8).Select(_ => Hex(random.NextDouble())));
                random = new SimulationRandom(seed);
                yield return Line($@"gaussian seed={Hex(seed)}", Enumerable.Range(0, 8).Select(_ => Hex(random.NextGaussian())));
                yield return Line($@"chunkseed seed={Hex(seed)}", Enumerable.Range(0, 8).Select(x => Hex(SimulationRandom.ChunkSeed(seed, x))));
            }

            foreach (double shape in new[] { 0.5, 0.667, 1.0, 2.5, 9.0 })
            {
                var random = new SimulationRandom(7UL);
                yield return Line($@"gamma shape={Hex(shape)}", Enumerable.Range(0, 8).Select(_ => Hex(random.NextGamma(shape))));
            }

            foreach ((double alpha, double beta) in new[] { (0.667, 2.0), (1.0, 1.0), (3.0, 5.0) })
            {
                var random = new SimulationRandom(8UL);
                yield return Line($@"beta alpha={Hex(alpha)} beta={Hex(beta)}", Enumerable.Range(0, 8).Select(_ => Hex(random.NextBeta(alpha, beta))));
            }

            var estimates = new[]
            {
                new DurationEstimate(2, 4, 6),
                new DurationEstimate(4, 5, 12),
                new DurationEstimate(0, 3, 10),
                new DurationEstimate(5, 5, 9),
                new DurationEstimate(7, 9, 9),
                new DurationEstimate(0, 0, 5),
                DurationEstimate.Fixed(3),
            };
            foreach (DurationDistribution distribution in Enum.GetValues(typeof(DurationDistribution)))
            {
                foreach (DurationEstimate estimate in estimates)
                {
                    var random = new SimulationRandom(9UL);
                    var draws = new List<double>();
                    for (int i = 0; i < 8; i++)
                    {
                        draws.Add(DurationSampler.Sample(distribution, random, estimate, DurationSampler.DefaultModifiedBetaShape));
                    }
                    yield return Line(
                        $@"sample {distribution} {estimate}",
                        draws.Select(x => $@"{Hex(x)}={DurationSampler.RoundToWholeUnits(x, estimate).ToString(CultureInfo.InvariantCulture)}"));
                }
            }
        }

        #endregion

        #region File

        private static List<string> ReadBaselineLines()
        {
            string path = Path.Combine(AppContext.BaseDirectory, s_BaselineRelativePath);
            File.Exists(path).ShouldBeTrue(
                $@"The simulation baseline is missing from the test output at {path}. Run the RegenerateBaseline test to create it.");
            return File.ReadAllLines(path).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        // Walks up from the test output directory to the test project directory, so that the
        // regeneration test writes the baseline back into source control and not into bin.
        private static string FindSourceBaselinePath()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && directory.GetFiles(@"*.csproj").Length == 0)
            {
                directory = directory.Parent;
            }
            directory.ShouldNotBeNull(@"Could not locate the test project directory from the test output directory.");
            return Path.Combine(directory!.FullName, s_BaselineRelativePath);
        }

        #endregion

        [Fact]
        public void Baseline_Given_TheCommittedFile_Then_EveryNumberIsTheSameBitForBit()
        {
            List<string> expected = ReadBaselineLines();
            List<string> actual = BuildCurrentLines().ToList();

            actual.Count.ShouldBe(expected.Count, @"The baseline has changed size; it needs regenerating deliberately.");

            var divergences = new List<string>();
            for (int i = 0; i < expected.Count; i++)
            {
                if (!string.Equals(expected[i], actual[i], StringComparison.Ordinal))
                {
                    divergences.Add($@"expected: {expected[i]}{Environment.NewLine}  actual: {actual[i]}");
                }
            }

            divergences.ShouldBeEmpty(
                $@"{divergences.Count} of {expected.Count} lines of the baseline changed:{Environment.NewLine}{string.Join(Environment.NewLine, divergences.Take(3))}");
        }

        [Fact(Explicit = true)]
        public void RegenerateBaseline()
        {
            // Explicit: run only when the numbers are meant to change, and change the Rust port too.
            //   dotnet test --filter "FullyQualifiedName~SimulationBaselineTests.RegenerateBaseline" -- xUnit.ExplicitTests=on
            string path = FindSourceBaselinePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var builder = new StringBuilder();
            foreach (string line in BuildCurrentLines())
            {
                builder.Append(line).Append('\n');
            }
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }
    }
}
