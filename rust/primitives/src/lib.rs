//! Domain primitives for project-planning graphs (Critical Path Method).
//!
//! Rust port of `Zametek.Maths.Graphs.Primitives`. The C# library is generic over
//! three ID types (`T`, `TResourceId`, `TWorkStreamId`), each constrained to
//! `struct, IComparable, IEquatable`; the same genericity is expressed here with
//! the [`Key`] trait.
//!
//! Where the C# model splits `Activity` (base class) from `DependentActivity`
//! (subclass with dependency sets), this port composes: [`DependentActivity`]
//! wraps an [`Activity`] and dereferences to it. A `DependentActivity` with empty
//! dependency sets is equivalent to a plain `Activity`, so the compilers work
//! exclusively with `DependentActivity` without loss of generality.

mod activity;
mod compilation;
mod edge;
mod enums;
mod error;
mod event;
mod graph;
mod key;
mod node;
mod resource;
mod schedule;
mod work_stream;

pub use activity::{Activity, DependentActivity};
pub use compilation::{
    CircularDependency, GraphCompilation, GraphCompilationError, InvalidConstraint,
    UnavailableResources,
};
pub use edge::{Edge, HasId};
pub use enums::{
    GraphCompilationErrorCode, InterActivityAllocationType, LogicalOperator, NodeType,
};
pub use error::GraphError;
pub use event::Event;
pub use graph::Graph;
pub use key::Key;
pub use node::Node;
pub use resource::Resource;
pub use schedule::{ResourceSchedule, ScheduledActivity};
pub use work_stream::WorkStream;
