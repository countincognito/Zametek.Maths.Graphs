namespace Zametek.Maths.Graphs
{
    public enum GraphCompilationErrorCode
    {
        C0010, // Missing dependencies.
        C0020, // Circular dependencies.
        C0030, // Invalid constraints.
        C0040, // All resources are marked as explicit targets, but not all activities have targeted resources.
        C0050 // Unable to remove unnecessary edges.
    }
}
