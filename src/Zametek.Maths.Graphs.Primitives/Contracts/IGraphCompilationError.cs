namespace Zametek.Maths.Graphs
{
    public interface IGraphCompilationError
    {
        GraphCompilationErrorCode ErrorCode { get; }

        string ErrorMessage { get; }
    }
}
