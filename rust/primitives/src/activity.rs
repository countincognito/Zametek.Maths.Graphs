use crate::edge::HasId;
use crate::enums::LogicalOperator;
use crate::key::Key;
use indexmap::IndexSet;
use std::ops::{Deref, DerefMut};

/// An activity in a project-planning graph - the counterpart of the C#
/// `Activity<T, TResourceId, TWorkStreamId>`.
///
/// Times are `Option<i32>` where the C# uses `int?`. The derived values
/// (total slack, latest start time, etc.) are computed exactly as in C#.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Activity<K: Key, R: Key, W: Key> {
    id: K,
    pub name: Option<String>,
    pub notes: Option<String>,
    pub target_work_streams: IndexSet<W>,
    pub target_resources: IndexSet<R>,
    pub target_resource_operator: LogicalOperator,
    pub allocated_to_resources: IndexSet<R>,
    can_be_removed: bool,
    pub has_no_cost: bool,
    pub has_no_billing: bool,
    pub has_no_effort: bool,
    pub duration: i32,
    pub free_slack: Option<i32>,
    pub earliest_start_time: Option<i32>,
    pub latest_finish_time: Option<i32>,
    pub minimum_free_slack: Option<i32>,
    pub minimum_earliest_start_time: Option<i32>,
    pub maximum_latest_finish_time: Option<i32>,
}

impl<K: Key, R: Key, W: Key> Activity<K, R, W> {
    /// Creates an activity with the given ID and duration.
    pub fn new(id: K, duration: i32) -> Self {
        Self {
            id,
            name: None,
            notes: None,
            target_work_streams: IndexSet::new(),
            target_resources: IndexSet::new(),
            target_resource_operator: LogicalOperator::default(),
            allocated_to_resources: IndexSet::new(),
            can_be_removed: false,
            has_no_cost: false,
            has_no_billing: false,
            has_no_effort: false,
            duration,
            free_slack: None,
            earliest_start_time: None,
            latest_finish_time: None,
            minimum_free_slack: None,
            minimum_earliest_start_time: None,
            maximum_latest_finish_time: None,
        }
    }

    /// Creates an activity with the given ID, duration and removability flag.
    pub fn new_removable(id: K, duration: i32, can_be_removed: bool) -> Self {
        let mut activity = Self::new(id, duration);
        activity.can_be_removed = can_be_removed;
        activity
    }

    pub fn id(&self) -> K {
        self.id
    }

    /// Whether the activity is a zero-duration placeholder.
    pub fn is_dummy(&self) -> bool {
        self.duration <= 0
    }

    /// Whether the activity may be removed during clean-up passes.
    pub fn can_be_removed(&self) -> bool {
        self.can_be_removed
    }

    /// Marks the activity as not removable.
    pub fn set_as_read_only(&mut self) {
        self.can_be_removed = false;
    }

    /// Marks the activity as removable.
    pub fn set_as_removable(&mut self) {
        self.can_be_removed = true;
    }

    /// LatestFinishTime - EarliestFinishTime, when both are known.
    pub fn total_slack(&self) -> Option<i32> {
        match (self.latest_finish_time, self.earliest_finish_time()) {
            (Some(lft), Some(eft)) => Some(lft - eft),
            _ => None,
        }
    }

    /// TotalSlack - FreeSlack, when both are known.
    pub fn interfering_slack(&self) -> Option<i32> {
        match (self.total_slack(), self.free_slack) {
            (Some(total), Some(free)) => Some(total - free),
            _ => None,
        }
    }

    /// Whether the activity lies on the critical path (total slack <= 0).
    pub fn is_critical(&self) -> bool {
        matches!(self.total_slack(), Some(total) if total <= 0)
    }

    /// LatestFinishTime - Duration, when known.
    pub fn latest_start_time(&self) -> Option<i32> {
        self.latest_finish_time.map(|lft| lft - self.duration)
    }

    /// EarliestStartTime + Duration, when known.
    pub fn earliest_finish_time(&self) -> Option<i32> {
        self.earliest_start_time.map(|est| est + self.duration)
    }
}

impl<K: Key, R: Key, W: Key> HasId<K> for Activity<K, R, W> {
    fn id(&self) -> K {
        self.id
    }

    fn can_be_removed(&self) -> bool {
        self.can_be_removed
    }
}

/// An activity plus its dependency bookkeeping - the counterpart of the C#
/// `DependentActivity<T, TResourceId, TWorkStreamId>`. This is the input type
/// the graph compilers consume.
///
/// Dereferences to [`Activity`], mirroring the C# inheritance relationship.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DependentActivity<K: Key, R: Key, W: Key> {
    pub activity: Activity<K, R, W>,
    /// The compiled dependencies (activity IDs this activity depends on).
    pub dependencies: IndexSet<K>,
    /// The planning-only dependencies.
    pub planning_dependencies: IndexSet<K>,
    /// Dependencies introduced by resource allocation.
    pub resource_dependencies: IndexSet<K>,
    /// The IDs of activities that depend on this one (populated on compile).
    pub successors: IndexSet<K>,
}

impl<K: Key, R: Key, W: Key> DependentActivity<K, R, W> {
    /// Creates an activity with the given ID and duration, and no dependencies.
    pub fn new(id: K, duration: i32) -> Self {
        Self {
            activity: Activity::new(id, duration),
            dependencies: IndexSet::new(),
            planning_dependencies: IndexSet::new(),
            resource_dependencies: IndexSet::new(),
            successors: IndexSet::new(),
        }
    }

    /// Creates an activity with the given ID, duration and removability flag.
    pub fn new_removable(id: K, duration: i32, can_be_removed: bool) -> Self {
        Self {
            activity: Activity::new_removable(id, duration, can_be_removed),
            dependencies: IndexSet::new(),
            planning_dependencies: IndexSet::new(),
            resource_dependencies: IndexSet::new(),
            successors: IndexSet::new(),
        }
    }

    /// Creates an activity with the given ID, duration and dependencies.
    pub fn with_dependencies(
        id: K,
        duration: i32,
        dependencies: impl IntoIterator<Item = K>,
    ) -> Self {
        let mut a = Self::new(id, duration);
        a.dependencies = dependencies.into_iter().collect();
        a
    }

    /// Creates an activity with the given ID, duration, dependencies and planning dependencies.
    pub fn with_planning_dependencies(
        id: K,
        duration: i32,
        dependencies: impl IntoIterator<Item = K>,
        planning_dependencies: impl IntoIterator<Item = K>,
    ) -> Self {
        let mut a = Self::with_dependencies(id, duration, dependencies);
        a.planning_dependencies = planning_dependencies.into_iter().collect();
        a
    }
}

impl<K: Key, R: Key, W: Key> Deref for DependentActivity<K, R, W> {
    type Target = Activity<K, R, W>;

    fn deref(&self) -> &Self::Target {
        &self.activity
    }
}

impl<K: Key, R: Key, W: Key> DerefMut for DependentActivity<K, R, W> {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.activity
    }
}

impl<K: Key, R: Key, W: Key> HasId<K> for DependentActivity<K, R, W> {
    fn id(&self) -> K {
        self.activity.id()
    }

    fn can_be_removed(&self) -> bool {
        self.activity.can_be_removed()
    }
}
