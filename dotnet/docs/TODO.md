# TODO

Work identified during investigation, with what was observed, why it matters, and what a fix would need to establish - so anything picked up later starts from evidence rather than from scratch. Entries are marked done as they are undertaken, and keep their measurements.

In rough order of remaining value:

1. **Rust port parity** - underway. The scheduling livelock and the domain limits are done; bit-packing and the topological walk remain. The largest outstanding item.
2. **Priority-list Phase 3** - investigated and designed, deliberately not implemented. Its value is conditional on raising `GraphLimits.MaximumActivityCount`; see the reassessment at the end of that section.
3. **Recursive dummy-edge ordering** - a latent stack-depth risk on the arrow construction path.
4. **Topological CPM for the arrow engine** - low priority, because arrow runs one pass per compile rather than one per activity.

## Priority-list calculation (was cubic; Phases 0 to 2 done)

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

Phase 3 attacks the iteration count itself. It has been **investigated but not implemented**; the findings below revise what was originally proposed, in both directions.

**Collecting a whole critical path per iteration is not equivalent, and is rejected.** A counter-example settles it. Take A (duration 10) and B (duration 1) with no dependencies, and C (duration 1) depending on both. On the first pass the slack is 0 for A, 9 for B and 0 for C, so the critical set in earliest-start order is {A, C}; taking it whole would place A, C, B. Taking one activity at a time places A, then re-evaluates - and with A zeroed, B has become critical too - giving A, B, C. Verified against the implementation, which produces A, B, C.

**Recomputing incrementally is viable, and the objection raised against it was wrong.** The concern was that zeroing an activity moves the project finish time, from which every latest-finish time is measured, forcing a full backward pass anyway. Measured, the finish time moves in a *decreasing* share of iterations as the graph grows, because the number of moves is bounded by the length of the critical path rather than by the activity count:

| Activities | Iterations | Iterations where the finish time moved |
| - | - | - |
| 250 | 250 | 82 (33%) |
| 500 | 500 | 91 (18%) |
| 1,000 | 1,000 | 94 (9%) |
| 2,000 | 2,000 | 98 (5%) |

**Most of each pass is wasted work.** Counting how many activities actually take a new value between consecutive passes:

| Activities | Earliest start times changed, per iteration | Latest finish times changed | Share of the graph |
| - | - | - | - |
| 250 | 18.3 | 67.0 | 17.1% |
| 500 | 20.6 | 81.7 | 10.2% |
| 1,000 | 19.4 | 90.7 | 5.5% |
| 2,000 | 19.3 | 98.8 | 3.0% |

The absolute figures barely move as the graph grows, so the share falls roughly as 1/N: at 2,000 activities a pass recomputes 2,000 activities in order to change about 118 of them, and around 97% of the work is discarded. Propagating only where values actually change would make the per-iteration cost roughly independent of graph size, turning the whole calculation from about O(N^2) into about O(N) on this family of graphs - on the order of 15x to 20x at 2,000 activities, and more above that.

The cost is concentrated in the right place for this to pay off: of a single pass at 4,000 activities, 91% is the two flows themselves, 7% is `ClearCriticalPathVariables` and 2% the pre-compilation constraint check.

### What implementing it would involve

The intricacy is in preserving behaviour exactly, and the risk is that an incremental path and the full path drift apart.

- The per-node value computations - earliest start, the earliest finish given to outgoing edges, latest finish, free slack, each with its clamping, and with Start, End and Isolated nodes clamped differently - would need factoring into helpers shared by the full and incremental paths, so the arithmetic exists once.
- Propagation must run in topological order and stop wherever a recomputed value equals the old one; that early stop is the entire saving.
- Free slack depends on the successors' earliest start times as well as on the node's own values, so it needs tracking separately from the two flows.
- The current passes clear every value and recompute, which makes the "only assign if not already set" guards behave as first-write-wins. Incremental updates rewrite existing values, so those guards would need restructuring rather than reusing.
- When the finish time does move, fall back to a full recompute. At 5% of iterations that costs little.
- The engine interface is currently stateless whole-graph recomputation. An incremental entry point (recalculate after a single duration change) is new public surface on `IVertexCriticalPathEngine`, and only the priority-list calculation would use it.

### Why it was not implemented

`GraphLimits.MaximumActivityCount` is 2,000. At exactly that ceiling the priority-list calculation now takes about 1.5 seconds, down from 6.9 seconds; Phase 3 would take it to roughly a tenth of a second. Everything above 2,000 - where the advantage grows to fifteen-fold and beyond - is rejected by `P0080` before scheduling begins. Real plans sit far below the limit in any case: the production graph that prompted this whole investigation had 39 activities, which compiles in milliseconds and always did.

So Phase 3 would currently be optimising a region the limits forbid, for a size nobody runs. Its real value is conditional: **it is the enabler if the activity limit is ever to be raised.** With it, 8,000 activities becomes a few seconds instead of around 44, and a limit of 5,000 to 10,000 becomes defensible. Without it, 2,000 is the honest ceiling. Revisit this if raising the limit is ever wanted; everything needed to pick it up - the corpus, the harness, the measurements, the design and the two rejected alternatives - is recorded above.

Any change here must preserve the exact priority ordering, since that ordering determines resource assignment and therefore the final schedule. `CompileBehaviourTests` (order-independence across 50 random DAGs) and the golden tests in both the C# and Rust suites are the regression net; a benchmark harness comparing before/after priority lists on random graphs would be worth building first.

This is the change that would move the usable ceiling. The `GraphLimits.MaximumActivityCount` limit of 2,000 currently bounds what is *accepted*; it does not make large graphs fast, and raising it would not be sensible until this is addressed.

## Rust port parity

The `rust/` port mirrors the C# code as it stood before any of the recent work, and is being brought forward one self-contained item at a time. Its golden tests are mirrored copies rather than shared, so it passes against its own behaviour; the divergence is real but latent, and it widened with each change. This is still the largest outstanding item in the repo.

Its critical-path engines still sweep with a `progress` flag (`compilers/src/vertex/cpm.rs`, `compilers/src/arrow/cpm.rs`), and its allocation streams are `Vec<bool>`, which like the C# `List<bool>` spends a byte per flag (`primitives/src/schedule.rs`).

### Behavioural divergence - the port now computes different results

- **Scheduler stall detection, skip-ahead and the time horizon (`C0020`) - DONE.** The port did carry the livelock that started this investigation, and it was confirmed rather than assumed: temporarily restoring `time_counter += 1` made the ported guardrail tests hang until their 30-second watchdog fired. `next_tick_of_interest`, `build_stall_message` and `describe_unschedulable_activity` are now ported to `compilers/src/scheduling/scheduler.rs`, with the horizon guards in both scheduling attempts computed in `i64`. Since Rust has no exceptions, `GraphError` gained a `GraphErrorKind` discriminant so `VertexGraphCompiler` can tell a stall from any other failure and report `C0020` in place of it; the computed-schedule horizon check after the second CPM pass reports `C0020` too. `graph_limits` was added to primitives at the same time, since the horizon guard needs `MAXIMUM_TIME_VALUE` - constants only, with the `P0080` validation still to come. The error-code discriminants are now pinned to the C# values, leaving 7 free for `P0070`. 16 tests ported (`scheduler_time_gate_tests.rs`, `scheduler_guardrail_tests.rs`); suite 361 to 377 green. The six time-gate tests reproduce the C# expected values exactly, which is what demonstrates the skip-ahead did not change observable behaviour.
- **Domain limits and `P0080` - DONE.** `compilers/src/limit_checker.rs` mirrors the C# `LimitChecker` (three count checks, then each activity's four time values), reported as `P0080` from `add_pre_compilation_errors`. That method gained a `work_streams` parameter, since the work-stream count is one of the three counts and the Rust signature had no way to see them - a small public API change on `VertexGraphBuilder`. Two of the ported tests assert the complete rendered message rather than a substring, which is what pins the text to the C# resource strings; the `{0}`-style templates are now rendered through a single `messages::format_message` taking a slice of arguments, replacing the earlier `format1`/`format2` pair. As in C#, this is vertex-only: `ArrowGraphBuilder` does not reference `LimitChecker` there either, despite what the comment at the top of that file claims. 11 tests ported (`graph_limit_tests.rs`); suite 377 to 387 green, and no existing test tripped the new caps - the 20,000-activity chains in `deep_graph_tests.rs` go through the builder rather than the compiler, so they are unaffected, exactly as on the C# side.
- **`P0070`, the input self-consistency probe.** Recommend **not** porting, and the stall-detection work followed that recommendation. It detects a `HashSet` whose lookups disagree with its contents, which arises from unsynchronized concurrent mutation - a state safe Rust cannot produce, since a collection cannot be mutated from two threads without synchronization and the compiler enforces it. Porting it would mean writing a check for a condition that cannot occur, and no test could construct the input to exercise it. For the same reason, the corresponding branch of `describe_unschedulable_activity` (which probes the target resource set when everything else checks out) is absent from the port, and the three corrupted-`HashSet` tests were not ported.
- **Mandatory cancellation tokens.** No natural equivalent; the Rust idiom would be an `AtomicBool` or a callback, and the port has no consumer that needs it. Recommend skipping unless strict code-parity is the goal, in which case it should be designed as Rust rather than transliterated.

### Performance parity - same results, different speed

- **Bit-packed allocation streams.** Self-contained and worth doing: `Vec<bool>` carries the same eight-fold waste, and the same reasoning about (horizon x resources) applies.
- **Phase 1, the topological walk.** A larger mechanical port of the vertex engine change. It needs a Rust equivalent of `PriorityListCorpus` and its committed baseline first, for the same reason the C# change did - without it there is nothing to prove the ordering is unchanged.
- **Phase 2, per-pass allocation.** Partly applicable. Removing the per-pass edge sets carries over directly. The LINQ-to-loops part has no analogue, since Rust iterators are already zero-cost; and note that Rust's default `HashMap` hashing is slower than .NET's, so the per-pass constant may need separate attention rather than assuming the C# findings transfer.

### Suggested order

Fix the defect first (stall detection and the horizon - **done**), then the limits (**done**), then bit-packing - each self-contained and independently verifiable. The topological walk last, behind its own corpus, and only if the port's performance is judged to matter. `P0070` and cancellation are recommended out of scope, with the reasoning above.

## Recursive dummy-edge ordering on the arrow construction path

`DummyEdgeOrchestrator.GetEdgesInDescendingOrder` is still recursive, unlike the transitive reducers and dependency walks which were made iterative. It sits on the arrow edge-cleanup path rather than the analysis path, so it has not been a problem in practice, but it remains a stack-depth risk on very deep arrow graphs.

## Topological CPM rewrite for the arrow engine

Done for the vertex engine as Phase 1 above; `ArrowCriticalPathEngine` still uses label-correcting passes, sweeping the remaining event set repeatedly at O(depth x E) per pass.

This is **low priority**, because arrow does not repeat the pass. `ArrowGraphCompiler.Compile()` performs no resource scheduling - it validates, reduces and runs the critical path once, purely to prepare a structure for `ToGraph()` - so the cost is paid once per compile rather than once per activity. The multiplier that made this urgent on the vertex side does not exist here.

Arrow does expose the same pattern through `ArrowGraphBuilder.CalculateResourceSchedulesByPriorityList`, which is public but is not used by its own compiler. If anything ever drives that, it would be considerably worse than the vertex version ever was, because arrow's `CalculateCriticalPath` really does perform a transitive reduction on every pass - the extra factor that was wrongly attributed to the vertex path earlier in this file. Should that path matter, hoisting the reduction is the first move, since the graph structure does not change between iterations; only then does the topological rewrite become the next one.

Any such work would need an arrow equivalent of `PriorityListCorpus` first. The existing corpus is vertex-only.
