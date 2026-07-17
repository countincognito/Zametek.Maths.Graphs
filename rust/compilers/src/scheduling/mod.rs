//! Priority-list resource scheduling and its surrounding pipeline — the
//! counterpart of the C# `PriorityListResourceScheduler` and
//! `ResourceScheduleBuilder`.

mod schedule_builder;
mod scheduler;

pub use schedule_builder::ResourceScheduleBuilder;
pub(crate) use scheduler::{
    calculate_resource_schedules, collect_indirect_resource_schedules,
    gather_unavailable_resources, get_resource_phases_used, rebuild_aligned_resource_schedules,
    replace_with_synthetic_resources, ResourceSchedulingGraph,
};
