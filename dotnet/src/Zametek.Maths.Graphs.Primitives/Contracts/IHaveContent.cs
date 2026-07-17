namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A thing that carries a content payload (e.g. a node or edge carrying an
    /// activity or event).
    /// </summary>
    /// <typeparam name="T">The content type.</typeparam>
    public interface IHaveContent<out T>
    {
        /// <summary>
        /// The content payload.
        /// </summary>
        T Content { get; }
    }
}
