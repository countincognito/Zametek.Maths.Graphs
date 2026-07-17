use std::fmt;

/// The error type used across the graph libraries.
///
/// The C# original throws `InvalidOperationException`/`ArgumentException` with
/// message strings; this port surfaces the same messages through `Result`s.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GraphError {
    message: String,
}

impl GraphError {
    pub fn new(message: impl Into<String>) -> Self {
        Self {
            message: message.into(),
        }
    }

    pub fn message(&self) -> &str {
        &self.message
    }
}

impl fmt::Display for GraphError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(&self.message)
    }
}

impl std::error::Error for GraphError {}
