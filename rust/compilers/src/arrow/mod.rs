//! Activity-on-Arrow graphs: activities live on edges, events on nodes, with
//! dummy-edge orchestration.

mod builder;
mod compiler;
mod cpm;
mod orchestrator;
mod reducer;
mod state;

pub use builder::ArrowGraphBuilder;
pub use compiler::ArrowGraphCompiler;
