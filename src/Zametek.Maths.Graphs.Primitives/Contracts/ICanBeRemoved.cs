namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A graph element that can be flagged as removable, meaning clean-up
    /// operations (such as transitive reduction) are allowed to delete it.
    /// </summary>
    public interface ICanBeRemoved
    {
        /// <summary>
        /// Whether the element may be removed during graph clean-up.
        /// </summary>
        bool CanBeRemoved { get; }

        /// <summary>
        /// Flags the element as not removable.
        /// </summary>
        void SetAsReadOnly();

        /// <summary>
        /// Flags the element as removable.
        /// </summary>
        void SetAsRemovable();
    }
}
