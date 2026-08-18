# TODO

Work identified but not yet undertaken. Each entry records what was observed, why it matters, and what a fix would need to establish - so the investigation can start from evidence rather than from scratch.

## Priority-list calculation scales cubically

`CalculateCriticalPathPriorityList` (in both `VertexGraphBuilder` and `ArrowGraphBuilder`) appends exactly **one** activity to the priority list per iteration: it runs a full `CalculateCriticalPath()`, takes the first activity on the current critical path, zeroes that activity's duration, and repeats. Since `CalculateCriticalPath()` itself calls `RemoveRedundantEdges()`, every iteration performs a complete transitive reduction - which builds an ancestor bitset costing O(N^2 / 64) in both time and allocation. The result is roughly O(A x N^2), and it dominates compile time for anything beyond a few hundred activities.

Measured on a layered DAG (each activity depending on two activities in the previous layer), 20 resources, Release build:

| Activities | Compile time | Allocated (churn) | Live heap |
| - | - | - | - |
| 250 | 0.31 s | 41 MB | 6 MB |
| 500 | 0.68 s | 166 MB | 7 MB |
| 1,000 | 1.2 s | 580 MB | 10 MB |
| 2,000 | 6.9 s | 2.8 GB | 16 MB |
| 4,000 | 47.3 s | 15.1 GB | 34 MB |
| 6,000 | 173 s | 44.4 GB | 60 MB |
| 8,000 | 413 s | 91.4 GB | 75 MB |

That is about 8.7x per doubling at the top end - close to cubic. Note that live heap stays tiny throughout: this is not a memory problem, it is repeated work and allocation churn. The 91 GB at 8,000 activities is 8,000 transitive reductions, each allocating and discarding a fresh ancestor bitset.

Why it looks tractable: between consecutive iterations the graph changes by exactly one activity's duration being set to zero. The graph *structure* does not change at all, so the transitive reduction - which depends only on structure - is recomputed identically every time. Candidate approaches, roughly in increasing order of ambition: hoist the reduction out of the loop entirely (it should be computable once); avoid recomputing the whole critical path when only one duration changed; or collect a whole critical path per iteration rather than a single activity, which would cut the iteration count by the average path length.

Any change here must preserve the exact priority ordering, since that ordering determines resource assignment and therefore the final schedule. `CompileBehaviourTests` (order-independence across 50 random DAGs) and the golden tests in both the C# and Rust suites are the regression net; a benchmark harness comparing before/after priority lists on random graphs would be worth building first.

This is the change that would move the usable ceiling. The `GraphLimits.MaximumActivityCount` limit of 2,000 currently bounds what is *accepted*; it does not make large graphs fast, and raising it would not be sensible until this is addressed.

## Rust port parity for the scheduling guardrails

The dotnet side has scheduler stall detection with skip-ahead (`C0020`), the input self-consistency probe (`P0070`), the domain limits (`P0080`, `GraphLimits`), mandatory cancellation tokens, and bit-packed allocation streams. None of this has been ported to `rust/` yet. The port's golden tests mirror the C# suite verbatim, so the ported behaviour needs to match message-for-message where the tests assert on content.

## Recursive dummy-edge ordering on the arrow construction path

`DummyEdgeOrchestrator.GetEdgesInDescendingOrder` is still recursive, unlike the transitive reducers and dependency walks which were made iterative. It sits on the arrow edge-cleanup path rather than the analysis path, so it has not been a problem in practice, but it remains a stack-depth risk on very deep arrow graphs.

## Full topological CPM rewrite (deferred from the performance work)

The critical-path engines use label-correcting passes. A full O(V+E) topological restructure was scoped during the performance work and deliberately deferred as higher risk, wanting both a benchmark harness and a dedicated review to prove it stays behaviour-identical. See `PERFORMANCE.md` for the original analysis.
