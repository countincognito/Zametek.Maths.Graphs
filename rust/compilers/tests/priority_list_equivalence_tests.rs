//! Port of `PriorityListEquivalenceTests.cs`.
//!
//! The safety net for any future change to the priority-list calculation or to
//! the critical-path engine beneath it.
//!
//! The priority list decides the order in which activities are offered
//! resources, so it determines the final schedule. A faster calculation that
//! produces a different list is a behaviour change, not an optimisation - and
//! one that would be easy to miss, because most graphs still compile
//! successfully afterwards, just to a different schedule. These tests pin the
//! current output exactly, over a corpus chosen to stress depth, width, density
//! and (above all) ties.
//!
//! If a change makes one of these fail, the change is not equivalent. Regenerate
//! the baseline only when the output is *intended* to change, using the
//! `regenerate_baseline` test below (marked `#[ignore]` so it never runs by
//! accident):
//!
//! ```text
//! cargo test --test priority_list_equivalence_tests -- --ignored regenerate_baseline
//! ```
//!
//! The corpus is a value-for-value port of the C# generator, so the graphs are
//! identical in both languages and this baseline is byte-identical to the C#
//! one at `dotnet/test/.../Compilers/TestFiles/PriorityListBaseline.txt`. The
//! `matches_the_dotnet_baseline` test below checks exactly that, which turns the
//! oracle into a cross-language parity check as well as a regression net.

mod priority_list_corpus;

use std::fs;
use std::path::PathBuf;

const BASELINE_RELATIVE_PATH: &str = "tests/testfiles/priority_list_baseline.txt";
const DOTNET_BASELINE_RELATIVE_PATH: &str =
    "../../dotnet/test/Zametek.Maths.Graphs.Compilers.Tests/Compilers/TestFiles/PriorityListBaseline.txt";

fn baseline_path() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join(BASELINE_RELATIVE_PATH)
}

fn dotnet_baseline_path() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join(DOTNET_BASELINE_RELATIVE_PATH)
}

fn format_case(case: &mut priority_list_corpus::Case) -> String {
    let priority_list = case
        .graph_builder
        .calculate_critical_path_priority_list()
        .unwrap_or_else(|error| panic!("case {} failed: {}", case.name, error));
    let joined: Vec<String> = priority_list.iter().map(|x| x.to_string()).collect();
    format!("{}: {}", case.name, joined.join(","))
}

fn build_current_lines() -> Vec<String> {
    priority_list_corpus::generate()
        .iter_mut()
        .map(format_case)
        .collect()
}

fn read_lines(path: &PathBuf) -> Vec<String> {
    let contents = fs::read_to_string(path)
        .unwrap_or_else(|error| panic!("could not read {}: {error}", path.display()));
    contents
        .lines()
        .map(|x| x.trim_end_matches('\r').to_string())
        .filter(|x| !x.trim().is_empty())
        .collect()
}

/// Compares case by case so a failure names the shape that diverged rather than
/// just reporting that two large lists differ.
fn assert_lines_match(expected: &[String], actual: &[String], source: &str) {
    assert_eq!(
        actual.len(),
        expected.len(),
        "the corpus has changed size against {source}; the baseline needs regenerating deliberately"
    );

    let divergences: Vec<String> = expected
        .iter()
        .zip(actual.iter())
        .filter(|(expected_line, actual_line)| expected_line != actual_line)
        .map(|(expected_line, actual_line)| {
            format!("expected: {expected_line}\n  actual: {actual_line}")
        })
        .collect();

    assert!(
        divergences.is_empty(),
        "the priority list changed for {} of {} corpus cases against {source}:\n{}",
        divergences.len(),
        expected.len(),
        divergences
            .iter()
            .take(5)
            .cloned()
            .collect::<Vec<_>>()
            .join("\n")
    );
}

#[test]
fn priority_list_given_corpus_then_matches_committed_baseline() {
    let expected = read_lines(&baseline_path());
    let actual = build_current_lines();

    assert_lines_match(&expected, &actual, "the committed baseline");
}

#[test]
fn priority_list_given_corpus_then_matches_the_dotnet_baseline() {
    // The corpus generator is a value-for-value port, so both languages build
    // the same graphs; if their priority lists agree, the two implementations
    // agree on every one of these shapes. Skipped rather than failed when the
    // C# tree is not present, so the crate stays usable on its own.
    let path = dotnet_baseline_path();
    if !path.exists() {
        eprintln!(
            "skipping: the C# baseline is not present at {}",
            path.display()
        );
        return;
    }

    let expected = read_lines(&path);
    let actual = build_current_lines();

    assert_lines_match(&expected, &actual, "the C# baseline");
}

#[test]
fn priority_list_given_corpus_then_is_independent_of_processing_order() {
    // shuffle_processing_order randomises the order the critical-path engine
    // walks its work lists. The priority list must not depend on it - if it
    // does, any rewrite of that engine is free to change the schedule silently.
    for case in priority_list_corpus::generate().iter_mut() {
        let ordered = case
            .graph_builder
            .calculate_critical_path_priority_list()
            .unwrap();

        case.graph_builder.shuffle_processing_order = true;
        let shuffled = case
            .graph_builder
            .calculate_critical_path_priority_list()
            .unwrap();
        case.graph_builder.shuffle_processing_order = false;

        assert_eq!(shuffled, ordered, "case {}", case.name);
    }
}

#[test]
fn priority_list_given_corpus_then_every_non_dummy_activity_appears_exactly_once() {
    // An invariant the calculation must preserve regardless of ordering: every
    // activity with a non-zero duration is scheduled, and none is scheduled twice.
    for case in priority_list_corpus::generate().iter_mut() {
        let mut expected: Vec<i32> = case
            .graph_builder
            .activities()
            .filter(|x| !x.is_dummy())
            .map(|x| x.id())
            .collect();
        expected.sort_unstable();

        let priority_list = case
            .graph_builder
            .calculate_critical_path_priority_list()
            .unwrap();

        let mut sorted = priority_list.clone();
        sorted.sort_unstable();
        sorted.dedup();
        assert_eq!(
            sorted.len(),
            priority_list.len(),
            "case {} has duplicates",
            case.name
        );
        assert_eq!(sorted, expected, "case {}", case.name);
    }
}

/// Explicit, because it takes far longer than a normal test:
///
/// ```text
/// cargo test --release --test priority_list_equivalence_tests -- --ignored --nocapture measure_priority_list_scaling
/// ```
///
/// Compare the result against the tables in `dotnet/docs/TODO.md`.
#[test]
#[ignore = "measurement rather than a test; run deliberately"]
fn measure_priority_list_scaling() {
    println!("Scaling with activity count (12 layers):");
    println!("{:>12} {:>12}", "activities", "time");
    for size in [250, 500, 1_000, 2_000] {
        let elapsed = measure_one(size, 12);
        println!("{:>12} {:>12}", size, format!("{}ms", elapsed.as_millis()));
    }

    println!();
    println!("Scaling with depth (1,500 activities):");
    println!("{:>12} {:>12}", "layers", "time");
    for layers in [10, 30, 60, 120, 240] {
        let elapsed = measure_one(1_500, layers);
        println!(
            "{:>12} {:>12}",
            layers,
            format!("{}ms", elapsed.as_millis())
        );
    }
}

fn measure_one(size: i32, layers: i32) -> std::time::Duration {
    use indexmap::IndexSet;
    use zametek_maths_graphs_compilers::{NextIdGenerator, VertexGraphBuilder};
    use zametek_maths_graphs_primitives::DependentActivity;

    let mut graph_builder: VertexGraphBuilder<i32, i32, i32> =
        VertexGraphBuilder::new(NextIdGenerator::new(0));
    let per_layer = (size / layers).max(1);
    for id in 1..=size {
        let layer = (id - 1) / per_layer;
        let mut dependencies: IndexSet<i32> = IndexSet::new();
        if layer > 0 {
            let previous_start = ((layer - 1) * per_layer) + 1;
            let previous_end = (layer * per_layer).min(size);
            if previous_end >= previous_start {
                let span = previous_end - previous_start + 1;
                dependencies.insert(previous_start + ((id * 7) % span));
                dependencies.insert(previous_start + ((id * 13) % span));
            }
        }
        graph_builder
            .add_activity_with_dependencies(DependentActivity::new(id, 1 + (id % 9)), dependencies);
    }

    let started = std::time::Instant::now();
    graph_builder
        .calculate_critical_path_priority_list()
        .expect("priority list must calculate");
    started.elapsed()
}

#[test]
#[ignore = "run deliberately, only when the priority-list output is intended to change"]
fn regenerate_baseline() {
    let path = baseline_path();
    fs::create_dir_all(path.parent().expect("baseline has a parent directory"))
        .expect("could not create the baseline directory");
    let mut contents = build_current_lines().join("\n");
    contents.push('\n');
    fs::write(&path, contents).expect("could not write the baseline");
    assert!(path.exists());
}
