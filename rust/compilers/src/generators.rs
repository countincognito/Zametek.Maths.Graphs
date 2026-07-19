//! Default event and dummy-activity generators — the counterparts of the C#
//! `EventGenerator`, `RemovableEventGenerator` and `DummyActivityGenerator`.

use crate::contracts::{IActivityGenerator, IEventGenerator};
use zametek_maths_graphs_primitives::{DependentActivity, Event, Key};

/// Default event generator for Activity-on-Arrow graphs — events represent real
/// milestones, so they are created read-only (not removable). The counterpart
/// of the C# `EventGenerator<T>`.
#[derive(Debug, Clone, Copy, Default)]
pub struct EventGenerator;

impl<K: Key> IEventGenerator<K> for EventGenerator {
    fn generate(&self, id: K) -> Event<K> {
        Event::new(id)
    }

    fn generate_with_times(
        &self,
        id: K,
        earliest_finish_time: Option<i32>,
        latest_finish_time: Option<i32>,
    ) -> Event<K> {
        Event::with_times(id, earliest_finish_time, latest_finish_time)
    }
}

/// Default event generator for Activity-on-Vertex graphs — events live on
/// structural edges, which are removed during transitive reduction, so they are
/// flagged removable. Decorates an inner [`EventGenerator`], mirroring the C#
/// `RemovableEventGenerator<T>` decorator over `EventGenerator<T>`.
#[derive(Debug, Clone, Copy, Default)]
pub struct RemovableEventGenerator {
    inner: EventGenerator,
}

impl RemovableEventGenerator {
    /// Creates a generator decorating a default [`EventGenerator`].
    pub fn new() -> Self {
        Self::default()
    }
}

impl<K: Key> IEventGenerator<K> for RemovableEventGenerator {
    fn generate(&self, id: K) -> Event<K> {
        let mut event = IEventGenerator::<K>::generate(&self.inner, id);
        event.set_as_removable();
        event
    }

    fn generate_with_times(
        &self,
        id: K,
        earliest_finish_time: Option<i32>,
        latest_finish_time: Option<i32>,
    ) -> Event<K> {
        let mut event = IEventGenerator::<K>::generate_with_times(
            &self.inner,
            id,
            earliest_finish_time,
            latest_finish_time,
        );
        event.set_as_removable();
        event
    }
}

/// Default dummy-activity generator — creates removable zero-duration
/// activities. The counterpart of the C# `DummyActivityGenerator<…>`.
#[derive(Debug, Clone, Copy, Default)]
pub struct DummyActivityGenerator;

impl<K: Key, R: Key, W: Key> IActivityGenerator<K, R, W> for DummyActivityGenerator {
    fn generate(&self, id: K) -> DependentActivity<K, R, W> {
        DependentActivity::new_removable(id, 0, true)
    }
}
