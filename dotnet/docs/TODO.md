# TODO

Work identified during investigation, with what was observed, why it matters, and what a fix would need to establish - so anything picked up later starts from evidence rather than from scratch. Entries are marked done as they are undertaken, and keep their measurements.

**Read this alongside [PERFORMANCE.md](PERFORMANCE.md).** That document is the plan and the running audit: what was originally identified across the whole library, what became of each item, what was reassessed and deliberately left alone, and what is still outstanding with a current verdict on whether it is worth doing. It also carries the guardrails that every change here was held to, and what the sequencing turned out to teach.

This document is the investigation record for the items that were actually worked: the measurements before and after, the corpora and baselines that guard them, the counter-examples that settled design arguments, and the reasoning behind what was changed - including the reasoning behind approaches that were tried and abandoned. Where an entry here has a wider status, `PERFORMANCE.md` holds it; where an item there needs its evidence, it points back to a section here. Neither document is complete on its own, and the two use overlapping phase numbers for different sequences - "Phase 3" here is the incremental recalculation, "Phase 3" there is the topological CPM.

**One item is outstanding**, and it is a Rust-only performance gap rather than a defect: the incremental critical-path walk reaches its nodes by ID and hashes for each one, where the C# session holds them by reference, which now makes Rust about twice as slow as C# on deep graphs. It is recorded in the section immediately below. Everything else identified during this investigation has been done, in both languages:

- **Priority-list Phases 0 to 3** - the calculation was cubic on a layered graph and is now roughly 80x faster at 8,000 activities than where it started, with allocation down from 91 GB to 721 MB. Phase 3 was the item held back as conditional; it is now implemented, and with it the case for keeping `GraphLimits.MaximumActivityCount` at 2,000 is a domain decision rather than a performance one. Raising it is deliberately left to whoever owns that call.

- **`RemoveRedundantEdges`** was the largest, at 60 seconds and 158 GB on a 150-activity graph, and is now 2 ms and 1 MB. The separate item for the recursive dummy-edge ordering went with it - they were two defects in the same walk.
- **`RedirectDummyEdges`** on a graph that was never transitively reduced was roughly cubic, at 19.7 s for a 4,000-activity chain, and is now 3.5 s and quadratic. The remaining quadratic is inherent to the canonical form the method produces; the section records why, and why a linear version would be a behaviour change rather than an optimisation.
- **Recursive dummy-edge ordering** turned out to be the same method as `RemoveRedundantEdges` - two defects in one walk - and went with it.
- **Rust port parity**, all four items, each with its own measurement; see that section for what was ported and what was deliberately left out.
- **The arrow topological CPM**, in both languages, though the measurement showed it buys nothing - the section at the end records why, and what it turned up instead.

Each is recorded in full below, with its measurements. One thing noted along the way was deliberately *not* acted on and says so where it is recorded: raising the activity limit, which is a domain decision rather than a performance one.

## Rust: the incremental walk reaches its nodes by ID - OUTSTANDING

**Rust is about twice as slow as C# on deep graphs**, where the two were comparable before Phase 3 - 319 ms against 155 ms at 240 layers, and around 13 s against 5.2 s on the 8,000-activity deep shape. Both languages run the same algorithm and produce byte-identical output; the difference is how each one reaches the data.

The cause is believed to be structural rather than algorithmic, and it is worth stating as a belief rather than a measurement: it has not been confirmed with a profiler. `IncrementalCriticalPath` in `rust/compilers/src/vertex/incremental.rs` holds node **IDs** - in `nodes_in_topological_order`, `end_nodes`, `isolated_nodes` and the three changed-node sets - and resolves each one through `state.node(id)`, which is a hash lookup into an `IndexMap`. Reaching a successor costs two: `state.edge_head_node_id(edge_id)` hashes into `edge_head`, and the resulting node ID then hashes into `nodes`. The C# session holds `Node<T, TActivity>` references directly and dereferences them. A node is visited several times per propagation step, so the multiplier lands on the innermost loop of the hottest path.

The cheap thing was tried first and did not help. Caching the End and Isolated node lists in the session removes a whole-graph scan per iteration, which was a real inefficiency in **both** languages and was backported to C# for that reason - but it barely moved the gap, which is what points at the per-visit cost rather than at anything per-iteration.

### What a fix would involve

`state.nodes` is an `IndexMap`, so dense indices already exist: `get_index_of` at session start, `get_index` per visit, no hashing thereafter. Storing those indices instead of keys would suit the topological order especially well, since it is built once and never changes. The `edge_head` and `edge_tail` maps would need the same treatment, or the head node's index would need caching per edge, since resolving a successor is where two lookups become one.

Three things to be careful of. Indices into an `IndexMap` are invalidated by removal, so a session must remain valid only while the structure does not change - which is already its documented contract, but that contract would go from "the ordering would be stale" to "the indices point at the wrong nodes", a considerably sharper failure. The borrow checker is the reason several of the work lists are `mem::take`-ed during propagation; indices do not change that, but any rework should expect it. And the C# and Rust implementations are currently close enough to read side by side, which has been worth a great deal during this work - an index-based Rust walk would diverge structurally from its C# counterpart, so the shared comments would need to carry the equivalence argument explicitly.

### Whether it is worth doing

Nothing depends on it today. The Rust port has no consumer, C# is the shipping implementation, and 13 s at 8,000 activities is far beyond the 2,000-activity limit and further still beyond real plans - the production graph that prompted this investigation had 39 activities. It is recorded because the gap is new, it is understood, and a future reader comparing the two languages deserves to find the reason here rather than rediscover it.

The measurement to reproduce it, after a release build:

```
cargo test --release --test priority_list_equivalence_tests -- --ignored --nocapture measure_priority_list_scaling
```

## Priority-list calculation (was cubic; Phases 0 to 3 done)

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

Phase 3 (**done, in both languages**) attacks the iteration count itself. It was investigated first, and the findings below revise what was originally proposed in both directions; the implementation and its measurements follow them.

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

### What was built

The two flows keep their full passes, and their arithmetic is now factored into a handful of value helpers so that the full and incremental paths compute from one copy rather than two - the drift between them was the risk the design section flags, and this removes the opportunity for it. Extracting the helpers was verified on its own, before anything used them, by the baseline staying byte-identical.

`IVertexIncrementalCriticalPath` / `IncrementalCriticalPath` is the incremental path. The priority-list calculation now runs one full calculation and then hands each duration change to it. Three things make it exact rather than approximate:

- **The order is computed once.** The graph structure never changes during a priority-list calculation - only durations do - so a topological ordering computed at the start stays valid for every iteration. Changed nodes are visited in that order, so each is recomputed at most once with all of its predecessors already final, which is what makes a single comparison per node sufficient.
- **Propagation stops where a value has not moved.** A value that did not change cannot change anything downstream of it, because everything below depends on the values above only through the numbers being compared.
- **A moved finish time is propagated, not surrendered to.** The first implementation fell back to a full recalculation whenever the project finish time moved, on the strength of the 5% figure measured above. That figure is from shallow graphs and badly understates the deep ones: the selection picks the activity with the least total slack, which is one *on* the critical path, so shortening it moves the finish time constantly. Seeding the backward propagation from the End nodes instead - and letting it stop where values have not moved, as everywhere else - took the 8,000-activity deep shape from 9.9 s to 5.2 s on its own.

Two things needed care and are worth knowing. `Duration` feeds the forward pass twice, not once - it sets the earliest finish time, but it also feeds the `MaximumLatestFinishTime - Duration` clamp, so shortening an activity can *raise* its own earliest start rather than only lowering its finish. And free slack is tracked separately from both flows, because it depends on the successors' earliest start times as well as on the node's own values; the priority-list selection never reads it, but it is maintained so that an incrementally updated graph is indistinguishable from a fully recalculated one, which is what lets the two be compared value for value.

The constraint check is no longer run per iteration. `FindInvalidPreCompilationConstraints` depends on the duration only through `MinimumEarliestStartTime + Duration > MaximumLatestFinishTime`, so zeroing an activity can make it valid but never invalid: if the set is empty at the first iteration it stays empty, and if it is not, the first iteration throws.

### Measured

C#, for the priority-list calculation alone, against the Phase 2 figures:

| Activities (12 layers) | Phase 2 | Phase 3 | Allocated, Phase 2 | Allocated, Phase 3 |
| - | - | - | - | - |
| 250 | 130 ms | 58 ms | 8 MB | 1 MB |
| 500 | 296 ms | 77 ms | 27 MB | 3 MB |
| 1,000 | 770 ms | 194 ms | 104 MB | 7 MB |
| 2,000 | 1,477 ms | 434 ms | 416 MB | 16 MB |
| 4,000 | - | 611 ms | - | 27 MB |
| 8,000 | - | 1,794 ms | - | 57 MB |

Depth dependence is gone at fixed size, where Phase 2 still had some:

| Layers, at 1,500 activities | Phase 2 | Phase 3 |
| - | - | - |
| 10 | 669 ms | 76 ms |
| 60 | 677 ms | 159 ms |
| 240 | 520 ms | 155 ms |

On the shape the investigation started from, where depth grows with size, measured against every earlier stage:

| Activities | Depth | Start | Phase 2 | Phase 3 | Allocated, Phase 2 | Allocated, Phase 3 |
| - | - | - | - | - | - | - |
| 1,000 | 40 | 1.2 s | 0.88 s | **0.08 s** | 91 MB | 15 MB |
| 2,000 | 80 | 6.9 s | 1.54 s | **0.3 s** | 375 MB | 51 MB |
| 4,000 | 160 | 47.3 s | 5.96 s | **1.2 s** | 1.4 GB | 189 MB |
| 8,000 | 320 | 413 s | 44.4 s | **5.5 s** | 5.7 GB | 720 MB |

That is 5x to 12x on top of Phase 2, and roughly 75x against where the investigation started at 8,000 activities, with allocation down eight-fold again.

Treat the top row as "about five and a half seconds" rather than a precise figure: repeated runs of the same build varied between 5.2 s and 8.9 s while allocating within a megabyte of each other, so the spread is the machine and the garbage collector rather than the work. Allocation is the stable number of the two, and it is what the tables above should be read by.

Rust, same calculation:

| Activities (12 layers) | Before | Phase 3 | | Layers, at 1,500 | Before | Phase 3 |
| - | - | - | - | - | - | - |
| 250 | 20 ms | 14 ms | | 10 | 901 ms | 66 ms |
| 500 | 90 ms | 25 ms | | 30 | 999 ms | 166 ms |
| 1,000 | 418 ms | 48 ms | | 60 | 1,024 ms | 208 ms |
| 2,000 | 1,854 ms | 166 ms | | 240 | 653 ms | 319 ms |

**Worth recording honestly: on deep graphs Rust is now about twice as slow as C#** - 319 ms against 155 ms at 240 layers, and around 13 s against 5.2 s on the 8,000-activity deep shape, where the two were comparable before. That is the one outstanding item in this document; the analysis, what a fix would involve and why it was not done here are in the Rust node-lookup section at the top.

### What this does for the activity limit

`GraphLimits.MaximumActivityCount` is 2,000, and that ceiling was set by what was affordable. At exactly that limit the priority-list calculation now takes **0.29 seconds** on the realistic shape, against 1.54 seconds after Phase 2 and 6.9 seconds when this began. At 8,000 activities it takes 5.2 seconds, where before Phase 3 it took 44.

A whole compile at the limit - not just this calculation - has since been measured at about **0.36 seconds**, of which this is roughly two thirds; the breakdown is in the compile-split section of [PERFORMANCE.md](PERFORMANCE.md). So the affordability argument that set the ceiling has moved by a factor of nearly twenty, and it is now the whole compile that fits inside an interactive edit cycle rather than just a part of it.

The limit has deliberately **not** been raised here - that is a domain decision about what the library should accept, not a consequence of making it faster, and it belongs to whoever owns the product rather than to this optimisation. But the argument the earlier note made for keeping it at 2,000 no longer holds: a limit of 5,000 to 10,000 is now defensible on performance grounds. Real plans sit far below either figure in any case - the production graph that prompted this whole investigation had 39 activities.

Any change here must preserve the exact priority ordering, since that ordering determines resource assignment and therefore the final schedule. `CompileBehaviourTests` (order-independence across 50 random DAGs) and the golden tests in both the C# and Rust suites are the regression net; a benchmark harness comparing before/after priority lists on random graphs would be worth building first.

This is the change that would move the usable ceiling. The `GraphLimits.MaximumActivityCount` limit of 2,000 currently bounds what is *accepted*; it does not make large graphs fast, and raising it would not be sensible until this is addressed.

## Rust port parity

**Complete.** The `rust/` port mirrored the C# code as it stood before any of the recent work, and was brought forward one self-contained item at a time: the scheduling stall detection, the domain limits, the bit-packed allocation streams, and the topological walk. Each is recorded below with what was measured.

The port's golden tests were mirrored copies rather than shared, so it only ever passed against its own behaviour - the divergence was real but latent. The priority-list corpus built for the last item closes that gap for the calculation that matters most: it generates the same graphs in both languages and checks the Rust output against the **C# baseline file itself**, so a future change to either side that breaks agreement now fails a test.

**Parity is not one-directional, and checking it in both directions is the point.** Of the small allocation items done on the C# side afterwards, one did not exist in the port at all (`scheduled_activities()` returns a borrowed slice, so there was never a copy to remove), one had no counterpart (Rust's iterator chains already compile to the loop the C# was rewritten into), and one was a genuine defect present in both and fixed in both. Three of the four occasions this investigation has compared the two languages closely, the port has been the correct side. The habit worth keeping is to read the port first and treat a divergence as a question rather than a to-do; the reason to keep porting even the changes that save nothing there, such as `first_activity_start_time`, is that the two files staying readable side by side is what makes divergences findable at all. `PERFORMANCE.md` carries the item-by-item detail.

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

## `RedirectDummyEdges` on a graph that was never transitively reduced - DONE, cubic to quadratic

Calculating the critical path on an `ArrowGraphBuilder` that has not been transitively reduced was roughly cubic, at 19.7 s for a 4,000-activity chain. It is now **3.5 s in C# and 3.7 s in Rust**, and quadratic. The residual quadratic is inherent to what the method computes, and the reasoning for leaving it is below.

The cost is entirely the `RedirectEdges()` that `CalculateCriticalPath` performs *after* the critical-path passes, which is a different proposition from the same call before them: `RedirectDummyEdges` orders its nodes by `EarliestFinishTime`, so once those times are populated it does real redirection work rather than iterating a graph whose keys are all null. Timed through the engine seams, at 4,000 activities it was 19,401 ms of a 19,488 ms total, against 60 ms for the redundant-edge removal and 11 ms for the three critical-path passes.

### What it does, and why it is quadratic

Where a group of nodes all feed the same successors through removable dummy edges, the group is collapsed so the dummies can be dropped. On an un-reduced chain the whole graph is one such group: every event has a dummy into the End node. Dumped on a six-activity chain, the six dummies `3,5,7,9,11,13 -> 2` become `3->5, 5->7, 7->9, 9->11, 11->13, 13->2` - the fan-in is rewritten as a chain running alongside the real edges.

It builds that chain one link per pass. Each node in turn redirects the whole remaining group to itself, and the next node takes all of them back except one, so a fan-in of k costs k(k-1)/2 redirections. Counted at 4,000 activities: **7,998,000 redirections of 3,999 distinct edges**, which is exactly N(N-1)/2 - each edge moved about two thousand times. Only the last move of each edge survives.

That churn is load-bearing, which is why it is still there. The order is `EarliestFinishTime` descending, and it decides the result: walked in ascending order instead, the first node's redirect makes the guard fail for every node after it, and the graph ends as a star into one node rather than a chain. So a linear formulation is available, but it produces a different canonical form - a behaviour change to a method whose output feeds resource scheduling, not an optimisation. Reproducing the current form directly means simulating the same passes, which is the quadratic.

### What was fixed

The factor sitting on top of it. Every redirection copied a node's whole edge set purely to ask whether it was empty (`ToList()` then `.Any()`, with the copy never used), and the node being asked is the one holding the fan-in - so the copy was O(k) and the pass was cubic rather than quadratic. Counting instead of copying is provably output-identical, and the instrumented counts were byte-identical across the change: same nodes processed, same 7,998,000 redirections, same 3,999 distinct edges. At 4,000 activities C# went from 24,438 ms to 3,580 ms.

Rust had the emptiness test right already - another case of the port being the correct side - but carried the same cubic factor by a different route: `Node`'s edge sets were `IndexSet`, whose `shift_remove` moves and re-indexes every later entry, so each removal from the fan-in node was O(k). `primitives/src/insertion_order_set.rs` is the set counterpart of the compilers' `InsertionOrderMap`, tombstoning a removed slot and compacting once half the slots are dead, so removal is O(1) amortised with insertion order unchanged. `Node.incoming` and `Node.outgoing` now use it, which took Rust from 20,390 ms to 3,671 ms - the two languages are now within 5% of each other, where before this they were both cubic. The 663 existing assertions over those sets were untouched, because the type carries the same method names; that they still pass, and that both golden baselines stayed byte-identical, is what shows the iteration order was preserved.

### Reachability

`ArrowGraphCompiler` calls `TransitiveReduction()` immediately before `CalculateCriticalPath()`, and a reduced graph has no removable dummy fan-in left at all - measured, it performs **zero** redirections and the pass costs 11 ms at 4,000 activities. So a compile never took this path; it needs `ArrowGraphBuilder` driven directly, which is public API but not what the compiler does. That is also why the deep-chain regression tests in both languages reduce first, with a comment saying so, and why the redirect tests deliberately do not.

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
