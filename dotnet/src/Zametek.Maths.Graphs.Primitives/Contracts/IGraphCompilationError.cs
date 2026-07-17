namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A single problem found during graph compilation.
    /// </summary>
    public interface IGraphCompilationError
    {
        /// <summary>
        /// The code categorising the error.
        /// </summary>
        GraphCompilationErrorCode ErrorCode { get; }

        /// <summary>
        /// A human-readable description listing the specific activities or
        /// resources involved.
        /// </summary>
        string ErrorMessage { get; }
    }
}
