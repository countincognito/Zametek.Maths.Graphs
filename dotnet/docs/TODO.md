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

Phase 1 (**done**) replaced the label-correcting sweeps in `VertexCriticalPathEngine` with a walk over nodes in dependency order, taking a critical-path pass from O(depth x E) to O(V + E).

The equivalence argument rests on two observations. An edge can be completed once every incoming edge of its tail node is complete (forward flow; the mirror holds backward), and the value the edge receives depends only on that node - so all outgoing edges of a node receive the *same* earliest finish time, and all incoming edges of a node the same latest finish time. The per-edge search was therefore really a per-node computation, and visiting each node once as it becomes ready produces identical values while removing the repeated sweeping. Every value-computing statement was kept verbatim, including the places where Start and End nodes are clamped differently from the rest; only the driver changed.

Two traps worth recording. `Node.IncomingEdges` throws for Start and Isolated nodes, and `Node.OutgoingEdges` throws for End and Isolated nodes, rather than returning empty sets - the old code never tripped this because it only ever reached nodes through edges, whereas a node-driven walk enumerates `state.Nodes` directly and must filter by `NodeType` first. And the `ShuffleProcessingOrder` hook was preserved by processing ready nodes in rounds and shuffling each round, which keeps its meaning: within a round the order genuinely cannot matter.

Measured effect. Depth dependence is gone - at 1,500 activities the time was 1,102 ms at depth 10 rising to 4,292 ms at depth 240, and is now essentially flat, ending *lower* at depth 240 than at depth 10 because deeper layers are narrower:

| Layers, at 1,500 activities | Before | After |
| - | - | - |
| 10 | 1,102 ms | 1,335 ms |
| 30 | 1,739 ms | 1,323 ms |
| 60 | 2,980 ms | 1,316 ms |
| 120 | 3,701 ms | 1,038 ms |
| 240 | 4,292 ms | 861 ms |

On the original benchmark shape, where depth grows with size, the gain grows with it. The "before" column is a full compile and the "after" column the priority-list calculation alone, which is a fair comparison because the priority list is where effectively all of that time goes - at 2,000 activities the full compile now takes 2.40 s of which 2.34 s is the priority list:

| Activities | Depth | Before | After | Allocated, before | Allocated, after |
| - | - | - | - | - | - |
| 1,000 | 40 | 1.2 s | 1.22 s | 580 MB | 592 MB |
| 2,000 | 80 | 6.9 s | 2.34 s | 2.8 GB | 2.4 GB |
| 4,000 | 160 | 47.3 s | 12.29 s | 15.1 GB | 9.3 GB |
| 8,000 | 320 | 413 s | 76.41 s | 91.4 GB | 37.7 GB |

Shallow graphs gain little, as expected - there was no depth factor to remove - and at very low depth the new form is marginally slower, because it allocates two dictionaries of pending counts per critical-path pass where the old form allocated one list per sweep. That is a Phase 2 target.

What remains is the iteration count and a large constant factor. At 8,000 activities the calculation still performs roughly 8,000 passes of about 24,000 node and edge visits, which is around 2 x 10^8 elementary steps - a second or two of actual work, against a measured 76 s. The gap is per-visit overhead: LINQ iterators allocated per node in the hot path (`Select(...).Max(...)`, `Select(...).Min(...)`), dictionary lookups through `state.Edge`, `EdgeHeadNode` and `EdgeTailNode`, the two pending-count dictionaries per pass, and the per-iteration work in the priority-list loop itself. 37.7 GB across 8,000 passes is about 4.7 MB per pass, which is far more than the algorithm needs and is what Phase 2 should target before Phase 3 is considered.

Note that the compile-level tables above 2,000 activities can no longer be reproduced through `Compile`, since `GraphLimits.MaximumActivityCount` now rejects such graphs with P0080; measure through `CalculateCriticalPathPriorityList` on the builder instead, as the timing harness does.

Phase 2 (**done**) removed per-iteration overhead. None of it changes the asymptotics; together it accounted for most of the allocation churn.

The largest item was not on the original list. Each critical-path pass built two hash sets covering every edge in the graph - one of completed edges, one of remaining edges - purely to answer "is this edge done yet". With the node-order walk of Phase 1 that question has a cheaper answer: an edge is complete exactly when the node at its tail (forward) or head (backward) has been processed, so each node need only count how many of its own edges are outstanding and decrement as they complete. The counts start as the node's edge count, need no per-edge lookup to build, and the cycle check becomes a single integer reaching zero. Two edge-sized hash sets per pass, times a pass per activity, was the dominant allocation.

The rest:

- The LINQ chains on the hot path (`Select(...).Max(...)`, `Select(...).Min(...)`) allocated an enumerator and a closure per node per pass; they are now explicit loops. The replacements return zero for an empty edge set, matching the `DefaultIfEmpty` the Start and End node passes relied on.
- The priority-list selection ordered every candidate activity and then used only the first. It now compares as it goes. `Comparer<int?>.Default` is used deliberately, because that is the comparer `OrderBy` would have used, so an activity with no earliest start time still sorts ahead of one that has a value.
- `FindInvalidPreCompilationConstraints` materialised a list of every activity on each call - once per pass - although the checker only enumerates it once. `ConstraintChecker` now takes a sequence.

Measured, for the priority-list calculation alone:

| Activities (12 layers) | Phase 1 | Phase 2 | Allocated, Phase 1 | Allocated, Phase 2 |
| - | - | - | - | - |
| 250 | 177 ms | 130 ms | 48 MB | 8 MB |
| 500 | 599 ms | 296 ms | 163 MB | 27 MB |
| 1,000 | 850 ms | 770 ms | 623 MB | 104 MB |
| 2,000 | 2,309 ms | 1,477 ms | 2,379 MB | 416 MB |

| Layers, at 1,500 activities | Phase 1 | Phase 2 |
| - | - | - |
| 10 | 1,335 ms | 669 ms |
| 60 | 1,316 ms | 677 ms |
| 240 | 861 ms | 520 ms |

On the realistic shape, where depth grows with size, measured against the numbers this investigation started from (those were full compiles, but the priority list accounts for effectively all of that time):

| Activities | Depth | Start of investigation | After Phase 2 | Allocated, before | Allocated, after |
| - | - | - | - | - | - |
| 1,000 | 40 | 1.2 s | 0.88 s | 580 MB | 91 MB |
| 2,000 | 80 | 6.9 s | 1.54 s | 2.8 GB | 375 MB |
| 4,000 | 160 | 47.3 s | 5.96 s | 15.1 GB | 1.4 GB |
| 8,000 | 320 | 413 s | 44.4 s | 91.4 GB | 5.7 GB |

That is roughly 9x faster and 16x less allocation at 8,000 activities, and 4.5x faster at 2,000. Timings at the top end vary by several seconds between runs as the garbage collector reacts to the remaining churn, so treat 8,000 as "about 40 s" rather than a precise figure.

What remains is dominated by the pending-count dictionaries, one per flow per pass. Removing those entirely would mean either checking readiness by rescanning a node's edges - which is cheap for sparse graphs but O(indegree) per completed edge, and so worse for the dense ones in the corpus - or giving nodes a scratch field, which puts transient algorithm state on a shared primitive type. Neither looked worth it against the remaining gain, so Phase 2 stops here; the iteration count that Phase 3 targets is now much the larger factor.

Phase 3, only if still needed after measuring, attacks the iteration count itself. Recomputing incrementally is sound in principle, since zeroing one duration only affects that activity's descendants in the forward pass and its ancestors in the backward pass. Collecting a whole critical path per iteration instead of a single activity would cut the iteration count by the average path length, but is **not** obviously equivalent - zeroing the first activity changes the slack of the others - so it would need both an argument and corpus evidence, or to be rejected.

Any change here must preserve the exact priority ordering, since that ordering determines resource assignment and therefore the final schedule. `CompileBehaviourTests` (order-independence across 50 random DAGs) and the golden tests in both the C# and Rust suites are the regression net; a benchmark harness comparing before/after priority lists on random graphs would be worth building first.

This is the change that would move the usable ceiling. The `GraphLimits.MaximumActivityCount` limit of 2,000 currently bounds what is *accepted*; it does not make large graphs fast, and raising it would not be sensible until this is addressed.

## Rust port parity for the scheduling guardrails

The dotnet side has scheduler stall detection with skip-ahead (`C0020`), the input self-consistency probe (`P0070`), the domain limits (`P0080`, `GraphLimits`), mandatory cancellation tokens, and bit-packed allocation streams. None of this has been ported to `rust/` yet. The port's golden tests mirror the C# suite verbatim, so the ported behaviour needs to match message-for-message where the tests assert on content.

## Recursive dummy-edge ordering on the arrow construction path

`DummyEdgeOrchestrator.GetEdgesInDescendingOrder` is still recursive, unlike the transitive reducers and dependency walks which were made iterative. It sits on the arrow edge-cleanup path rather than the analysis path, so it has not been a problem in practice, but it remains a stack-depth risk on very deep arrow graphs.

## Full topological CPM rewrite (deferred from the performance work)

The critical-path engines use label-correcting passes. A full O(V+E) topological restructure was scoped during the performance work and deliberately deferred as higher risk, wanting both a benchmark harness and a dedicated review to prove it stays behaviour-identical. See `PERFORMANCE.md` for the original analysis.
