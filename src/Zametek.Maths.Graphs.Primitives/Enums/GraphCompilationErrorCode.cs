namespace Zametek.Maths.Graphs
{
    public enum GraphCompilationErrorCode
    {
        P0010, // Missing dependencies.
        P0020, // Circular dependencies.
        P0030, // Invalid precompilation constraints.
        P0040, // All resources are marked as explicit targets, but not all activities have targeted resources.
        P0050, // Unable to remove unnecessary edges.
        C0010 // Invalid postcompilation constraints.
    }
}
