namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// The position of a node within a directed graph.
    /// </summary>
    public enum NodeType
    {
        /// <summary>
        /// A node with both incoming and outgoing edges.
        /// </summary>
        Normal,

        /// <summary>
        /// A node with only outgoing edges.
        /// </summary>
        Start,

        /// <summary>
        /// A node with only incoming edges.
        /// </summary>
        End,

        /// <summary>
        /// A node with no edges at all.
        /// </summary>
        Isolated
    }
}
