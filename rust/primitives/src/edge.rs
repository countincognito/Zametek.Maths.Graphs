use crate::key::Key;

/// Content that carries its own identity — the counterpart of the C#
/// `IHaveId<T>` (plus the removability probe both graph flavours need).
pub trait HasId<K: Key> {
    fn id(&self) -> K;
    fn can_be_removed(&self) -> bool;
}

/// A directed-graph edge carrying a content payload (an activity in arrow
/// graphs; an event in vertex graphs). Equality is by ID only, as in C#.
#[derive(Debug, Clone)]
pub struct Edge<K: Key, C: HasId<K>> {
    pub content: C,
    _marker: std::marker::PhantomData<K>,
}

impl<K: Key, C: HasId<K>> Edge<K, C> {
    /// Creates an edge carrying the given content.
    pub fn new(content: C) -> Self {
        Self {
            content,
            _marker: std::marker::PhantomData,
        }
    }

    pub fn id(&self) -> K {
        self.content.id()
    }
}

impl<K: Key, C: HasId<K>> PartialEq for Edge<K, C> {
    fn eq(&self, other: &Self) -> bool {
        self.id() == other.id()
    }
}

impl<K: Key, C: HasId<K>> Eq for Edge<K, C> {}
