//! Domain sanity limits - the counterpart of the C# `GraphLimits`.
//!
//! These are not algorithmic requirements - they are bounds chosen so that
//! absurd or corrupted input is rejected with a clear compilation error instead
//! of consuming unbounded time and memory. Values within these limits are
//! accepted; a compilation that violates one reports `P0080` (declared values
//! and counts) or `C0020` (a computed schedule that runs past the horizon).
//!
//! Why a horizon limit exists at all: each resource schedule retains five
//! per-time-unit allocation streams (resource, cost, billing, effort,
//! activity), so the memory held by a compilation grows as
//! (horizon x resources), independently of how many activities there are.
//!
//! Consumers can use these to validate user input before compiling, so that
//! out-of-range values are rejected at the point of entry rather than at
//! compile time.

/// The largest accepted value for any time-valued input or computed schedule
/// time (durations, `minimum_earliest_start_time`,
/// `maximum_latest_finish_time`, `minimum_free_slack`, and the compiled
/// schedule's finish time). At one time unit per day this is roughly 270 years,
/// far beyond any real plan.
pub const MAXIMUM_TIME_VALUE: i32 = 100_000;

/// The smallest accepted value for any time-valued input. Negative times are
/// never meaningful, so they are rejected up front rather than after
/// compilation.
pub const MINIMUM_TIME_VALUE: i32 = 0;

/// The largest accepted number of activities in a graph. Compilation cost grows
/// steeply with activity count (the priority-list calculation dominates), so
/// this bounds a compile to a workable size rather than describing an
/// algorithmic ceiling.
pub const MAXIMUM_ACTIVITY_COUNT: usize = 2_000;

/// The largest accepted number of resources supplied to a compilation.
/// Resources multiply the memory held by the per-time-unit allocation streams,
/// so this bound works together with [`MAXIMUM_TIME_VALUE`].
pub const MAXIMUM_RESOURCE_COUNT: usize = 1_000;

/// The largest accepted number of work streams supplied to a compilation.
pub const MAXIMUM_WORK_STREAM_COUNT: usize = 100;
