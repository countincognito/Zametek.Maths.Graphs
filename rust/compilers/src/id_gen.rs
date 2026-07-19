use crate::contracts::IIdGenerator;
use zametek_maths_graphs_primitives::Key;

/// Generates sequential IDs in ascending order — the counterpart of the C#
/// `NextIdGenerator<T>`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub struct NextIdGenerator<K: Key> {
    value: K,
}

impl<K: Key> NextIdGenerator<K> {
    /// Creates a generator that starts stepping upwards from the given value.
    pub fn new(initial: K) -> Self {
        Self { value: initial }
    }
}

impl<K: Key> IIdGenerator<K> for NextIdGenerator<K> {
    fn generate(&mut self) -> K {
        self.value = self.value.next();
        self.value
    }
}

/// Generates sequential IDs in descending order — the counterpart of the C#
/// `PreviousIdGenerator<T>`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub struct PreviousIdGenerator<K: Key> {
    value: K,
}

impl<K: Key> PreviousIdGenerator<K> {
    /// Creates a generator that starts stepping downwards from the given value.
    pub fn new(initial: K) -> Self {
        Self { value: initial }
    }
}

impl<K: Key> IIdGenerator<K> for PreviousIdGenerator<K> {
    fn generate(&mut self) -> K {
        self.value = self.value.previous();
        self.value
    }
}
