//! In-crate unit tests ported from `Zametek.Maths.Graphs.Compilers.Tests/Engines`.
//!
//! The C# engine tests construct the internal graph state directly (through
//! `InternalsVisibleTo`) and drive one engine in isolation. The Rust state is
//! `pub(crate)`, so the faithful counterpart is a unit test inside this crate
//! rather than an integration test in `tests/`. C# null-argument guards have no
//! Rust counterpart (the state is `&mut` and the constraint lists are `&[…]`)
//! and are omitted throughout.

mod common;
mod cpm_engine;
mod dummy_edge_orchestrator;
mod error_formatter;
mod generators;
mod tarjan_scc;
mod transitive_reducer;
