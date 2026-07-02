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

    /// <summary>
    /// A thing that can produce a strongly-typed deep copy of itself, avoiding
    /// the cast that <see cref="ICloneObject.CloneObject"/> requires.
    /// </summary>
    /// <typeparam name="T">The type the copy is returned as.</typeparam>
    public interface ICloneObject<out T>
        : ICloneObject
    {
        /// <summary>
        /// Creates a deep copy of this instance.
        /// </summary>
        T Clone();
    }
}
