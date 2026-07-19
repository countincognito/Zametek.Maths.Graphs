use crate::enums::InterActivityAllocationType;
use crate::key::Key;
use indexmap::IndexSet;

/// A schedulable resource - the counterpart of the C#
/// `Resource<T, TWorkStreamId>`.
#[derive(Debug, Clone, PartialEq)]
pub struct Resource<R: Key, W: Key> {
    pub id: R,
    pub name: Option<String>,
    /// Whether the resource only accepts activities that explicitly target it.
    pub is_explicit_target: bool,
    /// Whether the resource is disabled.
    pub is_inactive: bool,
    pub inter_activity_allocation_type: InterActivityAllocationType,
    pub unit_cost: f64,
    pub unit_billing: f64,
    /// The order in which the scheduler considers resources (ascending).
    pub allocation_order: i32,
    /// The work-stream phases this resource participates in.
    pub inter_activity_phases: IndexSet<W>,
}

impl<R: Key, W: Key> Resource<R, W> {
    #[allow(clippy::too_many_arguments)]
    pub fn new(
        id: R,
        name: Option<String>,
        is_explicit_target: bool,
        is_inactive: bool,
        inter_activity_allocation_type: InterActivityAllocationType,
        unit_cost: f64,
        unit_billing: f64,
        allocation_order: i32,
        inter_activity_phases: impl IntoIterator<Item = W>,
    ) -> Self {
        Self {
            id,
            name,
            is_explicit_target,
            is_inactive,
            inter_activity_allocation_type,
            unit_cost,
            unit_billing,
            allocation_order,
            inter_activity_phases: inter_activity_phases.into_iter().collect(),
        }
    }
}
