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
//! (SCC finder, CPM engine, transitive reducer, resource scheduler). This port
//! keeps the same algorithms and the same inputs/outputs but implements them as
//! plain modules; the C# thread-safety locks are replaced by Rust's `&mut`
//! ownership rules (wrap a compiler in a `Mutex` for shared use).

mod ancestor;
pub mod arrow;
mod constraint_checker;
mod error_formatter;
mod id_gen;
pub mod messages;
mod scheduling;
mod shuffle;
mod tarjan;
pub mod vertex;

pub use arrow::{ArrowGraphBuilder, ArrowGraphCompiler};
pub use id_gen::IdGenerator;
pub use scheduling::ResourceScheduleBuilder;
pub use vertex::{VertexGraphBuilder, VertexGraphCompiler};

pub use zametek_maths_graphs_primitives as primitives;

/// The structure exported by a vertex builder: activities on nodes, events on
/// edges — the counterpart of the C# `Graph<T, IEvent<T>, TActivity>`.
pub type VertexGraph<K, R, W> =
    primitives::Graph<K, primitives::Event<K>, primitives::DependentActivity<K, R, W>>;

/// The structure exported by an arrow builder: activities on edges, events on
/// nodes — the counterpart of the C# `Graph<T, TActivity, IEvent<T>>`.
pub type ArrowGraph<K, R, W> =
    primitives::Graph<K, primitives::DependentActivity<K, R, W>, primitives::Event<K>>;
