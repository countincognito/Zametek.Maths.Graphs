//! The plans of the simulation tests, built from numbers only: the sample of the
//! reference tool, and a layered plan of any size. They are value-for-value
//! ports of `SpikePlan.ReferenceSample` and `SpikePlan.Layered` in the .NET
//! tests, so that both languages simulate the same plans.

#![allow(dead_code)]

use std::collections::HashMap;
use zametek_maths_graphs_compilers::primitives::{
    DependentActivity, DurationEstimate, InterActivityAllocationType, Resource,
};

pub type Activity = DependentActivity<i32, i32, i32>;

pub struct Record {
    pub id: i32,
    /// The most likely duration.
    pub duration: i32,
    pub optimistic: i32,
    pub pessimistic: i32,
    pub dependencies: Vec<i32>,
}

pub struct Plan {
    pub records: Vec<Record>,
    pub resource_count: i32,
}

impl Record {
    pub fn fixed(id: i32, duration: i32, dependencies: &[i32]) -> Self {
        Self {
            id,
            duration,
            optimistic: duration,
            pessimistic: duration,
            dependencies: dependencies.to_vec(),
        }
    }

    pub fn estimate(&self) -> DurationEstimate {
        DurationEstimate::new(self.optimistic, self.duration, self.pessimistic).unwrap()
    }
}

impl Plan {
    /// The activities, each with the duration that the plan schedules (the
    /// expected duration).
    pub fn activities(&self) -> Vec<Activity> {
        self.records
            .iter()
            .map(|x| {
                Activity::with_dependencies(
                    x.id,
                    x.estimate().expected_duration(),
                    x.dependencies.iter().copied(),
                )
            })
            .collect()
    }

    /// The activities, each with its most likely duration.
    pub fn most_likely_activities(&self) -> Vec<Activity> {
        self.records
            .iter()
            .map(|x| Activity::with_dependencies(x.id, x.duration, x.dependencies.iter().copied()))
            .collect()
    }

    pub fn estimates(&self) -> HashMap<i32, DurationEstimate> {
        self.records.iter().map(|x| (x.id, x.estimate())).collect()
    }

    pub fn resources(&self, use_resources: bool) -> Vec<Resource<i32, i32>> {
        if !use_resources {
            return Vec::new();
        }
        (1..=self.resource_count)
            .map(|id| {
                Resource::new(
                    id,
                    Some(String::new()),
                    false,
                    false,
                    InterActivityAllocationType::None,
                    1.0,
                    1.0,
                    0,
                    Vec::<i32>::new(),
                )
            })
            .collect()
    }

    /// A plan of fixed durations, from (id, duration, dependencies).
    pub fn fixed(activities: &[(i32, i32, &[i32])]) -> Self {
        Self {
            records: activities
                .iter()
                .map(|(id, duration, dependencies)| Record::fixed(*id, *duration, dependencies))
                .collect(),
            resource_count: 0,
        }
    }
}

/// The sample of the reference tool: Start, the activities A to O, and Finish.
/// The IDs are 1 (Start), 2 to 16 (A to O) and 17 (Finish). No resources.
pub fn reference_sample() -> Plan {
    let letters = [
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O",
    ];
    let estimates: [[i32; 3]; 15] = [
        [2, 4, 6],
        [4, 5, 12],
        [3, 5, 7],
        [7, 10, 13],
        [11, 12, 13],
        [5, 6, 7],
        [9, 10, 11],
        [5, 7, 9],
        [7, 9, 11],
        [1, 5, 9],
        [15, 17, 19],
        [6, 8, 10],
        [9, 10, 17],
        [12, 14, 16],
        [16, 17, 18],
    ];
    let predecessors = [
        "Start", "Start", "B", "A", "A", "B,D", "C", "C,F", "D", "E", "G", "H", "I,J", "G", "L,M",
    ];

    let id_of = |name: &str| -> i32 {
        match name {
            "Start" => 1,
            "Finish" => 17,
            _ => letters.iter().position(|x| *x == name).unwrap() as i32 + 2,
        }
    };

    let mut records = vec![Record::fixed(1, 0, &[])];
    for (i, letter) in letters.iter().enumerate() {
        records.push(Record {
            id: id_of(letter),
            duration: estimates[i][1],
            optimistic: estimates[i][0],
            pessimistic: estimates[i][2],
            dependencies: predecessors[i].split(',').map(id_of).collect(),
        });
    }
    records.push(Record::fixed(17, 0, &["K", "N", "O"].map(id_of)));
    Plan {
        records,
        resource_count: 0,
    }
}

/// The same layered graph that the scaling tests use. The resources are not
/// explicit targets and the activities name none. The optimistic and the
/// pessimistic durations are 0.75 and 1.6 times the most likely duration, rounded
/// half away from zero.
pub fn layered(size: i32, layers: i32, resource_count: i32) -> Plan {
    let per_layer = (size / layers).max(1);
    let mut records = Vec::new();
    for id in 1..=size {
        let layer = (id - 1) / per_layer;
        let duration = 1 + (id % 9);
        let mut dependencies = Vec::new();
        if layer > 0 {
            let previous_start = ((layer - 1) * per_layer) + 1;
            let previous_end = (layer * per_layer).min(size);
            if previous_end >= previous_start {
                let span = previous_end - previous_start + 1;
                dependencies.push(previous_start + ((id * 7) % span));
                dependencies.push(previous_start + ((id * 13) % span));
            }
        }
        records.push(Record {
            id,
            duration,
            optimistic: (f64::from(duration) * 0.75).round() as i32,
            pessimistic: (f64::from(duration) * 1.6).round() as i32,
            dependencies,
        });
    }
    Plan {
        records,
        resource_count,
    }
}
