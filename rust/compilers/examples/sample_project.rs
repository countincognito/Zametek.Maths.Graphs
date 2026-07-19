//! Extends the basic example with a real-world project: the sample plan from
//! the Zametek.ProjectPlan wiki
//! (<https://github.com/countincognito/Zametek.ProjectPlan/wiki>), hard-coded
//! below - 46 activities (including zero-duration milestones) scheduled onto
//! 15 resources with mixed None/Direct/Indirect allocation types and
//! explicit-target flags.
//!
//! The example compiles the plan with resource scheduling and prints every
//! resource's lane - idle periods marked `*** from -> to ***` - in the
//! application's display order.
//!
//! The output is asserted against the schedule published for this sample
//! (which the original .NET library computes), so a successful run is an
//! end-to-end demonstration of input/output parity.

use std::collections::HashMap;
use zametek_maths_graphs_compilers::VertexGraphCompiler;
use zametek_maths_graphs_primitives::{DependentActivity, InterActivityAllocationType, Resource};

// The sample plan's activities: (id, duration, dependencies, target resources).
// All target-resource operators are AND (the default). Zero-duration entries
// are milestones; insertion order is kept because scheduling tie-breaks
// resolve in favour of earlier activities.
const ACTIVITIES: &[(i32, i32, &[i32], &[i32])] = &[
    (1, 0, &[], &[]),       // Start milestone
    (2, 25, &[1], &[1, 2]), // Requirements & Architecture
    (3, 20, &[2], &[1, 2, 3]),
    (4, 5, &[3], &[1, 2, 3]),
    (5, 0, &[2, 4], &[1, 2, 3]), // Milestone
    (6, 15, &[7], &[4]),
    (7, 5, &[5], &[2]),
    (8, 25, &[7], &[13]),
    (9, 25, &[8], &[13]),
    (10, 10, &[5], &[14]),
    (11, 20, &[6], &[4]),
    (12, 20, &[11], &[1]),
    (13, 10, &[7], &[]),
    (14, 15, &[7], &[2]),
    (15, 20, &[7], &[]),
    (17, 5, &[10, 13, 14, 15, 44], &[]),
    (18, 5, &[10, 13, 14, 15, 22, 44], &[]),
    (19, 5, &[10, 13, 14, 15, 18, 44], &[]),
    (20, 5, &[10, 13, 14, 15, 19, 44], &[]),
    (21, 15, &[14, 44], &[]),
    (22, 5, &[10, 13, 14, 15, 44], &[]),
    (23, 10, &[17, 18, 19, 20, 22, 45], &[]),
    (24, 10, &[17, 45], &[]),
    (25, 10, &[18, 45], &[]),
    (26, 10, &[19, 45], &[]),
    (27, 10, &[20, 45], &[]),
    (28, 20, &[21, 45], &[]),
    (29, 10, &[22, 45], &[]),
    (30, 30, &[29, 46], &[]),
    (31, 15, &[25, 26, 46], &[]),
    (32, 15, &[26, 27, 30, 46], &[]),
    (33, 30, &[28, 46], &[]),
    (34, 10, &[24, 25, 30, 33, 46, 47], &[]),
    (35, 10, &[26, 31, 32, 33, 46, 47], &[]),
    (36, 10, &[33, 47], &[]),
    (38, 15, &[11, 34, 35, 36, 48], &[]),
    (39, 15, &[11, 34, 35, 36, 48], &[]),
    (40, 25, &[11, 34, 35, 36, 48], &[]),
    (41, 10, &[38, 39, 40], &[1]),
    (42, 10, &[9, 38, 39, 40], &[11, 12]),
    (43, 10, &[12, 23, 41, 42], &[1, 2, 3, 14]),
    (44, 0, &[10, 13, 14, 15], &[]),         // Milestone
    (45, 0, &[17, 18, 19, 20, 21, 22], &[]), // Milestone
    (46, 0, &[24, 25, 26, 27, 28, 29], &[]), // Milestone
    (47, 0, &[31, 32, 33], &[]),             // Milestone
    (48, 0, &[34, 35, 36], &[]),             // Milestone
];

// The sample plan's resources:
// (id, name, is explicit target, inter-activity allocation type, display order).
// Only DEV1-DEV4 accept untargeted work (they are not explicit targets).
const RESOURCES: &[(i32, &str, bool, InterActivityAllocationType, i32)] = &[
    (1, "PO", true, InterActivityAllocationType::Indirect, 2),
    (2, "ARC", true, InterActivityAllocationType::Indirect, 0),
    (3, "PM", true, InterActivityAllocationType::Indirect, 1),
    (4, "UX", true, InterActivityAllocationType::None, 20),
    (5, "DEV1", false, InterActivityAllocationType::Direct, 6),
    (6, "DEV2", false, InterActivityAllocationType::Direct, 7),
    (7, "DEV3", false, InterActivityAllocationType::Direct, 8),
    (8, "DEV4", false, InterActivityAllocationType::Direct, 9),
    (9, "DEV5", true, InterActivityAllocationType::Direct, 10),
    (10, "DEV6", true, InterActivityAllocationType::Direct, 11),
    (11, "QA1", true, InterActivityAllocationType::Indirect, 4),
    (12, "QA2", true, InterActivityAllocationType::Direct, 5),
    (13, "TE", true, InterActivityAllocationType::Direct, 19),
    (14, "CM", true, InterActivityAllocationType::Indirect, 3),
    (16, "DBA", true, InterActivityAllocationType::None, 25),
];

fn main() {
    // -- Build the plan ------------------------------------------------------

    let mut compiler: VertexGraphCompiler<i32, i32, i32> = VertexGraphCompiler::new();

    for (id, duration, dependencies, target_resources) in ACTIVITIES {
        let mut activity =
            DependentActivity::with_dependencies(*id, *duration, dependencies.iter().copied());
        activity.target_resources = target_resources.iter().copied().collect();
        assert!(compiler.add_activity(activity), "duplicate activity id");
    }

    let resources: Vec<Resource<i32, i32>> = RESOURCES
        .iter()
        .map(|(id, name, is_explicit_target, allocation_type, _)| {
            Resource::new(
                *id,
                Some(name.to_string()),
                *is_explicit_target,
                false, // none of the sample resources are inactive
                *allocation_type,
                0.0,
                0.0,
                0,
                [],
            )
        })
        .collect();

    // -- Compile with resource scheduling ------------------------------------

    let compilation = compiler
        .compile_with_resources(&resources)
        .expect("compilation must succeed");
    assert!(
        compilation.compilation_errors.is_empty(),
        "sample project must compile cleanly"
    );

    // -- Print each resource's lane, gaps marked, in display order -----------

    let display_orders: HashMap<i32, i32> = RESOURCES
        .iter()
        .map(|(id, _, _, _, order)| (*id, *order))
        .collect();
    let resource_names: HashMap<i32, &str> = RESOURCES
        .iter()
        .map(|(id, name, _, _, _)| (*id, *name))
        .collect();

    let mut schedules: Vec<_> = compilation
        .resource_schedules
        .iter()
        .filter(|s| s.resource.is_some())
        .collect();
    schedules.sort_by_key(|s| display_orders[&s.resource.as_ref().unwrap().id]);

    let mut output = String::new();
    for schedule in schedules {
        let resource_id = schedule.resource.as_ref().unwrap().id;
        output.push_str(&format!(">{}\n", resource_names[&resource_id]));

        // Walk the lane from the project start; report idle periods as gaps.
        let mut cursor = schedule.start_time;
        for scheduled in &schedule.scheduled_activities {
            if scheduled.start_time > cursor {
                output.push_str(&format!("*** {} -> {} ***\n", cursor, scheduled.start_time));
            }
            output.push_str(&format!(
                "Activity {}: {} -> {}\n",
                scheduled.id, scheduled.start_time, scheduled.finish_time
            ));
            cursor = scheduled.finish_time;
        }
        output.push('\n');
    }
    output.push_str(&format!("Project end: day {}\n", compiler.finish_time()));

    print!("{output}");

    assert_eq!(
        output, EXPECTED,
        "output must match the schedule published for the sample project"
    );
    eprintln!("Verified against the published sample schedule.");
}

/// The schedule published for the sample project, as computed by the original
/// .NET library (lanes in Zametek.ProjectPlan display order).
const EXPECTED: &str = "\
>ARC
Activity 2: 0 -> 25
Activity 3: 25 -> 45
Activity 4: 45 -> 50
Activity 7: 50 -> 55
Activity 14: 55 -> 70
*** 70 -> 205 ***
Activity 43: 205 -> 215

>PM
*** 0 -> 25 ***
Activity 3: 25 -> 45
Activity 4: 45 -> 50
*** 50 -> 205 ***
Activity 43: 205 -> 215

>PO
Activity 2: 0 -> 25
Activity 3: 25 -> 45
Activity 4: 45 -> 50
*** 50 -> 90 ***
Activity 12: 90 -> 110
*** 110 -> 195 ***
Activity 41: 195 -> 205
Activity 43: 205 -> 215

>CM
*** 0 -> 50 ***
Activity 10: 50 -> 60
*** 60 -> 205 ***
Activity 43: 205 -> 215

>QA1
*** 0 -> 195 ***
Activity 42: 195 -> 205

>QA2
*** 0 -> 195 ***
Activity 42: 195 -> 205

>DEV1
*** 0 -> 55 ***
Activity 15: 55 -> 75
Activity 22: 75 -> 80
Activity 18: 80 -> 85
Activity 19: 85 -> 90
Activity 20: 90 -> 95
Activity 28: 95 -> 115
Activity 30: 115 -> 145
Activity 32: 145 -> 160
Activity 34: 160 -> 170
Activity 40: 170 -> 195

>DEV2
*** 0 -> 55 ***
Activity 13: 55 -> 65
*** 65 -> 75 ***
Activity 21: 75 -> 90
*** 90 -> 95 ***
Activity 24: 95 -> 105
Activity 27: 105 -> 115
Activity 33: 115 -> 145
*** 145 -> 160 ***
Activity 35: 160 -> 170
Activity 38: 170 -> 185

>DEV3
*** 0 -> 75 ***
Activity 17: 75 -> 80
*** 80 -> 95 ***
Activity 25: 95 -> 105
Activity 29: 105 -> 115
Activity 31: 115 -> 130
*** 130 -> 160 ***
Activity 36: 160 -> 170
Activity 39: 170 -> 185

>DEV4
*** 0 -> 95 ***
Activity 26: 95 -> 105
Activity 23: 105 -> 115

>TE
*** 0 -> 55 ***
Activity 8: 55 -> 80
Activity 9: 80 -> 105

>UX
*** 0 -> 55 ***
Activity 6: 55 -> 70
Activity 11: 70 -> 90

Project end: day 215
";
