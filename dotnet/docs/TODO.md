# TODO

Work identified but not yet undertaken. Each entry records what was observed, why it matters, and what a fix would need to establish - so the investigation can start from evidence rather than from scratch.

## Priority-list calculation scales cubically

`CalculateCriticalPathPriorityList` (in both `VertexGraphBuilder` and `ArrowGraphBuilder`) appends exactly **one** activity to the priority list per iteration: it runs a full `CalculateCriticalPath()`, takes the first activity on the current critical path, zeroes that activity's duration, and repeats until every activity has been placed. Compile time is dominated by this loop for anything beyond a few hundred activities.

Measured on a layered DAG (each activity depending on two activities in the previous layer), 20 resources, Release build, through the vertex compiler:

| Activities | Compile time | Allocated (churn) | Live heap |
| - | - | - | - |
| 250 | 0.31 s | 41 MB | 6 MB |
| 500 | 0.68 s | 166 MB | 7 MB |
| 1,000 | 1.2 s | 580 MB | 10 MB |
| 2,000 | 6.9 s | 2.8 GB | 16 MB |
| 4,000 | 47.3 s | 15.1 GB | 34 MB |
| 6,000 | 173 s | 44.4 GB | 60 MB |
| 8,000 | 413 s | 91.4 GB | 75 MB |

That is about 8.7x per doubling at the top end - close to cubic. Live heap stays tiny throughout, so this is not a memory problem: it is repeated work and the allocation churn that goes with it.

The cost is the product of two independent factors.

**The loop runs once per activity.** Each iteration performs a complete critical-path calculation in order to place a single activity.

**Each critical-path pass is label-correcting rather than topological.** `VertexCriticalPathEngine.CalculateCriticalPathForwardFlow` sweeps repeatedly over the remaining edges: on every sweep it materialises `remainingEdgeIds.ToList()`, walks the whole list, completes whichever edges now have satisfied dependencies, and repeats until none remain. Each sweep costs O(E) and allocates a fresh list, and the number of sweeps is bounded by the depth of the graph, so a single pass is O(depth x E) rather than O(V + E). The backward flow has the same shape.

Holding the activity count fixed at 1,500 and varying only the depth confirms the second factor directly:

| Layers (depth) | Activities per layer | Compile time | Allocated |
| - | - | - | - |
| 10 | 150 | 1,961 ms | 1,203 MB |
| 30 | 50 | 1,922 ms | 1,372 MB |
| 60 | 25 | 2,617 ms | 1,556 MB |
| 120 | 12 | 4,480 ms | 1,996 MB |
| 240 | 6 | 7,114 ms | 2,750 MB |

Same graph size throughout; 3.6x the time from depth alone. The roughly 1.9 s floor at depth 10 is the per-iteration cost that remains once depth is small - the iteration count itself, plus per-iteration overhead such as re-enumerating `Activities` and the `Min` / `Where` / `OrderBy` / `ToList` selection that computes a full ordering but uses only its first element.

Note that this makes the deferred topological-CPM item (below) and this item the *same* bottleneck rather than two separate ones: making a critical-path pass O(V + E) removes the depth factor from every one of the per-activity iterations at once.

An earlier version of this entry attributed the cost to a transitive reduction being recomputed inside `CalculateCriticalPath()`. That is wrong for the vertex path, where `RemoveRedundantEdges()` and `RedirectEdges()` are documented no-ops; the measurements above are all from the vertex compiler. It does apply to the **arrow** path, where both methods do real work through the dummy-edge orchestrator, so the arrow builder carries this extra factor per iteration on top of the two above. The arrow path has not been benchmarked separately yet, and there the observation still holds that the graph structure does not change between iterations - only one activity's duration does - so a reduction that depends solely on structure is being recomputed identically every time and should be hoistable.

### Approach

Phase 0 (**done**) establishes the safety net before any production change: the priority list determines resource assignment and therefore the final schedule, so every change has to be output-identical rather than merely close. `PriorityListCorpus` generates a deterministic corpus of 56 graphs across a range of shapes - chains, fans, layered, random, diamonds, disconnected, plus variants with time constraints, zero-duration activities and deliberately tied durations - using its own xorshift generator so the corpus stays reproducible across runtimes. `PriorityListEquivalenceTests` compares the resulting priority lists against the committed baseline in `Compilers/TestFiles/PriorityListBaseline.txt`, and additionally asserts two invariants that must hold however the calculation is implemented: the list is unaffected by `ShuffleProcessingOrder`, and every non-dummy activity appears exactly once.

The baseline test was verified to have teeth by swapping a single pair of activity ids in the baseline file and confirming the test failed and named the affected case.

The same file carries an explicit-only timing harness. Explicit tests are most reliably run through the xUnit v3 test assembly directly, after a Release build:

```
.\Zametek.Maths.Graphs.Compilers.Tests.exe -explicit only -method "*MeasurePriorityListScaling*" -showLiveOutput
```

Its Phase 0 reference measurements, for `CalculateCriticalPathPriorityList` alone rather than a whole compile:

| Activities (12 layers) | Time | Allocated |
| - | - | - |
| 250 | 200 ms | 40 MB |
| 500 | 815 ms | 131 MB |
| 1,000 | 871 ms | 524 MB |
| 2,000 | 2,370 ms | 2,121 MB |

| Layers, at 1,500 activities | Time | Allocated |
| - | - | - |
| 10 | 1,102 ms | 1,259 MB |
| 30 | 1,739 ms | 1,437 MB |
| 60 | 2,980 ms | 1,689 MB |
| 120 | 3,701 ms | 1,949 MB |
| 240 | 4,292 ms | 2,010 MB |

Regenerate the baseline only when the output is *intended* to change, with `-explicit only -method "*RegenerateBaseline*"`; it writes back into the source tree.

Phase 1 replaces the label-correcting sweeps with a topological-order calculation (Kahn's algorithm, then one forward and one reverse pass), taking a critical-path pass from O(depth x E) to O(V + E). Two things need care: the `ShuffleProcessingOrder` hook exists to prove order-independence and must survive, by shuffling the ready queue rather than the edge list; and the constraint clamping needs checking for whether it can ever require re-relaxing an already-completed edge. The current code cannot revisit one - `completedEdgeIds` is add-only - which is good evidence that a single topological pass is equivalent, but it should be confirmed rather than assumed.

Phase 2 removes per-iteration overhead that does not change the asymptotics but accounts for much of the allocation churn: single-pass selection of the most critical activity instead of `Min` + `Where` + `OrderBy` + `ToList`, and avoiding repeated re-enumeration of `Activities`.

Phase 3, only if still needed after measuring, attacks the iteration count itself. Recomputing incrementally is sound in principle, since zeroing one duration only affects that activity's descendants in the forward pass and its ancestors in the backward pass. Collecting a whole critical path per iteration instead of a single activity would cut the iteration count by the average path length, but is **not** obviously equivalent - zeroing the first activity changes the slack of the others - so it would need both an argument and corpus evidence, or to be rejected.

Any change here must preserve the exact priority ordering, since that ordering determines resource assignment and therefore the final schedule. `CompileBehaviourTests` (order-independence across 50 random DAGs) and the golden tests in both the C# and Rust suites are the regression net; a benchmark harness comparing before/after priority lists on random graphs would be worth building first.

This is the change that would move the usable ceiling. The `GraphLimits.MaximumActivityCount` limit of 2,000 currently bounds what is *accepted*; it does not make large graphs fast, and raising it would not be sensible until this is addressed.

## Rust port parity for the scheduling guardrails

The dotnet side has scheduler stall detection with skip-ahead (`C0020`), the input self-consistency probe (`P0070`), the domain limits (`P0080`, `GraphLimits`), mandatory cancellation tokens, and bit-packed allocation streams. None of this has been ported to `rust/` yet. The port's golden tests mirror the C# suite verbatim, so the ported behaviour needs to match message-for-message where the tests assert on content.

## Recursive dummy-edge ordering on the arrow construction path

`DummyEdgeOrchestrator.GetEdgesInDescendingOrder` is still recursive, unlike the transitive reducers and dependency walks which were made iterative. It sits on the arrow edge-cleanup path rather than the analysis path, so it has not been a problem in practice, but it remains a stack-depth risk on very deep arrow graphs.

## Full topological CPM rewrite (deferred from the performance work)

The critical-path engines use label-correcting passes. A full O(V+E) topological restructure was scoped during the performance work and deliberately deferred as higher risk, wanting both a benchmark harness and a dedicated review to prove it stays behaviour-identical. See `PERFORMANCE.md` for the original analysis.
