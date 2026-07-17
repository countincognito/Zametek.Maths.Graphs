//! The user-facing message strings, ported verbatim from the C# resource files
//! so error output matches. Multi-line compilation error messages join lines
//! with `\n` (the C# original uses `Environment.NewLine`, which is
//! platform-dependent; this port fixes it to `\n`).

pub const MSG_ALL_RESOURCES_EXPLICIT_TARGETS_NOT_ALL_ACTIVITIES_TARGETED: &str =
    "All resources are explicit targets, but not all activities have targeted resources";
pub const MSG_ARROW_GRAPH_CONTAINS_MORE_THAN_ONE_END_NODE: &str =
    "Arrow graph contains more than one End node";
pub const MSG_ARROW_GRAPH_CONTAINS_MORE_THAN_ONE_START_NODE: &str =
    "Arrow graph contains more than one Start node";
pub const MSG_AT_LEAST_ONE_ACTIVITY_REQUIRES_NON_EXPLICIT_TARGET_RESOURCE: &str =
    "At least one activity requires a non-explicit target resource, but all provided resources are explicit targets";
pub const MSG_AT_LEAST_ONE_TARGET_RESOURCE_NOT_AVAILABLE: &str =
    "At least one of specified target resources are not available in the resources provided";
pub const MSG_CANNOT_BACKFILL_ISOLATED_NODES: &str = "Cannot backfill Isolated nodes";
pub const MSG_CANNOT_CALCULATE_CRITICAL_PATH: &str = "Cannot calculate critical path";
pub const MSG_CANNOT_CALCULATE_CRITICAL_PATH_BACKWARD_FLOW: &str =
    "Cannot calculate critical path backward flow";
pub const MSG_CANNOT_CALCULATE_CRITICAL_PATH_FORWARD_FLOW: &str =
    "Cannot calculate critical path forward flow";
pub const MSG_CANNOT_CALCULATE_CRITICAL_PATH_PRIORITY_LIST: &str =
    "Cannot calculate critical path priority list";
pub const MSG_CANNOT_CALCULATE_EARLIEST_FINISH_TIMES_DUE_TO_CYCLIC_DEPENDENCY: &str =
    "Cannot calculate earliest finish times due to cyclic dependency";
pub const MSG_CANNOT_CALCULATE_EVENT_EARLIEST_FINISH_TIMES: &str =
    "Cannot calculate Event earliest finish times";
pub const MSG_CANNOT_CALCULATE_EVENT_LATEST_FINISH_TIMES: &str =
    "Cannot calculate Event latest finish times";
pub const MSG_CANNOT_CALCULATE_LATEST_FINISH_TIMES_DUE_TO_CYCLIC_DEPENDENCY: &str =
    "Cannot calculate latest finish times due to cyclic dependency";
pub const MSG_CANNOT_CONSTRUCT_ARROW_GRAPH_DUE_TO_INVALID_DEPENDENCIES: &str =
    "Cannot construct arrow graph due to invalid dependencies";
pub const MSG_CANNOT_PERFORM_EDGE_REDIRECTION: &str = "Cannot perform edge redirection";
pub const MSG_CANNOT_PERFORM_TRANSITIVE_REDUCTION: &str = "Cannot perform transitive reduction";
pub const MSG_CANNOT_REMOVE_REDUNDANT_EDGES: &str = "Cannot remove redundant edges";
pub const MSG_CANNOT_SET_MINIMUM_FREE_SLACK_AND_MAXIMUM_LATEST_FINISH_TIME: &str =
    "Cannot set MinimumFreeSlack and MaximumLatestFinishTime at the same time";
pub const MSG_CIRCULAR_DEPENDENCIES: &str = "Circular activity dependencies:";
pub const MSG_EARLIEST_FINISH_TIME_LESS_THAN_ZERO: &str =
    "EarliestFinishTime cannot be less than zero";
pub const MSG_EARLIEST_START_TIME_LESS_THAN_MINIMUM_EARLIEST_START_TIME: &str =
    "EarliestStartTime cannot be less than MinimumEarliestStartTime";
pub const MSG_EARLIEST_START_TIME_LESS_THAN_ZERO: &str =
    "EarliestStartTime cannot be less than zero";
pub const MSG_FREE_SLACK_LESS_THAN_MINIMUM_FREE_SLACK: &str =
    "FreeSlack cannot be less than MinimumFreeSlack";
pub const MSG_INVALID_CONSTRAINTS: &str = "Invalid activity constraints:";
pub const MSG_INVALID_DEPENDENCIES: &str = "Invalid activity dependencies:";
pub const MSG_IS_INVALID_BUT_REFERENCED_BY: &str = "is invalid but referenced by:";
pub const MSG_LATEST_FINISH_TIME_LESS_THAN_EARLIEST_FINISH_TIME: &str =
    "LatestFinishTime cannot be less than EarliestFinishTime";
pub const MSG_LATEST_FINISH_TIME_LESS_THAN_ZERO: &str = "LatestFinishTime cannot be less than zero";
pub const MSG_LATEST_FINISH_TIME_MORE_THAN_MAXIMUM_LATEST_FINISH_TIME: &str =
    "LatestFinishTime cannot be more than MaximumLatestFinishTime";
pub const MSG_LATEST_START_TIME_LESS_THAN_EARLIEST_START_TIME: &str =
    "LatestStartTime cannot be less than EarliestStartTime";
pub const MSG_LATEST_START_TIME_LESS_THAN_ZERO: &str = "LatestStartTime cannot be less than zero";
pub const MSG_LIST_OF_EDGE_IDS_AND_HEAD_NODES_DO_NOT_MATCH: &str =
    "List of Edge IDs and Edges referenced by head Nodes do not match";
pub const MSG_LIST_OF_EDGE_IDS_AND_TAIL_NODES_DO_NOT_MATCH: &str =
    "List of Edge IDs and Edges referenced by tail Nodes do not match";
pub const MSG_LIST_OF_NODE_IDS_AND_TAIL_NODES_DO_NOT_MATCH: &str =
    "List of Node IDs and Edges referenced by tail Nodes do not match";
pub const MSG_MINIMUM_EARLIEST_START_TIME_PLUS_DURATION: &str =
    "(MinimumEarliestStartTime + Duration) must be greater than MaximumLatestFinishTime";
pub const MSG_UNABLE_TO_REMOVE_UNNECESSARY_EDGES: &str = "Unable to remove unnecessary edges";
pub const MSG_UNAVAILABLE_RESOURCES: &str = "Unavailable resources for activities:";
pub const MSG_VALUE_CANNOT_BE_NEGATIVE: &str = "Value cannot be negative";
pub const MSG_VERTEX_GRAPH_NORMAL_NODES_WITHOUT_END_NODES: &str =
    "Vertex graph cannot contain Normal nodes without any End nodes";
pub const MSG_VERTEX_GRAPH_NORMAL_NODES_WITHOUT_START_NODES: &str =
    "Vertex graph cannot contain Normal nodes without any Start nodes";
