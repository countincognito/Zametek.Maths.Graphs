use super::builder::ArrowGraphBuilder;
use crate::id_gen::IdGenerator;
use crate::messages;
use indexmap::IndexSet;
use zametek_maths_graphs_primitives::{DependentActivity, GraphError, Key};

/// Compiler for Activity-on-Arrow graphs: a coordinator around an
/// [`ArrowGraphBuilder`] — the counterpart of the C# `ArrowGraphCompiler`.
/// Intended for rendering — it prepares the network for
/// [`ArrowGraphCompiler::to_graph`] and performs no resource scheduling (use
/// [`crate::VertexGraphCompiler`] for analysis).
pub struct ArrowGraphCompiler<K: Key, R: Key, W: Key> {
    builder: ArrowGraphBuilder<K, R, W>,
}

impl<K: Key, R: Key, W: Key> Default for ArrowGraphCompiler<K, R, W> {
    fn default() -> Self {
        Self::new()
    }
}

impl<K: Key, R: Key, W: Key> ArrowGraphCompiler<K, R, W> {
    /// Creates a compiler wired with the default engines.
    pub fn new() -> Self {
        Self {
            builder: ArrowGraphBuilder::new(
                IdGenerator::Previous(K::default()),
                IdGenerator::Previous(K::default()),
            ),
        }
    }

    /// Creates a compiler around the given builder.
    pub fn with_builder(builder: ArrowGraphBuilder<K, R, W>) -> Self {
        Self { builder }
    }

    pub fn builder(&self) -> &ArrowGraphBuilder<K, R, W> {
        &self.builder
    }

    pub fn builder_mut(&mut self) -> &mut ArrowGraphBuilder<K, R, W> {
        &mut self.builder
    }

    /// The earliest start time across all activities.
    pub fn start_time(&self) -> i32 {
        self.builder.start_time()
    }

    /// The latest finish time across all activities.
    pub fn finish_time(&self) -> i32 {
        self.builder.finish_time()
    }

    /// The cyclomatic complexity of the network (a measure of its parallelism).
    pub fn cyclomatic_complexity(&self) -> i32 {
        let edge_count = self.builder.edge_ids().len() as i32;
        let node_count = self.builder.node_ids().len() as i32;

        // Correction factor for multiple entry and exit points.

        // Artificial Start and End nodes (there is only one Start and one End
        // in an arrow graph).
        let extra_nodes = 2;
        // Artificial edges to connect the artificial Start and End nodes.
        let extra_edges =
            (self.builder.start_nodes().len() + self.builder.end_nodes().len()) as i32;

        // Isolated nodes count as separate connected components.
        let isolated_node_count = self.builder.isolated_nodes().len() as i32;

        (edge_count + extra_edges) - (node_count + extra_nodes) + 2 * (1 + isolated_node_count)
    }

    /// Returns an unused activity ID.
    pub fn get_next_activity_id(&self) -> K {
        self.builder
            .activity_ids()
            .into_iter()
            .max()
            .unwrap_or_default()
            .next()
    }

    /// Clears all activities and returns the compiler to its initial state.
    pub fn reset(&mut self) {
        self.builder.reset();
    }

    /// Adds an activity, wiring its compiled and planning dependencies into
    /// the graph. Returns false if the ID already exists.
    pub fn add_activity(&mut self, activity: DependentActivity<K, R, W>) -> bool {
        let dependencies: IndexSet<K> = activity
            .dependencies
            .iter()
            .chain(activity.planning_dependencies.iter())
            .copied()
            .collect();
        self.builder
            .add_activity_with_dependencies(activity, dependencies)
    }

    /// Strips redundant dependencies, keeping only the minimal edge set.
    pub fn transitive_reduction(&mut self) -> Result<(), GraphError> {
        let transitively_reduced = self.builder.transitive_reduction()?;
        if !transitively_reduced {
            return Err(GraphError::new(
                messages::MSG_CANNOT_PERFORM_TRANSITIVE_REDUCTION,
            ));
        }
        Ok(())
    }

    /// Validates the graph, applies transitive reduction and runs the
    /// critical-path calculation so the network can be laid out. Performs no
    /// resource scheduling.
    pub fn compile(&mut self) -> Result<(), GraphError> {
        // Sanity check the graph data.
        if !self.builder.invalid_dependencies().is_empty() {
            return Err(GraphError::new(
                messages::MSG_CANNOT_CONSTRUCT_ARROW_GRAPH_DUE_TO_INVALID_DEPENDENCIES,
            ));
        }
        self.transitive_reduction()?;
        self.builder.calculate_critical_path()
    }

    /// Exports the compiled Activity-on-Arrow structure for rendering.
    pub fn to_graph(&mut self) -> Result<crate::ArrowGraph<K, R, W>, GraphError> {
        self.builder.to_graph()
    }
}
