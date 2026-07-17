/// The position of a node within a directed graph.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub enum NodeType {
    /// A node with both incoming and outgoing edges.
    #[default]
    Normal,
    /// A node with only outgoing edges.
    Start,
    /// A node with only incoming edges.
    End,
    /// A node with no edges at all.
    Isolated,
}

/// How an activity's target resources combine when the scheduler decides
/// which resources may perform it.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub enum LogicalOperator {
    /// The activity requires all of its target resources; if any are missing
    /// from the supplied list it cannot be scheduled.
    #[default]
    And,
    /// Any one of the activity's target resources will do.
    Or,
    /// Like [`LogicalOperator::And`], but evaluated only over the target
    /// resources actually present in the supplied list.
    ActiveAnd,
}

/// How a resource's time (and therefore cost, billing and effort) is spread
/// across the schedule between the activities it performs.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub enum InterActivityAllocationType {
    /// No inter-activity spreading; only the scheduled activities themselves
    /// are allocated. Used for synthetic resources.
    #[default]
    None,
    /// The resource is allocated only while actively performing scheduled
    /// activities; idle gaps between activities are not counted.
    Direct,
    /// The resource is allocated continuously across the span of its
    /// involvement, filling the gaps between its activities (overhead-style
    /// commitment).
    Indirect,
}

/// The error codes a graph compilation can report. Codes prefixed P are found
/// before compilation; codes prefixed C are found after compilation.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum GraphCompilationErrorCode {
    /// Invalid dependencies - an activity depends on an ID that no activity in the graph has.
    P0010,
    /// Circular dependencies - the activity dependencies form a cycle.
    P0020,
    /// Invalid pre-compilation constraints - an activity's requested constraints are self-contradictory.
    P0030,
    /// All resources are marked as explicit targets, but not all activities have targeted resources.
    P0040,
    /// Unable to remove unnecessary edges during pre-compilation clean-up.
    P0050,
    /// Some necessary explicit target resources are unavailable.
    P0060,
    /// Invalid post-compilation constraints - the computed times violate an activity's constraints.
    C0010,
}
