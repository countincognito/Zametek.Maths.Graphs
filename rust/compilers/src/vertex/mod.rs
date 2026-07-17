//! Activity-on-Vertex graphs: activities live on nodes, events on edges.

mod builder;
mod compiler;
mod cpm;
mod reducer;
mod state;

pub use builder::VertexGraphBuilder;
pub use compiler::VertexGraphCompiler;
