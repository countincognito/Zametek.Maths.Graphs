use std::fmt;

/// What a [`GraphError`] represents.
///
/// The C# original distinguishes a scheduling stall by throwing a dedicated
/// exception type that the compilers catch. This port keeps a single error type
/// and tags it instead, so that a caller such as the vertex compiler can tell a
/// stall apart from any other failure and report it as a compilation error
/// rather than propagating it.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum GraphErrorKind {
    /// Any failure with no special handling.
    General,
    /// Resource scheduling proved that one or more activities could never be
    /// scheduled onto the supplied resources, and stopped rather than looping
    /// for ever. Reported as compilation error `C0020`.
    ResourceSchedulingStall,
}

/// The error type used across the graph libraries.
///
/// The C# original throws `InvalidOperationException`/`ArgumentException` with
/// message strings; this port surfaces the same messages through `Result`s.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphError {
    message: String,
    kind: GraphErrorKind,
}

impl GraphError {
    pub fn new(message: impl Into<String>) -> Self {
        Self {
            message: message.into(),
            kind: GraphErrorKind::General,
        }
    }

    /// Creates an error marking a resource-scheduling stall.
    pub fn resource_scheduling_stall(message: impl Into<String>) -> Self {
        Self {
            message: message.into(),
            kind: GraphErrorKind::ResourceSchedulingStall,
        }
    }

    pub fn message(&self) -> &str {
        &self.message
    }

    pub fn kind(&self) -> GraphErrorKind {
        self.kind
    }

    /// Whether this error reports that resource scheduling could make no
    /// further progress.
    pub fn is_resource_scheduling_stall(&self) -> bool {
        self.kind == GraphErrorKind::ResourceSchedulingStall
    }
}

impl fmt::Display for GraphError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(&self.message)
    }
}

impl std::error::Error for GraphError {}
