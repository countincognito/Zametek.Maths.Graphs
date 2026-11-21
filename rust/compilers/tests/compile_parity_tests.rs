//! The comparison of a whole compile between C# and this port, plan by plan, on
//! random plans - the counterpart of the C# `CompileParityTests`.
//!
//! The two languages build the same plans (`compile_parity_corpus`, a
//! value-for-value port of the C# `CompileParityCorpus`). Each compiles a plan
//! with `VertexGraphCompiler`, the resource scheduler included, writes the whole
//! result as one canonical text, and takes a hash of the text. The baseline file
//! has one line for each plan: category, index, finish time, error codes and
//! hash. C# writes the file, C# checks it, and the test here checks the same
//! file, at `dotnet/test/.../Compilers/TestFiles/CompileParityBaseline.txt`.
//! Thus, the two languages give the same result for each plan as long as both
//! tests pass. This port has no copy of the file and no test that writes it.
//!
//! A test that fails prints the plan and the canonical text of its own result.
//! To see what differs, make the other side print the same plan: change the
//! hash of that plan in the baseline file by hand, and run the test of the other
//! side.
//!
//! A one-off run on many more plans: write a large baseline with the C# test
//! `CompileParityTests.WriteLargeBaseline` (see its comment), and check it here
//! with the ignored test. In PowerShell, from `rust/`:
//!
//! ```text
//! $env:COMPILE_PARITY_BASELINE_PATH = "$env:TEMP\CompileParityBaselineLarge.txt"
//! cargo test --release --locked --test compile_parity_tests -- --ignored --nocapture
//! ```

mod compile_parity_corpus;
mod corpus_shapes;

use std::fs;
use std::path::{Path, PathBuf};

/// The plans of each category in the committed baseline.
const PLANS_PER_CATEGORY: usize = 500;

/// How many plans that differ a failing test prints in full.
const MAXIMUM_PRINTED: usize = 3;

/// The path of the large baseline, for the ignored test here and for the C#
/// test that writes it.
const LARGE_BASELINE_PATH_VARIABLE: &str = "COMPILE_PARITY_BASELINE_PATH";

const DOTNET_BASELINE_RELATIVE_PATH: &str =
    "../../dotnet/test/Zametek.Maths.Graphs.Compilers.Tests/Compilers/TestFiles/CompileParityBaseline.txt";

fn dotnet_baseline_path() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join(DOTNET_BASELINE_RELATIVE_PATH)
}

/// The lines of the file with no line ending, so that `"\n"` and `"\r\n"` give
/// the same lines.
fn read_lines(path: &Path) -> Vec<String> {
    let contents = fs::read_to_string(path)
        .unwrap_or_else(|error| panic!("could not read {}: {error}", path.display()));
    contents
        .lines()
        .map(|x| x.trim().to_string())
        .filter(|x| !x.is_empty())
        .collect()
}

/// Compiles `plans_per_category` plans of each category, in the order of the
/// file, and compares the line of each with the line of the baseline. A plan
/// that differs is printed with the whole result of this side, so that the
/// other side can be put next to it.
fn assert_matches_baseline(expected: &[String], plans_per_category: usize, source: &str) {
    assert_eq!(
        expected.len(),
        compile_parity_corpus::CATEGORIES.len() * plans_per_category,
        "the corpus has changed size against {source}; the baseline needs regenerating deliberately"
    );

    let mut position = 0;
    let mut divergence_count = 0;
    let mut first_divergence = String::new();
    for category in compile_parity_corpus::CATEGORIES {
        for index in 0..plans_per_category {
            let plan = compile_parity_corpus::generate(category, index as i32);
            let result = compile_parity_corpus::compile(&plan);
            let actual = compile_parity_corpus::baseline_line(&plan, &result);

            if expected[position] != actual {
                let divergence = format!(
                    "category {category}, index {index}\nexpected: {}\n  actual: {actual}\n{}{}",
                    expected[position],
                    compile_parity_corpus::format_plan(&plan),
                    result.text
                );
                if divergence_count < MAXIMUM_PRINTED {
                    eprintln!("{divergence}");
                }
                if divergence_count == 0 {
                    first_divergence = divergence;
                }
                divergence_count += 1;
            }
            position += 1;

            // A long run must show that it goes on.
            if position % 5000 == 0 {
                println!("{position} of {} plans compiled", expected.len());
            }
        }
    }

    assert!(
        divergence_count == 0,
        "the result of the compile changed for {divergence_count} of {} plans against {source}. The first:\n{first_divergence}",
        expected.len()
    );
}

#[test]
fn compile_given_corpus_then_matches_the_dotnet_baseline() {
    // Skipped rather than failed when the C# tree is not present, so the crate
    // stays usable on its own.
    let path = dotnet_baseline_path();
    if !path.exists() {
        eprintln!(
            "skipping: the C# baseline is not present at {}",
            path.display()
        );
        return;
    }

    let expected = read_lines(&path);

    assert_matches_baseline(&expected, PLANS_PER_CATEGORY, "the C# baseline");
}

// The hash is written out in both languages. These are the known values of
// 64-bit FNV-1a, and the C# test checks the same ones.
#[test]
fn fnv1a64_given_known_text_then_gives_known_hash() {
    assert_eq!(compile_parity_corpus::fnv1a64(""), 0xcbf2_9ce4_8422_2325);
    assert_eq!(compile_parity_corpus::fnv1a64("a"), 0xaf63_dc4c_8601_ec8c);
    assert_eq!(
        compile_parity_corpus::fnv1a64("foobar"),
        0x8594_4171_f739_67e8
    );
}

/// A one-off run against a baseline that the C# test `WriteLargeBaseline` wrote
/// to a path outside the repository. See the top of this file for the commands.
/// The number of plans of each category is the number of lines of the file
/// divided by the number of categories.
#[test]
#[ignore = "a one-off run; needs a large baseline that the C# test WriteLargeBaseline wrote"]
fn compile_given_large_corpus_then_matches_the_baseline_at_the_given_path() {
    let path = std::env::var(LARGE_BASELINE_PATH_VARIABLE).unwrap_or_else(|_| {
        panic!("set {LARGE_BASELINE_PATH_VARIABLE} to the path of the file that the C# test WriteLargeBaseline wrote")
    });

    let expected = read_lines(Path::new(&path));
    let category_count = compile_parity_corpus::CATEGORIES.len();
    assert!(
        !expected.is_empty() && expected.len().is_multiple_of(category_count),
        "{path} has {} lines, which is not a number of plans for each of {category_count} categories",
        expected.len()
    );

    let plans_per_category = expected.len() / category_count;
    assert_matches_baseline(&expected, plans_per_category, &path);
    println!(
        "{} plans of each of {category_count} categories match {path}",
        plans_per_category
    );
}
