use crate::activity::DependentActivity;
use crate::enums::GraphCompilationErrorCode;
use crate::key::Key;
use crate::schedule::ResourceSchedule;
use crate::work_stream::WorkStream;
use indexmap::IndexSet;

/// A set of activity IDs that form a dependency cycle - the counterpart of the
/// C# `CircularDependency<T>`.
///
/// The IDs are kept in discovery order (the C# `HashSet<T>` preserves insertion
/// order in practice, and the error formatting relies on it); equality is
/// order-insensitive, as in C#.
#[derive(Debug, Clone)]
pub struct CircularDependency<K: Key> {
    pub dependencies: Vec<K>,
}

impl<K: Key> CircularDependency<K> {
    pub fn new(dependencies: impl IntoIterator<Item = K>) -> Self {
        Self {
            dependencies: dependencies.into_iter().collect(),
        }
    }
}

impl<K: Key> PartialEq for CircularDependency<K> {
    fn eq(&self, other: &Self) -> bool {
        let mut a = self.dependencies.clone();
        let mut b = other.dependencies.clone();
        a.sort();
        b.sort();
        a == b
    }
}

impl<K: Key> Eq for CircularDependency<K> {}

/// A violated activity constraint - the counterpart of the C#
/// `InvalidConstraint<T>`. Equality compares the message case-insensitively,
/// as in C# (`OrdinalIgnoreCase`).
#[derive(Debug, Clone)]
pub struct InvalidConstraint<K: Key> {
    pub id: K,
    pub message: String,
}

impl<K: Key> InvalidConstraint<K> {
    pub fn new(id: K, message: impl Into<String>) -> Self {
        Self {
            id,
            message: message.into(),
        }
    }
}

impl<K: Key> PartialEq for InvalidConstraint<K> {
    fn eq(&self, other: &Self) -> bool {
        self.id == other.id && self.message.eq_ignore_ascii_case(&other.message)
    }
}

impl<K: Key> Eq for InvalidConstraint<K> {}

/// The resources an activity requires but that are not available - the
/// counterpart of the C# `UnavailableResources<T, TResourceId>`.
#[derive(Debug, Clone)]
pub struct UnavailableResources<K: Key, R: Key> {
    pub id: K,
    pub resource_ids: IndexSet<R>,
}

impl<K: Key, R: Key> UnavailableResources<K, R> {
    pub fn new(id: K, resource_ids: impl IntoIterator<Item = R>) -> Self {
        Self {
            id,
            resource_ids: resource_ids.into_iter().collect(),
        }
    }
}

impl<K: Key, R: Key> PartialEq for UnavailableResources<K, R> {
    fn eq(&self, other: &Self) -> bool {
        if self.id != other.id {
            return false;
        }
        let mut a: Vec<R> = self.resource_ids.iter().copied().collect();
        let mut b: Vec<R> = other.resource_ids.iter().copied().collect();
        a.sort();
        b.sort();
        a == b
    }
}

impl<K: Key, R: Key> Eq for UnavailableResources<K, R> {}

/// An error found while compiling a graph - the counterpart of the C#
/// `GraphCompilationError`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphCompilationError {
    pub error_code: GraphCompilationErrorCode,
    pub error_message: String,
}

impl GraphCompilationError {
    pub fn new(error_code: GraphCompilationErrorCode, error_message: impl Into<String>) -> Self {
        Self {
            error_code,
            error_message: error_message.into(),
        }
    }
}

/// The result of compiling a graph - the counterpart of the C#
/// `GraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity>`.
#[derive(Debug, Clone)]
pub struct GraphCompilation<K: Key, R: Key, W: Key> {
    pub dependent_activities: Vec<DependentActivity<K, R, W>>,
    pub resource_schedules: Vec<ResourceSchedule<K, R, W>>,
    pub work_streams: Vec<WorkStream<W>>,
    pub compilation_errors: Vec<GraphCompilationError>,
}

impl<K: Key, R: Key, W: Key> GraphCompilation<K, R, W> {
    pub fn new(
        dependent_activities: Vec<DependentActivity<K, R, W>>,
        resource_schedules: Vec<ResourceSchedule<K, R, W>>,
        work_streams: Vec<WorkStream<W>>,
        compilation_errors: Vec<GraphCompilationError>,
    ) -> Self {
        Self {
            dependent_activities,
            resource_schedules,
            work_streams,
            compilation_errors,
        }
    }
}
