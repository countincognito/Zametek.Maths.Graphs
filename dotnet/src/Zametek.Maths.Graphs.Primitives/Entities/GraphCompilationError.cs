namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Default implementation of <see cref="IGraphCompilationError"/>.
    /// </summary>
    public class GraphCompilationError
        : IGraphCompilationError
    {
        #region Ctors

        /// <summary>
        /// Creates an error with the given code and an empty message.
        /// </summary>
        public GraphCompilationError(GraphCompilationErrorCode errorCode)
        {
            ErrorCode = errorCode;
            ErrorMessage = string.Empty;
        }

        /// <summary>
        /// Creates an error with the given code and message.
        /// </summary>
        public GraphCompilationError(
            GraphCompilationErrorCode errorCode,
            string errorMessage)
            : this(errorCode)
        {
            ErrorMessage = errorMessage;
        }

        #endregion

        #region IGraphCompilationError Members

        /// <inheritdoc/>
        public GraphCompilationErrorCode ErrorCode
        {
            get;
        }

        /// <inheritdoc/>
        public string ErrorMessage
        {
            get;
        }

        #endregion
    }
}
