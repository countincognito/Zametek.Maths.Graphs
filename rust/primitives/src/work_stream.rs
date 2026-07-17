use crate::key::Key;

/// A stream of work (optionally a sequential phase) — the counterpart of the
/// C# `WorkStream<T>`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WorkStream<W: Key> {
    pub id: W,
    pub name: String,
    pub is_phase: bool,
}

impl<W: Key> WorkStream<W> {
    pub fn new(id: W, name: impl Into<String>, is_phase: bool) -> Self {
        Self {
            id,
            name: name.into(),
            is_phase,
        }
    }
}
