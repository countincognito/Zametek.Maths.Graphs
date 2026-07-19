//! Activity-on-Arrow graphs: activities live on edges, events on nodes, with
//! dummy-edge orchestration.

mod builder;
mod compiler;
mod cpm;
mod engines;
mod orchestrator;
mod reducer;
mod state;

pub use builder::ArrowGraphBuilder;
pub use compiler::ArrowGraphCompiler;
pub use engines::{
    ArrowCriticalPathEngine, ArrowGraphBuilderEngines,
    ArrowTarjanStronglyConnectedComponentsFinder, ArrowTransitiveReducer,
};
pub use orchestrator::DummyEdgeOrchestrator;
pub use state::ArrowGraphState;
