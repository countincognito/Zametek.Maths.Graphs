//! Graph compilers for project planning — Rust port of
//! `Zametek.Maths.Graphs.Compilers`.
//!
//! Two graph representations are supported:
//!
//! * **Activity-on-Vertex** ([`VertexGraphBuilder`], [`VertexGraphCompiler`]):
//!   activities live on nodes, events on edges. This is the compiler to use
//!   for analysis — [`VertexGraphCompiler::compile`] runs the full pipeline
//!   including resource scheduling.
//! * **Activity-on-Arrow** ([`ArrowGraphBuilder`], [`ArrowGraphCompiler`]):
//!   activities live on edges, events on nodes, with dummy-edge orchestration.
//!   Intended for rendering the classic arrow diagram.
//!
//! The C# original decomposes the algorithms into injectable engine interfaces
//! (SCC finder, CPM engine, transitive reducer, resource scheduler, generators).
//! This port preserves those seams as the traits in [`contracts`], each with a
//! default engine struct that keeps the C# name. Unlike C#, the engines are
//! stateless (they take the graph state as a parameter), which removes the need
//! for the C# `…Factory` seams. The C# thread-safety locks are replaced by
//! Rust's `&mut` ownership rules.
//!
//! To customise an engine, pass an engines bundle
//! ([`VertexGraphBuilderEngines`](vertex::VertexGraphBuilderEngines) /
//! [`ArrowGraphBuilderEngines`](arrow::ArrowGraphBuilderEngines)) to the
//! builder's `with_engines` constructor, setting only the fields you want to
//! override.

mod ancestor;
pub mod arrow;
mod constraint_checker;
pub mod contracts;
mod error_formatter;
mod generators;
mod id_gen;
pub mod messages;
mod scheduling;
mod shuffle;
mod tarjan;
pub mod vertex;

pub use arrow::{
    ArrowCriticalPathEngine, ArrowGraphBuilder, ArrowGraphBuilderEngines, ArrowGraphCompiler,
    ArrowGraphState, ArrowTarjanStronglyConnectedComponentsFinder, ArrowTransitiveReducer,
    DummyEdgeOrchestrator,
};
pub use generators::{DummyActivityGenerator, EventGenerator, RemovableEventGenerator};
pub use id_gen::{NextIdGenerator, PreviousIdGenerator};
pub use scheduling::{PriorityListResourceScheduler, ResourceScheduleBuilder};
pub use vertex::{
    VertexCriticalPathEngine, VertexGraphBuilder, VertexGraphBuilderEngines, VertexGraphCompiler,
    VertexGraphState, VertexTarjanStronglyConnectedComponentsFinder, VertexTransitiveReducer,
};

pub use zametek_maths_graphs_primitives as primitives;

/// The structure exported by a vertex builder: activities on nodes, events on
/// edges — the counterpart of the C# `Graph<T, IEvent<T>, TActivity>`.
pub type VertexGraph<K, R, W> =
    primitives::Graph<K, primitives::Event<K>, primitives::DependentActivity<K, R, W>>;

/// The structure exported by an arrow builder: activities on edges, events on
/// nodes — the counterpart of the C# `Graph<T, TActivity, IEvent<T>>`.
pub type ArrowGraph<K, R, W> =
    primitives::Graph<K, primitives::DependentActivity<K, R, W>, primitives::Event<K>>;
