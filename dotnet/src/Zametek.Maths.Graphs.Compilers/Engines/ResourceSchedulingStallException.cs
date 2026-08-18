using System;

namespace Zametek.Maths.Graphs
{
    // Raised by resource scheduling engines when the scheduling loop can prove that no
    // further progress is possible - i.e. at least one remaining activity can never be
    // scheduled onto the supplied resources. The graph compilers catch this and convert
    // it into a C0020 compilation error; it only escapes to callers that invoke an
    // engine directly, which is why it derives from InvalidOperationException.
    /// <summary>
    /// Indicates that resource scheduling stalled because one or more activities could
    /// never be scheduled onto the supplied resources.
    /// </summary>
    internal sealed class ResourceSchedulingStallException
        : InvalidOperationException
    {
        internal ResourceSchedulingStallException(string message)
            : base(message)
        {
        }
    }
}
