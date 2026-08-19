# TODO

Work identified during investigation, with what was observed, why it matters, and what a fix would need to establish - so anything picked up later starts from evidence rather than from scratch. Entries are marked done as they are undertaken, and keep their measurements.

In rough order of remaining value:

1. **`RedirectDummyEdges` on a graph that was never transitively reduced** - newly measured while fixing the item below, and now the largest remaining problem: roughly cubic, at 2.7 s for a 2,000-activity chain and 19.7 s for 4,000. Lower severity than it sounds, because the compiler always reduces before calculating; it is reachable through `ArrowGraphBuilder` used directly.
2. **Priority-list Phase 3** - investigated and designed, deliberately not implemented. Its value is conditional on raising `GraphLimits.MaximumActivityCount`; see the reassessment at the end of that section.

**`RemoveRedundantEdges` is fixed in both languages** - it was the largest item on this list, at 60 seconds and 158 GB on a 150-activity graph, and is now 2 ms and 1 MB. The separate item that used to sit here for the recursive dummy-edge ordering is fixed with it: they were two defects in the same walk. The section at the end of this document records both.

**Rust port parity is complete** - all four items done, each with its own measurement; see that section for what was ported and what was deliberately left out. **The arrow topological CPM is also done in both languages**, though the measurement showed it buys nothing - the section at the end of this document records why, and what it turned up instead.

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

**Complete.** The `rust/` port mirrored the C# code as it stood before any of the recent work, and was brought forward one self-contained item at a time: the scheduling stall detection, the domain limits, the bit-packed allocation streams, and the topological walk. Each is recorded below with what was measured.

The port's golden tests were mirrored copies rather than shared, so it only ever passed against its own behaviour - the divergence was real but latent. The priority-list corpus built for the last item closes that gap for the calculation that matters most: it generates the same graphs in both languages and checks the Rust output against the **C# baseline file itself**, so a future change to either side that breaks agreement now fails a test.

Neither language sweeps any longer: the arrow critical-path engine (`compilers/src/arrow/cpm.rs` and its C# counterpart) was rewritten too - see the arrow section at the end of this document, including why that particular rewrite gained nothing.

### Behavioural divergence - the port now computes different results

- **Scheduler stall detection, skip-ahead and the time horizon (`C0020`) - DONE.** The port did carry the livelock that started this investigation, and it was confirmed rather than assumed: temporarily restoring `time_counter += 1` made the ported guardrail tests hang until their 30-second watchdog fired. `next_tick_of_interest`, `build_stall_message` and `describe_unschedulable_activity` are now ported to `compilers/src/scheduling/scheduler.rs`, with the horizon guards in both scheduling attempts computed in `i64`. Since Rust has no exceptions, `GraphError` gained a `GraphErrorKind` discriminant so `VertexGraphCompiler` can tell a stall from any other failure and report `C0020` in place of it; the computed-schedule horizon check after the second CPM pass reports `C0020` too. `graph_limits` was added to primitives at the same time, since the horizon guard needs `MAXIMUM_TIME_VALUE` - constants only, with the `P0080` validation still to come. The error-code discriminants are now pinned to the C# values, leaving 7 free for `P0070`. 16 tests ported (`scheduler_time_gate_tests.rs`, `scheduler_guardrail_tests.rs`); suite 361 to 377 green. The six time-gate tests reproduce the C# expected values exactly, which is what demonstrates the skip-ahead did not change observable behaviour.
- **Domain limits and `P0080` - DONE.** `compilers/src/limit_checker.rs` mirrors the C# `LimitChecker` (three count checks, then each activity's four time values), reported as `P0080` from `add_pre_compilation_errors`. That method gained a `work_streams` parameter, since the work-stream count is one of the three counts and the Rust signature had no way to see them - a small public API change on `VertexGraphBuilder`. Two of the ported tests assert the complete rendered message rather than a substring, which is what pins the text to the C# resource strings; the `{0}`-style templates are now rendered through a single `messages::format_message` taking a slice of arguments, replacing the earlier `format1`/`format2` pair. As in C#, this is vertex-only: `ArrowGraphBuilder` does not reference `LimitChecker` there either, despite what the comment at the top of that file claims. 11 tests ported (`graph_limit_tests.rs`); suite 377 to 387 green, and no existing test tripped the new caps - the 20,000-activity chains in `deep_graph_tests.rs` go through the builder rather than the compiler, so they are unaffected, exactly as on the C# side.
- **`P0070`, the input self-consistency probe.** Recommend **not** porting, and the stall-detection work followed that recommendation. It detects a `HashSet` whose lookups disagree with its contents, which arises from unsynchronized concurrent mutation - a state safe Rust cannot produce, since a collection cannot be mutated from two threads without synchronization and the compiler enforces it. Porting it would mean writing a check for a condition that cannot occur, and no test could construct the input to exercise it. For the same reason, the corresponding branch of `describe_unschedulable_activity` (which probes the target resource set when everything else checks out) is absent from the port, and the three corrupted-`HashSet` tests were not ported.
- **Mandatory cancellation tokens.** No natural equivalent; the Rust idiom would be an `AtomicBool` or a callback, and the port has no consumer that needs it. Recommend skipping unless strict code-parity is the goal, in which case it should be designed as Rust rather than transliterated.

### Performance parity - same results, different speed

- **Bit-packed allocation streams - DONE.** `primitives/src/packed_bool_list.rs` stores one bit per flag over a `Vec<u64>`, using the same shift/mask idiom as the ancestor bit sets, and the five `ResourceSchedule` streams are now `PackedBoolList` rather than `Vec<bool>`. Measured with a counting global allocator at a 100,000 horizon x 100 resources x 5 streams: **47.7 MB to 6.0 MB**, the expected eight-fold reduction, and at the full 1,000-resource shape that scales to roughly 477 MB to 60 MB - within a whisker of the C# figures. The type compares equal to a `Vec<bool>` of the same flags and renders as a list of flags when formatted, so the 375 existing allocation assertions across the suite were left untouched; that they still pass is what shows the packing preserved every value. There is deliberately no `Index` implementation, because indexing must return a reference and a bit inside a word has no address to borrow - reads go through `get`, and the two tests that sliced a stream now use the iterator. 11 tests ported (`packed_bool_list_tests.rs`); suite 387 to 399 green.
- **Phases 1 and 2, the topological walk and per-pass allocation - DONE, together.** The oracle was built first, as on the C# side: `tests/priority_list_corpus/mod.rs` is a value-for-value port of the C# generator, so both languages build the same 56 graphs. That paid off immediately - the Rust baseline came out **byte-identical to the committed C# one**, which means the port already agreed with the original on every shape, and a `matches_the_dotnet_baseline` test now keeps checking that. The oracle was then verified to fail: perturbing only the tie order in the priority-list selection (same primary key, reversed IDs within ties) broke 44 of the 56 cases, which is the exact failure mode the rewrite risked.

  `compilers/src/vertex/cpm.rs` then moved from label-correcting sweeps to node-order walks with per-node pending-edge counters, exactly as the C# engine did, with cycle detection via a leftover outstanding-edge count. Only the traversal changed; every value computation was left alone, and the baseline stayed byte-identical.

  | Activities (12 layers) | Before | After | | Depth (1,500 activities) | Before | After |
  | - | - | - | - | - | - | - |
  | 250 | 77 ms | 20 ms | | 10 layers | 11,637 ms | 901 ms |
  | 500 | 582 ms | 90 ms | | 30 layers | 14,901 ms | 999 ms |
  | 1,000 | 4,077 ms | 418 ms | | 60 layers | 14,971 ms | 1,024 ms |
  | 2,000 | 31,564 ms | 1,854 ms | | 240 layers | 14,960 ms | 653 ms |

  Phase 2 came along for free, because the C# design has no per-pass edge sets to begin with. That mattered more here than it did in C#: the Rust "before" profile was flat in depth rather than growing with it, because the dominant cost was not the O(depth x E) sweep at all but `IndexSet::shift_remove`, which is linear in the set size, making each pass O(E^2). Removing the sets removed that.

  Worth recording honestly: at 2,000 activities Rust is now 1.85 s against C#'s 1.54 s - comparable, where before it was 31.6 s against 1.54 s. The remaining gap is small enough not to be worth chasing without a specific reason.

### Suggested order

This was the order followed, and it held up: the defect first (stall detection and the horizon), then the limits, then bit-packing - each self-contained and independently verifiable - and the topological walk last, behind its own corpus. `P0070` and cancellation were left out of scope, with the reasoning above.

## Recursive dummy-edge ordering on the arrow construction path - DONE

`DummyEdgeOrchestrator.GetEdgesInDescendingOrder` was the last recursive traversal, and it turned out to be the same method as the `RemoveRedundantEdges` cost below, so both were fixed together. The stack-depth risk recorded here was real and not merely latent: a 10,000-activity arrow chain overflows the stack and kills the process outright, which is what the new deep-chain regression test was checked against.

## `RedirectDummyEdges` on a graph that was never transitively reduced

Found while fixing the walk below, and left unfixed. Calculating the critical path on an `ArrowGraphBuilder` that has not been transitively reduced costs, for a plain chain:

| Chain | Build | `RemoveRedundantEdges` | `CalculateCriticalPath` |
| - | - | - | - |
| 250 | 6 ms | 13 ms | 58 ms |
| 500 | 0 ms | 4 ms | 141 ms |
| 1,000 | 1 ms | 17 ms | 762 ms |
| 2,000 | 6 ms | 25 ms | 2,658 ms |
| 4,000 | 10 ms | 29 ms | 19,744 ms |

About 7x per doubling, so roughly cubic, and none of it is the redundant-edge removal - that column is the same call immediately beforehand, and it stays flat. The cost is the `RedirectEdges()` that `CalculateCriticalPath` performs *after* the critical-path passes, which is a different proposition from the same call before them: `RedirectDummyEdges` orders its nodes by `EarliestFinishTime`, so once those times are populated it does real redirection work rather than iterating a graph whose keys are all null. The same shape reduced first costs 9 ms at 4,000.

The severity is bounded by reachability. `ArrowGraphCompiler` calls `TransitiveReduction()` immediately before `CalculateCriticalPath()`, so a compile never takes this path; it needs `ArrowGraphBuilder` driven directly, which is public API but not what the compiler does. That is why the deep-chain regression tests in both languages reduce first, with a comment saying so - otherwise they would be measuring this instead of the walk they are meant to pin.

## Topological CPM rewrite for the arrow engine - DONE, and it bought nothing

Both engines now walk events in dependency order with per-node pending-edge counters instead of sweeping the remaining event set (`ArrowCriticalPathEngine`, `rust/compilers/src/arrow/cpm.rs`). The output is byte-identical in both languages, and the safety net that proves it is described below.

**The honest result: there was no measurable gain, because the arrow critical path was never the cost.** The prediction recorded here previously - low priority, because arrow runs the pass once per compile rather than once per activity - held. Measuring with a timing decorator injected through the engine seam, so the critical-path passes could be separated from the redundant-edge removal that `CalculateCriticalPath` performs first:

| Graph | Total | Critical-path passes | The rest | Allocated |
| - | - | - | - | - |
| 150 activities, 12 layers | 3 ms | 0 ms | 3 ms | 3 MB |
| 150 activities, 10 layers | 8 ms | 0 ms | 8 ms | 7 MB |
| **150 activities, 30 layers** | **59,918 ms** | **0 ms** | **59,918 ms** | **158,191 MB** |
| 150 activities, 60 layers | 0 ms | 0 ms | 0 ms | 0 MB |

That is the state *before* the redundant-edge fix recorded below, which is why the third row is what it is; the "rest" column has since collapsed to milliseconds throughout.

The passes are sub-millisecond at every size tried, including the pathological one. The rewrite is still worth keeping - it removes a quadratic sweep and, in Rust, a linear `shift_remove` per event, so it protects against a future caller that does drive the arrow path hard - but it should not be described as an optimisation of anything that was slow.

### The real finding: `RemoveRedundantEdges` on the arrow path - DONE

That third row was the thing worth acting on, and it was a bug rather than an expense. **One 150-activity graph took 60 seconds and churned 158 GB**, entirely inside the redundant-edge removal, on a shape no larger than its neighbours - 150 activities across 30 layers, five per layer - while its neighbours at 10, 60 and 120 layers completed in single-digit milliseconds. It is now **2 ms and 1 MB**.

The cause is one line in `DummyEdgeOrchestrator.GetEdgesInDescendingOrder`. It guarded whether an edge was *recorded*, but the recursive descent sat outside that guard and ran for every edge *occurrence*. That makes the walk enumerate every distinct path from the start node instead of visiting each edge once, so its cost is the number of start-to-node paths summed over the graph - exponential in the depth of a branching DAG, and unrelated to the size of the graph being walked.

Counted exactly, by a dynamic program over the same recurrence rather than by running the walk, on the benchmark's 150-activity graphs:

| Layers | Nodes | Edges | Invocations | Against a walk that visits each node once |
| - | - | - | - | - |
| 8 | 284 | 384 | 3.5 x 10^3 | 5x |
| 10 | 287 | 408 | 1.4 x 10^4 | 20x |
| 12 | 290 | 366 | 5.1 x 10^3 | 8x |
| 16 | 293 | 392 | 8.3 x 10^4 | 121x |
| 20 | 295 | 422 | 1.4 x 10^6 | 1,883x |
| 24 | 296 | 300 | 3.0 x 10^2 | 0.5x |
| 30 | 297 | 416 | **3.5 x 10^8** | **496,078x** |
| 40 | 299 | 300 | 3.0 x 10^2 | 0.5x |
| 60 | 300 | 300 | 3.0 x 10^2 | 0.5x |
| 120 | 301 | 300 | 3.0 x 10^2 | 0.5x |

That also explains the "shape sensitivity", which was not one. The count is about 2^depth wherever activities have two distinct predecessors, and collapses to the chain case wherever they have one - and the benchmark generator picks its two dependencies with `id * 7` and `id * 13` modulo the layer width, which coincide for every id once the width divides into both, at 24 layers and beyond. So the rows that looked free were chains, and 30 layers was simply the deepest shape that still branched. Three walks at 3.5 x 10^8 steps, each step allocating a LINQ enumerator over a node's outgoing edges, is the 158 GB.

Descending again through an edge already recorded cannot record anything new: the state does not change for the duration of the walk, and the earlier descent through that edge has necessarily finished, since re-entering one still in progress would need a cycle, which the removal has already excluded. So the guard drops only steps whose output was discarded, and the recorded order - which decides which dummy edges are removed, and therefore the compiled graph - is unchanged. The walk was made iterative at the same time, which is the separate stack-depth item above.

The Rust port already had both, which is the reverse of the usual direction, and its guard carries the comment explaining why it is safe. That is also the strongest evidence the change is equivalent: the two languages' arrow baselines were byte-identical *before* this fix, so the guarded walk was already known to produce the same compiled graph as the unguarded one on all 56 corpus shapes. They remain byte-identical after it.

Measured again after the fix, the whole table is flat:

| Graph | Total | Critical-path passes | The rest | Allocated |
| - | - | - | - | - |
| 150 activities, 10 layers | 1 ms | 0 ms | 1 ms | 1 MB |
| **150 activities, 30 layers** | **2 ms** | **0 ms** | **2 ms** | **1 MB** |
| 150 activities, 60 layers | 0 ms | 0 ms | 0 ms | 0 MB |
| 150 activities, 120 layers | 0 ms | 0 ms | 0 ms | 0 MB |

`ArrowGraphBuilder.CalculateResourceSchedulesByPriorityList` was going to multiply the old cost by an iteration per activity, since arrow's `CalculateCriticalPath` runs the reduction on every pass; that public method remains unused by its own compiler, but it is no longer carrying an exponential factor if anything ever calls it.

`RedundantEdgeRemovalTests` / `redundant_edge_removal_tests.rs` pin both defects, one test each, and both were checked against the old code first: the branching graph allocated 158,192 MB against a 100 MB ceiling, and the deep chain killed the test process with a stack overflow inside `GetEdgesInDescendingOrder`. The C# test asserts on allocation rather than elapsed time because allocation is deterministic and the two regimes sit three orders of magnitude either side of the threshold; the Rust test uses the watchdog idiom already established by the scheduler guardrail tests, since counting allocations there would mean a global allocator, which is per test binary rather than per test.

A related property found while building the corpus, and worth knowing before touching any of this: **`CalculateCriticalPath` is not idempotent.** It removes redundant edges before calculating, so a second call on the same builder can produce different values - on `fan-in-5` a dummy activity's free slack moves from 2 to 1, with no shuffling involved. The equivalence tests therefore build a fresh graph per variant rather than recalculating one.

### The safety net

`ArrowCriticalPathCorpus` / `arrow_critical_path_corpus` build the same 56 networks as the priority-list corpus - both now draw their shapes from a shared `CorpusShapes` / `corpus_shapes`, and the vertex baselines staying byte-identical is what proved that extraction safe. The arrow baseline pins every value the engine produces: each event's earliest and latest finish time, and each activity's earliest start, latest finish and free slack, dummy activities included.

The two languages agree byte-for-byte, so `ArrowCriticalPathBaseline.txt` is checked directly from the Rust suite as well. The oracle was verified to fail before being relied on: seeding a traversal bug (processing an event before its dependencies were ready) diverged all 56 cases.
