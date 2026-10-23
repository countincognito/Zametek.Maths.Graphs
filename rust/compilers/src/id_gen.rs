use crate::contracts::{IIdGenerator, IIdGeneratorFactory};
use zametek_maths_graphs_primitives::Key;

/// Generates sequential IDs in ascending order - the counterpart of the C#
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

/// Generates sequential IDs in descending order - the counterpart of the C#
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

/// Makes [`NextIdGenerator`] objects: generators of IDs in ascending order that
/// start over the highest ID that the graph holds - the counterpart of the C#
/// `NextIdGeneratorFactory<T>`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub struct NextIdGeneratorFactory<K: Key> {
    initial: K,
}

impl<K: Key> NextIdGeneratorFactory<K> {
    /// Creates a factory whose generators start stepping upwards from the given
    /// initial value, or from the highest ID of the graph if that ID is higher.
    pub fn new(initial: K) -> Self {
        Self { initial }
    }
}

impl<K: Key> IIdGeneratorFactory<K> for NextIdGeneratorFactory<K> {
    fn create(&self, used_ids: &[K]) -> Box<dyn IIdGenerator<K>> {
        let start = used_ids.iter().copied().fold(self.initial, K::max);
        Box::new(NextIdGenerator::new(start))
    }
}

/// Makes [`PreviousIdGenerator`] objects: generators of IDs in descending order
/// that start under the lowest ID that the graph holds - the counterpart of the
/// C# `PreviousIdGeneratorFactory<T>`. It is the factory that builders have by
/// default. (The C# `StandardIdGeneratorFactory<T>` chooses by the type of the
/// ID, because a `Guid` has no previous value. A `Key` of this crate has one.)
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub struct PreviousIdGeneratorFactory<K: Key> {
    initial: K,
}

impl<K: Key> PreviousIdGeneratorFactory<K> {
    /// Creates a factory whose generators start stepping downwards from the
    /// given initial value, or from the lowest ID of the graph if that ID is
    /// lower.
    pub fn new(initial: K) -> Self {
        Self { initial }
    }
}

impl<K: Key> IIdGeneratorFactory<K> for PreviousIdGeneratorFactory<K> {
    fn create(&self, used_ids: &[K]) -> Box<dyn IIdGenerator<K>> {
        let start = used_ids.iter().copied().fold(self.initial, K::min);
        Box::new(PreviousIdGenerator::new(start))
    }
}
