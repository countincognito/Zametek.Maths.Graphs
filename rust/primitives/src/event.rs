use crate::edge::HasId;
use crate::key::Key;

/// An event (milestone) in a project-planning graph — the counterpart of the
/// C# `Event<T>`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Event<K: Key> {
    id: K,
    pub earliest_finish_time: Option<i32>,
    pub latest_finish_time: Option<i32>,
    can_be_removed: bool,
}

impl<K: Key> Event<K> {
    /// Creates an event with the given ID.
    pub fn new(id: K) -> Self {
        Self::with_times(id, None, None)
    }

    /// Creates an event with the given ID and finish times.
    pub fn with_times(
        id: K,
        earliest_finish_time: Option<i32>,
        latest_finish_time: Option<i32>,
    ) -> Self {
        Self {
            id,
            earliest_finish_time,
            latest_finish_time,
            can_be_removed: false,
        }
    }

    pub fn id(&self) -> K {
        self.id
    }

    /// Whether the event may be removed during clean-up passes.
    pub fn can_be_removed(&self) -> bool {
        self.can_be_removed
    }

    /// Marks the event as not removable.
    pub fn set_as_read_only(&mut self) {
        self.can_be_removed = false;
    }

    /// Marks the event as removable.
    pub fn set_as_removable(&mut self) {
        self.can_be_removed = true;
    }
}

impl<K: Key> HasId<K> for Event<K> {
    fn id(&self) -> K {
        self.id
    }

    fn can_be_removed(&self) -> bool {
        self.can_be_removed
    }
}
