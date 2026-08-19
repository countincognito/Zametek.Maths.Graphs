//! Port of `ArrowCriticalPathEquivalenceTests.cs`.
//!
//! The safety net for any change to the arrow critical-path engine.
//!
//! The engine computes the earliest and latest finish times of every event, and
//! from those the start, finish and slack values of every activity. Those values
//! decide what the compiled arrow graph reports as critical, so a faster
//! calculation that produces different numbers is a behaviour change, not an
//! optimisation. These tests pin the current output exactly, over the same
//! corpus the vertex side uses.
//!
//! If a change makes one of these fail, the change is not equivalent. Regenerate
//! the baseline only when the output is *intended* to change, using the
//! `regenerate_baseline` test below (marked `#[ignore]` so it never runs by
//! accident):
//!
//! ```text
//! cargo test --test arrow_critical_path_equivalence_tests -- --ignored regenerate_baseline
//! ```
//!
//! As with the priority-list corpus, the shapes and the ID generator seeds match
//! the C# original exactly, so this baseline is byte-identical to the C# one at
//! `dotnet/test/.../Compilers/TestFiles/ArrowCriticalPathBaseline.txt`, and the
//! `matches_the_dotnet_baseline` test checks that.

mod arrow_critical_path_corpus;
mod corpus_shapes;

use std::fmt::Write as _;
use std::fs;
use std::path::PathBuf;

const BASELINE_RELATIVE_PATH: &str = "tests/testfiles/arrow_critical_path_baseline.txt";
const DOTNET_BASELINE_RELATIVE_PATH: &str =
    "../../dotnet/test/Zametek.Maths.Graphs.Compilers.Tests/Compilers/TestFiles/ArrowCriticalPathBaseline.txt";

fn baseline_path() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join(BASELINE_RELATIVE_PATH)
}

fn dotnet_baseline_path() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join(DOTNET_BASELINE_RELATIVE_PATH)
}

/// An event's ID with its earliest and latest finish times.
type EventValues = (i32, Option<i32>, Option<i32>);
/// An activity's ID with its earliest start, latest finish and free slack.
type ActivityValues = (i32, Option<i32>, Option<i32>, Option<i32>);

fn show(value: Option<i32>) -> String {
    match value {
        Some(value) => value.to_string(),
        None => "-".to_string(),
    }
}

/// Renders every value the engine is responsible for: each event's earliest and
/// latest finish time, then each activity's earliest start, latest finish and
/// free slack. Ordered by ID so the line is stable regardless of iteration order.
fn format_case(case: &mut arrow_critical_path_corpus::Case) -> String {
    case.graph_builder
        .calculate_critical_path()
        .unwrap_or_else(|error| panic!("case {} failed: {}", case.name, error));

    let mut events: Vec<EventValues> = case
        .graph_builder
        .events()
        .map(|x| (x.id(), x.earliest_finish_time, x.latest_finish_time))
        .collect();
    events.sort_by_key(|x| x.0);

    let mut activities: Vec<ActivityValues> = case
        .graph_builder
        .activities()
        .map(|x| {
            (
                x.id(),
                x.earliest_start_time,
                x.latest_finish_time,
                x.free_slack,
            )
        })
        .collect();
    activities.sort_by_key(|x| x.0);

    let mut output = String::new();
    let _ = write!(output, "{}: events ", case.name);
    let _ = write!(
        output,
        "{}",
        events
            .iter()
            .map(|(id, eft, lft)| format!("{}={}/{}", id, show(*eft), show(*lft)))
            .collect::<Vec<_>>()
            .join(",")
    );
    let _ = write!(output, " activities ");
    let _ = write!(
        output,
        "{}",
        activities
            .iter()
            .map(|(id, est, lft, fs)| format!("{}={}/{}/{}", id, show(*est), show(*lft), show(*fs)))
            .collect::<Vec<_>>()
            .join(",")
    );
    output
}

fn build_current_lines() -> Vec<String> {
    arrow_critical_path_corpus::generate()
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
        "the arrow critical path changed for {} of {} corpus cases against {source}:\n{}",
        divergences.len(),
        expected.len(),
        divergences
            .iter()
            .take(3)
            .cloned()
            .collect::<Vec<_>>()
            .join("\n")
    );
}

#[test]
fn arrow_critical_path_given_corpus_then_matches_committed_baseline() {
    let expected = read_lines(&baseline_path());
    let actual = build_current_lines();

    assert_lines_match(&expected, &actual, "the committed baseline");
}

#[test]
fn arrow_critical_path_given_corpus_then_matches_the_dotnet_baseline() {
    // The corpus generator and the ID generator seeds are ports of the C# ones,
    // so both languages build the same arrow graphs; if their computed values
    // agree, the two engines agree on every one of these shapes. Skipped rather
    // than failed when the C# tree is not present, so the crate stays usable on
    // its own.
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
fn arrow_critical_path_given_corpus_then_is_independent_of_processing_order() {
    // shuffle_processing_order randomises the order the engine walks its work
    // lists. The computed values must not depend on it - if they do, any rewrite
    // of that engine is free to change the results silently.
    //
    // Each variant gets its own builder, and deliberately so: calculating the
    // critical path begins by removing redundant edges, which is a structural
    // change, so calling it twice on one builder is not the same as calling it
    // twice on the same graph. The corpus is deterministic, so two generations
    // give identical graphs to compare.
    let mut ordered_cases = arrow_critical_path_corpus::generate();
    let mut shuffled_cases = arrow_critical_path_corpus::generate();

    for (ordered_case, shuffled_case) in ordered_cases.iter_mut().zip(shuffled_cases.iter_mut()) {
        let ordered = format_case(ordered_case);

        shuffled_case.graph_builder.shuffle_processing_order = true;
        let shuffled = format_case(shuffled_case);

        assert_eq!(shuffled, ordered, "case {}", ordered_case.name);
    }
}

#[test]
fn arrow_critical_path_given_corpus_then_every_event_and_activity_has_values() {
    // An invariant the calculation must preserve regardless of traversal: it
    // reaches everything. A walk that silently skipped part of the graph would
    // leave values unset rather than wrong, which a value comparison alone could
    // miss if the baseline were ever regenerated against it.
    for case in arrow_critical_path_corpus::generate().iter_mut() {
        case.graph_builder
            .calculate_critical_path()
            .unwrap_or_else(|error| panic!("case {} failed: {}", case.name, error));

        assert!(
            case.graph_builder
                .events()
                .all(|x| x.earliest_finish_time.is_some() && x.latest_finish_time.is_some()),
            "case {} left an event without times",
            case.name
        );
        assert!(
            case.graph_builder
                .activities()
                .all(|x| x.earliest_start_time.is_some()
                    && x.latest_finish_time.is_some()
                    && x.free_slack.is_some()),
            "case {} left an activity without values",
            case.name
        );
    }
}

#[test]
#[ignore = "run deliberately, only when the arrow critical-path output is intended to change"]
fn regenerate_baseline() {
    let path = baseline_path();
    fs::create_dir_all(path.parent().expect("baseline has a parent directory"))
        .expect("could not create the baseline directory");
    let mut contents = build_current_lines().join("\n");
    contents.push('\n');
    fs::write(&path, contents).expect("could not write the baseline");
    assert!(path.exists());
}
