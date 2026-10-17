//! Port of `SimulationBaselineTests.cs`.
//!
//! The numbers that the Rust port must give, bit for bit. The simulation
//! promises one thing across languages: for one seed, both give the same draws,
//! and so the same iterations. The file `SimulationBaseline.txt` of the .NET
//! tests holds the exact bit patterns of the logarithm, the exponential, the
//! generator, every sampler and the chunk seeds, and the iterations of four
//! whole simulations. This test computes the same lines and compares them with
//! the file.
//!
//! The .NET file is the only copy. If this test fails, the two ports differ in
//! a number that they must share: find out which one is wrong. Regenerate the
//! file (with the `RegenerateBaseline` test of the .NET tests) only when the
//! numbers are meant to change, and change both ports.

#![allow(clippy::excessive_precision, clippy::approx_constant)]

mod simulation_plans;

use std::fs;
use std::path::PathBuf;
use zametek_maths_graphs_compilers::primitives::{
    CriticalPathLink, DurationDistribution, DurationEstimate, SimulationSettings,
};
use zametek_maths_graphs_compilers::simulation::baseline_support::{
    chunk_seed, exp, ln, round_to_whole_units, Random,
};
use zametek_maths_graphs_compilers::ScheduleSimulator;

const DOTNET_BASELINE_RELATIVE_PATH: &str =
    "../../dotnet/test/Zametek.Maths.Graphs.Compilers.Tests/Compilers/TestFiles/SimulationBaseline.txt";

fn dotnet_baseline_path() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join(DOTNET_BASELINE_RELATIVE_PATH)
}

fn hex(value: f64) -> String {
    format!("{:016X}", value.to_bits())
}

fn hex_u64(value: u64) -> String {
    format!("{value:016X}")
}

fn line(name: &str, values: impl IntoIterator<Item = String>) -> String {
    format!(
        "{name}: {}",
        values.into_iter().collect::<Vec<_>>().join(",")
    )
}

fn ln_arguments() -> Vec<f64> {
    let mut values = vec![
        1.0,
        2.0,
        0.5,
        10.0,
        1e-10,
        1e10,
        1e-300,
        1e300,
        4.9406564584124654e-324,
        2.2250738585072009e-308,
        1.4142135623730951,
        0.7071067811865476,
    ];
    for i in 1..=120 {
        values.push(f64::from(i) * 0.0137);
    }
    let mut power = 9.5367431640625e-7;
    for i in 0..40 {
        values.push(power * (1.0 + (f64::from(i) * 0.021)));
        power *= 2.0;
    }
    values
}

fn exp_arguments() -> Vec<f64> {
    let mut values = vec![0.0, 1.0, -1.0, 0.5, 700.0, -700.0, 709.0, -745.0];
    for i in -100..=100 {
        values.push(f64::from(i) * 0.731);
    }
    values
}

fn simulation_lines() -> Vec<String> {
    let cases = [
        (
            "reference-15",
            simulation_plans::reference_sample(),
            false,
            DurationDistribution::BetaPert,
            2026u64,
            200usize,
        ),
        (
            "reference-15",
            simulation_plans::reference_sample(),
            false,
            DurationDistribution::Triangular,
            7,
            130,
        ),
        (
            "layered-40",
            simulation_plans::layered(40, 5, 4),
            true,
            DurationDistribution::BetaPert,
            99,
            130,
        ),
        (
            "layered-40",
            simulation_plans::layered(40, 5, 4),
            true,
            DurationDistribution::ModifiedBetaPert,
            100,
            70,
        ),
    ];

    let mut lines = Vec::new();
    for (name, plan, use_resources, distribution, seed, iterations) in cases {
        let simulator = ScheduleSimulator::new(
            &plan.activities(),
            &plan.resources(use_resources),
            &[],
            &plan.estimates(),
        )
        .unwrap();
        let result = simulator
            .run(&SimulationSettings {
                iterations,
                seed,
                distribution,
                max_degree_of_parallelism: 1,
                ..SimulationSettings::default()
            })
            .unwrap();

        let key = format!("simulation {name} {distribution} seed={seed} iterations={iterations}");
        lines.push(line(
            &format!("{key} planned"),
            [simulator.planned_finish_time().to_string()],
        ));
        lines.push(line(
            &format!("{key} finish"),
            result.finish_times().iter().map(|x| x.to_string()),
        ));
        lines.push(line(
            &format!("{key} critical"),
            (0..result.activity_ids().len()).map(|a| {
                (0..iterations)
                    .filter(|i| result.is_critical(*i, a).unwrap())
                    .count()
                    .to_string()
            }),
        ));
        lines.push(line(
            &format!("{key} paths"),
            result.critical_paths().iter().map(|path| {
                path.steps
                    .iter()
                    .map(|s| {
                        let mark = if s.link == CriticalPathLink::Resource {
                            "~"
                        } else {
                            ""
                        };
                        format!("{mark}{}", s.activity_id)
                    })
                    .collect::<Vec<_>>()
                    .join(">")
            }),
        ));
        lines.push(line(
            &format!("{key} pathsof"),
            (0..iterations).map(|i| {
                let indexes: Vec<String> = result
                    .critical_path_indexes(i)
                    .unwrap()
                    .iter()
                    .map(|x| x.to_string())
                    .collect();
                let incomplete = if result.is_critical_path_list_complete(i).unwrap() {
                    ""
                } else {
                    "!"
                };
                format!("{}{incomplete}", indexes.join("+"))
            }),
        ));
    }
    lines
}

fn build_current_lines() -> Vec<String> {
    let mut lines = Vec::new();
    lines.push(line(
        "ln",
        ln_arguments()
            .into_iter()
            .map(|x| format!("{}={}", hex(x), hex(ln(x)))),
    ));
    lines.push(line(
        "exp",
        exp_arguments()
            .into_iter()
            .map(|x| format!("{}={}", hex(x), hex(exp(x)))),
    ));

    for seed in [0u64, 1, 42, 0xDEAD_BEEF_CAFE_F00D, u64::MAX] {
        let mut random = Random::new(seed);
        lines.push(line(
            &format!("uint64 seed={}", hex_u64(seed)),
            (0..8).map(|_| hex_u64(random.next_u64())),
        ));
        let mut random = Random::new(seed);
        lines.push(line(
            &format!("double seed={}", hex_u64(seed)),
            (0..8).map(|_| hex(random.next_double())),
        ));
        let mut random = Random::new(seed);
        lines.push(line(
            &format!("gaussian seed={}", hex_u64(seed)),
            (0..8).map(|_| hex(random.next_gaussian())),
        ));
        lines.push(line(
            &format!("chunkseed seed={}", hex_u64(seed)),
            (0..8).map(|x| hex_u64(chunk_seed(seed, x))),
        ));
    }

    for shape in [0.5, 0.667, 1.0, 2.5, 9.0] {
        let mut random = Random::new(7);
        lines.push(line(
            &format!("gamma shape={}", hex(shape)),
            (0..8).map(|_| hex(random.next_gamma(shape))),
        ));
    }

    for (alpha, beta) in [(0.667, 2.0), (1.0, 1.0), (3.0, 5.0)] {
        let mut random = Random::new(8);
        lines.push(line(
            &format!("beta alpha={} beta={}", hex(alpha), hex(beta)),
            (0..8).map(|_| hex(random.next_beta(alpha, beta))),
        ));
    }

    let estimates = [
        DurationEstimate::new(2, 4, 6).unwrap(),
        DurationEstimate::new(4, 5, 12).unwrap(),
        DurationEstimate::new(0, 3, 10).unwrap(),
        DurationEstimate::new(5, 5, 9).unwrap(),
        DurationEstimate::new(7, 9, 9).unwrap(),
        DurationEstimate::new(0, 0, 5).unwrap(),
        DurationEstimate::fixed(3).unwrap(),
    ];
    for distribution in [
        DurationDistribution::BetaPert,
        DurationDistribution::ModifiedBetaPert,
        DurationDistribution::Triangular,
        DurationDistribution::Normal,
        DurationDistribution::Uniform,
    ] {
        for estimate in estimates {
            let mut random = Random::new(9);
            let draws: Vec<f64> = (0..8)
                .map(|_| random.sample(distribution, estimate, 4.0))
                .collect();
            lines.push(line(
                &format!("sample {distribution} {estimate}"),
                draws
                    .into_iter()
                    .map(|x| format!("{}={}", hex(x), round_to_whole_units(x, estimate))),
            ));
        }
    }

    lines.extend(simulation_lines());
    lines
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

#[test]
fn baseline_given_the_dotnet_file_then_every_number_is_the_same_bit_for_bit() {
    let expected = read_lines(&dotnet_baseline_path());
    let actual = build_current_lines();

    assert_eq!(
        actual.len(),
        expected.len(),
        "the baseline has changed size; it needs regenerating deliberately"
    );

    let divergences: Vec<String> = expected
        .iter()
        .zip(actual.iter())
        .filter(|(e, a)| e != a)
        .map(|(e, a)| {
            let shorten = |x: &String| x.chars().take(400).collect::<String>();
            format!("expected: {}\n  actual: {}", shorten(e), shorten(a))
        })
        .collect();

    assert!(
        divergences.is_empty(),
        "{} of {} lines of the baseline differ:\n{}",
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
