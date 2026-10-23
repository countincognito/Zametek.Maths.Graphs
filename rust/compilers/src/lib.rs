//! Graph compilers for project planning - Rust port of
//! `Zametek.Maths.Graphs.Compilers`.
//!
//! Two graph representations are supported:
//!
//! * **Activity-on-Vertex** ([`VertexGraphBuilder`], [`VertexGraphCompiler`]):
//!   activities live on nodes, events on edges. This is the compiler to use
//!   for analysis - [`VertexGraphCompiler::compile`] runs the full pipeline
//!   including resource scheduling.
//! * **Activity-on-Arrow** ([`ArrowGraphBuilder`], [`ArrowGraphCompiler`]):
//!   activities live on edges, events on nodes, with dummy-edge orchestration.
//!   Intended for rendering the classic arrow diagram.
//!
//! The C# original decomposes the algorithms into injectable engine interfaces
//! (SCC finder, CPM engine, transitive reducer, resource scheduler, generators).
//! This port preserves those seams as the traits in [`contracts`], each with a
//! default engine struct that keeps the C# name. As in the C# reference the
//! engines are stateless (they take the graph state as a parameter), so they
//! have no `…Factory` seams, and one engine serves every builder and every
//! thread (the engine traits are `Send + Sync`). The C# thread-safety locks are
//! replaced by Rust's `&mut` ownership rules.
//!
//! To customise an engine, pass an engines bundle
//! ([`VertexGraphBuilderEngines`](vertex::VertexGraphBuilderEngines) /
//! [`ArrowGraphBuilderEngines`](arrow::ArrowGraphBuilderEngines)) to the
//! builder's `with_engines` constructor, setting only the fields you want to
//! override.
//!
//! A builder is not stateless: it holds the state of one graph. Code that needs
//! many builders takes a factory ([`contracts::IVertexGraphFactory`] /
//! [`contracts::IArrowGraphFactory`]), and [`VertexGraphFactory`] /
//! [`ArrowGraphFactory`] are the standard ones. They take the same engines
//! bundle, and each builder that they make asks the ID generator factory of the
//! bundle ([`contracts::IIdGeneratorFactory`]) for an ID generator of its own.
//! [`ScheduleSimulator::with_factory`] takes a vertex factory.

mod ancestor;
pub mod arrow;
mod constraint_checker;
pub mod contracts;
mod error_formatter;
mod generators;
mod id_gen;
mod insertion_order_map;
mod limit_checker;
pub mod messages;
mod scheduling;
mod shuffle;
pub mod simulation;
mod tarjan;
pub mod vertex;

// In-crate unit tests for the engines: they construct the crate-private graph
// state directly (as the C# engine tests do via InternalsVisibleTo), which an
// external integration test cannot reach.
#[cfg(test)]
mod engine_tests;

pub use arrow::{
    ArrowCriticalPathEngine, ArrowGraphBuilder, ArrowGraphBuilderEngines, ArrowGraphCompiler,
    ArrowGraphFactory, ArrowGraphState, ArrowTarjanStronglyConnectedComponentsFinder,
    ArrowTransitiveReducer, DummyEdgeOrchestrator,
};
pub use generators::{DummyActivityGenerator, EventGenerator, RemovableEventGenerator};
pub use id_gen::{
    NextIdGenerator, NextIdGeneratorFactory, PreviousIdGenerator, PreviousIdGeneratorFactory,
};
pub use scheduling::{PriorityListResourceScheduler, ResourceScheduleBuilder};
pub use simulation::ScheduleSimulator;
pub use vertex::{
    VertexCriticalPathEngine, VertexGraphBuilder, VertexGraphBuilderEngines, VertexGraphCompiler,
    VertexGraphFactory, VertexGraphState, VertexTarjanStronglyConnectedComponentsFinder,
    VertexTransitiveReducer,
};

pub use zametek_maths_graphs_primitives as primitives;

/// The structure exported by a vertex builder: activities on nodes, events on
/// edges - the counterpart of the C# `Graph<T, IEvent<T>, TActivity>`.
pub type VertexGraph<K, R, W> =
    primitives::Graph<K, primitives::Event<K>, primitives::DependentActivity<K, R, W>>;

/// The structure exported by an arrow builder: activities on edges, events on
/// nodes - the counterpart of the C# `Graph<T, TActivity, IEvent<T>>`.
pub type ArrowGraph<K, R, W> =
    primitives::Graph<K, primitives::DependentActivity<K, R, W>, primitives::Event<K>>;
