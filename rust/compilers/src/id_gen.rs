use zametek_maths_graphs_primitives::Key;

/// Sequential ID generation — the counterpart of the C# `IIdGenerator<T>` with
/// its `NextIdGenerator`/`PreviousIdGenerator` implementations.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum IdGenerator<K: Key> {
    /// Generates IDs in ascending order, starting after the given value.
    Next(K),
    /// Generates IDs in descending order, starting before the given value.
    Previous(K),
}

impl<K: Key> IdGenerator<K> {
    /// Generates the next ID in the sequence.
    pub fn generate(&mut self) -> K {
        match self {
            IdGenerator::Next(value) => {
                *value = value.next();
                *value
            }
            IdGenerator::Previous(value) => {
                *value = value.previous();
                *value
            }
        }
    }
}
