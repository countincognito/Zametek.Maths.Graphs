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
mod duration;
mod edge;
mod enums;
mod error;
mod event;
mod graph;
pub mod graph_limits;
mod insertion_order_set;
mod key;
mod node;
mod packed_bool_list;
mod resource;
mod schedule;
mod simulation;
mod work_stream;

pub use activity::{Activity, DependentActivity};
pub use compilation::{
    CircularDependency, GraphCompilation, GraphCompilationError, InvalidConstraint,
    UnavailableResources,
};
pub use duration::{DurationDistribution, DurationEstimate};
pub use edge::{Edge, HasId};
pub use enums::{
    GraphCompilationErrorCode, InterActivityAllocationType, LogicalOperator, NodeType,
};
pub use error::{GraphError, GraphErrorKind};
pub use event::Event;
pub use graph::Graph;
pub use insertion_order_set::InsertionOrderSet;
pub use key::Key;
pub use node::Node;
pub use packed_bool_list::{Iter as PackedBoolListIter, PackedBoolList};
pub use resource::Resource;
pub use schedule::{ResourceSchedule, ScheduledActivity};
pub use simulation::{
    CriticalPath, CriticalPathLink, CriticalPathStep, SimulationResult, SimulationSettings,
};
pub use work_stream::WorkStream;
