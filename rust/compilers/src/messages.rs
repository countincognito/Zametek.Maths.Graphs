//! The user-facing message strings, ported verbatim from the C# resource files
//! so error output matches. Multi-line compilation error messages join lines
//! with `\n` (the C# original uses `Environment.NewLine`, which is
//! platform-dependent; this port fixes it to `\n`).

pub const MSG_ACTIVITIES: &str = "activities";
pub const MSG_ACTIVITY: &str = "Activity";
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
pub const MSG_CANNOT_BE_SCHEDULED_WITHIN_MAXIMUM_TIME_VALUE: &str =
    "cannot be scheduled within the maximum supported time value ({0})";
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
pub const MSG_COMPUTED_SCHEDULE_EXCEEDS_MAXIMUM_TIME_VALUE: &str =
    "The computed schedule finish time ({0}) exceeds the maximum supported time value ({1})";
pub const MSG_COUNT_EXCEEDS_MAXIMUM: &str =
    "The number of {0} is {1}, which exceeds the maximum supported count of {2}";
pub const MSG_COULD_NOT_BE_ASSIGNED_TO_ANY_RESOURCE: &str = "could not be assigned to any resource";
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
pub const MSG_LIMITS_EXCEEDED: &str = "Values or counts outside the supported limits:";
pub const MSG_LIST_OF_EDGE_IDS_AND_HEAD_NODES_DO_NOT_MATCH: &str =
    "List of Edge IDs and Edges referenced by head Nodes do not match";
pub const MSG_LIST_OF_EDGE_IDS_AND_TAIL_NODES_DO_NOT_MATCH: &str =
    "List of Edge IDs and Edges referenced by tail Nodes do not match";
pub const MSG_LIST_OF_NODE_IDS_AND_TAIL_NODES_DO_NOT_MATCH: &str =
    "List of Node IDs and Edges referenced by tail Nodes do not match";
pub const MSG_MINIMUM_EARLIEST_START_TIME_PLUS_DURATION: &str =
    "(MinimumEarliestStartTime + Duration) must be greater than MaximumLatestFinishTime";
pub const MSG_NONE_OF_TARGET_RESOURCES_ARE_AVAILABLE: &str =
    "none of its target resources are available:";
pub const MSG_NO_TARGET_RESOURCES_BUT_ALL_RESOURCES_ARE_EXPLICIT_TARGETS: &str =
    "has no target resources, but every supplied resource is an explicit target";
pub const MSG_REQUIRES_ALL_TARGET_RESOURCES_BUT_SOME_NOT_AVAILABLE: &str =
    "requires all of its target resources, but the following are not available:";
pub const MSG_RESOURCES: &str = "resources";
pub const MSG_RESOURCE_SCHEDULING_STALLED: &str =
    "Resource scheduling could not make progress with the following activities:";
pub const MSG_UNABLE_TO_REMOVE_UNNECESSARY_EDGES: &str = "Unable to remove unnecessary edges";
pub const MSG_UNAVAILABLE_RESOURCES: &str = "Unavailable resources for activities:";
pub const MSG_VALUE_CANNOT_BE_NEGATIVE: &str = "Value cannot be negative";
pub const MSG_VALUE_OUTSIDE_SUPPORTED_RANGE: &str =
    "{0} is {1}, which is outside the supported range of {2} to {3}";
pub const MSG_VERTEX_GRAPH_NORMAL_NODES_WITHOUT_END_NODES: &str =
    "Vertex graph cannot contain Normal nodes without any End nodes";
pub const MSG_VERTEX_GRAPH_NORMAL_NODES_WITHOUT_START_NODES: &str =
    "Vertex graph cannot contain Normal nodes without any Start nodes";
pub const MSG_WAITING_ON_DEPENDENCIES_THAT_CAN_NEVER_COMPLETE: &str =
    "waiting on dependencies that can never complete:";
pub const MSG_WORK_STREAMS: &str = "work streams";

/// Substitutes the positional placeholders of a message template, so that
/// `args[0]` replaces `{0}`, `args[1]` replaces `{1}`, and so on.
///
/// The templates above are held verbatim as they appear in the C# resource
/// files, placeholders included, so that the rendered text matches the C#
/// original exactly; this is the counterpart of the `string.Format` calls that
/// render them there.
pub fn format_message(template: &str, args: &[&dyn std::fmt::Display]) -> String {
    let mut output = template.to_string();
    for (index, arg) in args.iter().enumerate() {
        output = output.replace(&format!("{{{index}}}"), &arg.to_string());
    }
    output
}
