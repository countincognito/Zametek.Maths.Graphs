namespace Zametek.Maths.Graphs
{
    public class GraphCompilationError
        : IGraphCompilationError
    {
        #region Ctors

        public GraphCompilationError(GraphCompilationErrorCode errorCode)
        {
            ErrorCode = errorCode;
            ErrorMessage = string.Empty;
        }

        public GraphCompilationError(
            GraphCompilationErrorCode errorCode,
            string errorMessage)
            : this(errorCode)
        {
            ErrorMessage = errorMessage;
        }

        #endregion

        #region IGraphCompilationError Members

        public GraphCompilationErrorCode ErrorCode
        {
            get;
        }

        public string ErrorMessage
        {
            get;
        }

        #endregion
    }
}
