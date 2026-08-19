//! Activity-on-Vertex graphs: activities live on nodes, events on edges.

mod builder;
mod compiler;
mod cpm;
mod engines;
mod incremental;
mod reducer;
mod state;

pub use builder::VertexGraphBuilder;
pub use compiler::VertexGraphCompiler;
pub use engines::{
    VertexCriticalPathEngine, VertexGraphBuilderEngines,
    VertexTarjanStronglyConnectedComponentsFinder, VertexTransitiveReducer,
};
pub use incremental::IncrementalCriticalPath;
pub use state::VertexGraphState;
