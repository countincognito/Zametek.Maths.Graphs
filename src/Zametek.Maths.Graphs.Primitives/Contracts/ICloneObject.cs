namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A thing that can produce a deep copy of itself.
    /// </summary>
    public interface ICloneObject
    {
        /// <summary>
        /// Creates a deep copy of this instance.
        /// </summary>
        /// <returns>The copy, which the caller casts back to the concrete type.</returns>
        object CloneObject();
    }
}
