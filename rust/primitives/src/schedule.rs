use crate::key::Key;
use crate::packed_bool_list::PackedBoolList;
use crate::resource::Resource;

/// A snapshot of an activity placed onto a resource's timeline - the
/// counterpart of the C# `ScheduledActivity<T>`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ScheduledActivity<K: Key> {
    pub id: K,
    pub name: Option<String>,
    pub has_no_cost: bool,
    pub has_no_billing: bool,
    pub has_no_effort: bool,
    pub duration: i32,
    pub start_time: i32,
    pub finish_time: i32,
}

impl<K: Key> ScheduledActivity<K> {
    #[allow(clippy::too_many_arguments)]
    pub fn new(
        id: K,
        name: Option<String>,
        has_no_cost: bool,
        has_no_billing: bool,
        has_no_effort: bool,
        duration: i32,
        start_time: i32,
        finish_time: i32,
    ) -> Self {
        Self {
            id,
            name,
            has_no_cost,
            has_no_billing,
            has_no_effort,
            duration,
            start_time,
            finish_time,
        }
    }
}

/// The finished schedule for one resource (or for an unmapped, infinite-resources
/// lane when `resource` is `None`) - the counterpart of the C#
/// `ResourceSchedule<T, TResourceId, TWorkStreamId>`.
///
/// The allocation streams hold one flag per time unit from zero to
/// `finish_time`. They are stored bit-packed rather than as `Vec<bool>`: they
/// are the largest thing a compilation retains and they scale as
/// (horizon x resources), so a byte per flag would be seven eighths padding.
/// See [`PackedBoolList`] for the reasoning and the layout; it compares equal to
/// a `Vec<bool>` of the same flags, so callers can check a stream against a
/// plain boolean list directly.
#[derive(Debug, Clone, PartialEq)]
pub struct ResourceSchedule<K: Key, R: Key, W: Key> {
    pub resource: Option<Resource<R, W>>,
    pub scheduled_activities: Vec<ScheduledActivity<K>>,
    pub start_time: i32,
    pub finish_time: i32,
    pub resource_allocation: PackedBoolList,
    pub cost_allocation: PackedBoolList,
    pub billing_allocation: PackedBoolList,
    pub effort_allocation: PackedBoolList,
    pub activity_allocation: PackedBoolList,
}
