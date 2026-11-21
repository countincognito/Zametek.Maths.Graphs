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
    // The comparison of a whole compile between C# and the Rust port, plan by plan, on random plans.
    //
    // The two languages build the same plans (CompileParityCorpus, and its port in
    // rust/compilers/tests/compile_parity_corpus/mod.rs). Each compiles a plan with VertexGraphCompiler, the resource
    // scheduler included, writes the whole result as one canonical text, and takes a hash of the text. The baseline file
    // has one line for each plan: category, index, finish time, error codes and hash. C# writes the file
    // (RegenerateBaseline), C# checks it here, and Rust checks the same file (rust/compilers/tests/compile_parity_tests.rs).
    // Thus, the two languages give the same result for each plan as long as both tests pass.
    //
    // When this test fails and the Rust test passes, C# changed and Rust did not, or the other way round. A test that
    // fails prints the plan and the canonical text of its own result. To see what differs, make the other side print the
    // same plan: change the hash of that plan in the baseline file by hand, and run the test of the other side.
    //
    // Regenerate the baseline only when the result of a compile is intended to change, and then make the same change in
    // Rust: the Rust test must pass against the new file.
    public class CompileParityTests
    {
        // The plans of each category in the committed baseline.
        private const int c_PlansPerCategory = 500;

        // The plans of each category in the large baseline, when the environment variable does not give another number.
        private const int c_LargePlansPerCategory = 5000;

        // The path of the large baseline, for WriteLargeBaseline here and for the ignored test in Rust.
        private const string c_LargeBaselinePathVariable = @"COMPILE_PARITY_BASELINE_PATH";

        // The plans of each category in the large baseline.
        private const string c_LargePlansPerCategoryVariable = @"COMPILE_PARITY_PLANS_PER_CATEGORY";

        // How many plans that differ a failing test prints in full.
        private const int c_MaximumPrinted = 3;

        private static readonly string s_BaselineRelativePath =
            Path.Combine(@"Compilers", @"TestFiles", @"CompileParityBaseline.txt");

        private readonly ITestOutputHelper m_Output;

        public CompileParityTests(ITestOutputHelper output)
        {
            m_Output = output;
        }

        #region Helpers

        // The baseline lines of 'plansPerCategory' plans of each category, in the order of the file.
        private static IEnumerable<string> BuildCurrentLines(int plansPerCategory)
        {
            foreach (string category in CompileParityCorpus.Categories)
            {
                for (int index = 0; index < plansPerCategory; index++)
                {
                    CompileParityCorpus.PlanSpec plan = CompileParityCorpus.Generate(category, index);
                    CompileParityCorpus.CompileResult result = CompileParityCorpus.Compile(plan, TestContext.Current.CancellationToken);
                    yield return CompileParityCorpus.BaselineLine(plan, result);
                }
            }
        }

        // The lines end with '\n' on each system, so that the file is the same wherever it is written. The tests that
        // read it take either line ending.
        private static void WriteLines(string path, IEnumerable<string> lines)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var contents = new StringBuilder();
            foreach (string line in lines)
            {
                contents.Append(line).Append('\n');
            }
            File.WriteAllText(path, contents.ToString());
        }

        // File.ReadAllLines takes "\n" and "\r\n".
        private static List<string> ReadBaselineLines()
        {
            string path = Path.Combine(AppContext.BaseDirectory, s_BaselineRelativePath);
            File.Exists(path).ShouldBeTrue(
                $@"The compile parity baseline is missing from the test output at {path}. Run the RegenerateBaseline test to create it.");
            return File.ReadAllLines(path).Select(x => x.Trim()).Where(x => x.Length != 0).ToList();
        }

        // Walks up from the test output directory to the test project directory, so the
        // regeneration test can write the baseline back into source control rather than
        // into bin.
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
        public void Compile_GivenCorpus_ThenMatchesCommittedBaseline()
        {
            List<string> expected = ReadBaselineLines();

            expected.Count.ShouldBe(CompileParityCorpus.Categories.Length * c_PlansPerCategory,
                @"The corpus has changed size; the baseline needs regenerating deliberately.");

            int position = 0;
            var divergences = new List<string>();
            foreach (string category in CompileParityCorpus.Categories)
            {
                for (int index = 0; index < c_PlansPerCategory; index++)
                {
                    CompileParityCorpus.PlanSpec plan = CompileParityCorpus.Generate(category, index);
                    CompileParityCorpus.CompileResult result = CompileParityCorpus.Compile(plan, TestContext.Current.CancellationToken);
                    string actual = CompileParityCorpus.BaselineLine(plan, result);

                    if (!string.Equals(expected[position], actual, StringComparison.Ordinal))
                    {
                        // The plan and the whole result of this side, so that the other side can be put next to it.
                        string divergence = string.Join('\n',
                            $@"category {category}, index {index}",
                            $@"expected: {expected[position]}",
                            $@"  actual: {actual}",
                            CompileParityCorpus.FormatPlan(plan) + result.Text);
                        if (divergences.Count < c_MaximumPrinted)
                        {
                            m_Output.WriteLine(divergence);
                        }
                        divergences.Add(divergence);
                    }
                    position++;
                }
            }

            divergences.ShouldBeEmpty(
                $@"The result of the compile changed for {divergences.Count} of {expected.Count} plans. The first:{Environment.NewLine}{divergences.FirstOrDefault()}");
        }

        // The hash is written out in both languages. These are the known values of 64-bit FNV-1a, and the Rust test
        // checks the same ones.
        [Theory]
        [InlineData(@"", 0xcbf29ce484222325UL)]
        [InlineData(@"a", 0xaf63dc4c8601ec8cUL)]
        [InlineData(@"foobar", 0x85944171f73967e8UL)]
        public void Fnv1a64_GivenKnownText_ThenGivesKnownHash(string text, ulong expected)
        {
            CompileParityCorpus.Fnv1a64(text).ShouldBe(expected);
        }

        [Fact(Explicit = true)]
        public void RegenerateBaseline()
        {
            // Explicit: run only when the result of a compile is intended to change.
            //   dotnet test --filter "FullyQualifiedName~CompileParityTests.RegenerateBaseline" -- xUnit.Explicit=only
            // Then build again (the build copies the file to the test output), and run the Rust test against the new file:
            //   cargo test --locked --test compile_parity_tests
            string path = FindSourceBaselinePath();
            WriteLines(path, BuildCurrentLines(c_PlansPerCategory));
            File.Exists(path).ShouldBeTrue();
        }

        [Fact(Explicit = true)]
        public void WriteLargeBaseline()
        {
            // Explicit: a one-off run on many more plans than the committed baseline has. It writes a baseline to a path
            // outside the repository, and the ignored Rust test checks that file. In PowerShell, from dotnet/:
            //   $env:COMPILE_PARITY_BASELINE_PATH = "$env:TEMP\CompileParityBaselineLarge.txt"
            //   dotnet test --configuration Release --filter "FullyQualifiedName~CompileParityTests.WriteLargeBaseline" -- xUnit.Explicit=only
            // and then, from rust/, with the same variable:
            //   cargo test --release --locked --test compile_parity_tests -- --ignored --nocapture
            // The file has 5,000 plans of each category. COMPILE_PARITY_PLANS_PER_CATEGORY gives another number; the Rust
            // test takes the number from the file.
            string path = Environment.GetEnvironmentVariable(c_LargeBaselinePathVariable);
            string.IsNullOrWhiteSpace(path).ShouldBeFalse(
                $@"Set {c_LargeBaselinePathVariable} to the path of the file to write, outside the repository.");

            string plansPerCategoryText = Environment.GetEnvironmentVariable(c_LargePlansPerCategoryVariable);
            int plansPerCategory = string.IsNullOrWhiteSpace(plansPerCategoryText)
                ? c_LargePlansPerCategory
                : int.Parse(plansPerCategoryText, CultureInfo.InvariantCulture);
            plansPerCategory.ShouldBeGreaterThan(0);

            var lines = new List<string>();
            foreach (string line in BuildCurrentLines(plansPerCategory))
            {
                lines.Add(line);
                // A long run must show that it goes on.
                if (lines.Count % 1000 == 0)
                {
                    m_Output.WriteLine($@"{lines.Count} of {CompileParityCorpus.Categories.Length * plansPerCategory} plans compiled");
                }
            }

            WriteLines(path, lines);
            m_Output.WriteLine($@"Wrote {lines.Count} lines to {path}");
        }
    }
}
