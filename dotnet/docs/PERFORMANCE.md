# Performance & memory plan

A phased plan to reduce compile wall-clock time, cut allocation/GC pressure, and harden the deep-graph paths in `Zametek.Maths.Graphs`. Nearly all of it has since been implemented; the status section below is the current picture, and the tiers and phases after it are the original plan, kept for reference and annotated with what became of each item.

Target scale: **low thousands of activities** (interactive project plans, repeated re-compiles on edit). Scope: **vertex (analysis) and arrow (rendering) paths**.

### How this document relates to `TODO.md`

The two are meant to be read together, and they divide as follows.

**This document is the plan and the audit**: what was originally identified, what was done to it, what was reassessed and deliberately left, and what is still outstanding with a current verdict on whether it is worth doing. If you want to know the state of an item, look here.

**`TODO.md` is the investigation record**: the measurements, the equivalence corpora that guard them, the counter-examples that settled design arguments, and the reasoning behind each change - including the reasoning behind the things that were tried and abandoned. If you want to know *why* an item ended up the way it did, or what a number was before and after, look there.

So the items below cross-reference into `TODO.md` by section name rather than restating its measurements, and `TODO.md` points back here for the overall status. Neither is complete on its own.

## Implementation status

Applied (all behaviour-preserving; full test suite green):

- **Phase 2** - the scheduler resolves each activity's strong dependency set once before the tick loop instead of re-walking the graph on every time tick (`PriorityListResourceScheduler`). Shared by both graph flavours.
- **Phase 3, allocation cut** - the CPM label-correcting loops (`VertexCriticalPathEngine`, `ArrowCriticalPathEngine`) no longer build a throwaway `HashSet` per edge/node per pass; a zero-allocation membership test is used and each node's own edge set is read directly (it is not mutated during CPM).
- **Phase 3, full topological restructure - done for both engines.** `VertexCriticalPathEngine` now walks nodes in dependency order rather than sweeping the remaining edge set repeatedly, taking a pass from O(depth x E) to O(V + E), and its per-pass allocation was removed as well. This was undertaken as part of the priority-list investigation, since the two turned out to be the same bottleneck; the benchmark harness and equivalence corpus that this entry asked for exist now, in `PriorityListEquivalenceTests`. `ArrowCriticalPathEngine` has since had the same rewrite, guarded by its own corpus and baseline - and it bought nothing measurable, because the arrow critical path was never the cost. See "Topological CPM rewrite for the arrow engine" in `TODO.md` for the measurements and for the far larger finding that measuring it turned up.
- **Phase 4** - the transitive reducers (`VertexTransitiveReducer`, `ArrowTransitiveReducer.RemoveRedundantIncomingDummyEdges`) and the strong-dependency walks (`VertexGraphBuilder`/`ArrowGraphBuilder.StrongActivityDependencyIds`) are now iterative with visited sets - no more StackOverflow risk on deep chains, and no redundant re-traversal of shared sub-paths. `DeepGraphReductionTests` covers the vertex paths.
- **Phase 1 (partial)** - `SetActivityDependencies` uses an O(1) node-key lookup (P6).
- **Scheduler livelock guardrails (added after a production hang was diagnosed from a dump)** - the `PriorityListResourceScheduler` tick loop now skips idle stretches (jumping straight to the next running-activity finish or time-gate opening, provably without changing any schedule) and fails fast with a diagnostic when no future event exists while activities remain - surfaced as compilation error `C0020` through the compilers, or `ResourceSchedulingStallException` from direct engine calls. A pre-compilation self-consistency probe (`P0070`) rejects structurally corrupted input sets (a `HashSet` whose `Contains` disagrees with its own enumeration - the torn-copy signature of unsynchronized concurrent modification, which previously trapped the loop forever). `Compile` and the scheduling entry points now require a `CancellationToken` (a deliberate breaking change - the sole consumer opted in), checked between pipeline phases and on every scheduling tick. See `SchedulerGuardrailTests` and `SchedulerTimeGateTests`.
- **Cancellation extended to the arrow path and to the priority-list loop.** `ArrowGraphCompiler.Compile` was the one compiler entry point still accepting no token, and both builders' `CalculateResourceSchedulesByPriorityList` accepted one but then ran the whole priority-list calculation - a pass per activity, the dominant cost - before reaching the scheduling engine that honours it. A token that is only checked after the expensive part is worse than none, because the signature tells a caller the operation is interruptible when it is not. The token now reaches `ArrowGraphCompiler.Compile`, `ArrowGraphBuilder.CalculateCriticalPath`, and both builders' `CalculateCriticalPathPriorityList`, checked once per activity placed. It stops at the engine seams by design, so the residual uninterruptible stretch is a single engine call. `CancellationTests` pins it, and asserts the *iteration count* rather than only that an exception was thrown - which is the assertion that fails when a check is removed, since the exception still arrives, just too late to have saved any work.
- **Domain limits and bit-packed allocation streams.** `GraphLimits` publishes the accepted ranges (time values 0 to 100,000; 2,000 activities; 1,000 resources; 100 work streams) so consumers can validate input before compiling; violations are reported as `P0080`, and a computed schedule running past the horizon as `C0020`. The five per-time-unit allocation streams on each resource schedule are now stored one bit per flag (`PackedBoolList`) instead of one byte, since they are what a compilation retains and they scale as (horizon x resources): measured at the limits, 1,000 resources across a 100,000-unit horizon now retains 60 MB rather than 476 MB. The public surface is unchanged - the streams were already `IEnumerable<bool>` on both the interface and the `ResourceSchedule` constructor.

- **Priority-list calculation (P2) - reworked in three phases, and now roughly 75x faster at 8,000 activities than where it began.** The loop that dominates compile time ran a full critical-path calculation per activity; it now runs one, and thereafter propagates each duration change only as far as values actually move. See "Priority-list calculation" in `TODO.md` for the phase-by-phase measurements, the equivalence corpus that guards them, the counter-example that ruled out the obvious shortcut, and the two findings that changed the design mid-implementation.
- **Arrow dummy-edge defects found while measuring, and fixed - the largest single win in the whole investigation.** Measuring the arrow critical path turned up a 150-activity graph that took 60 seconds and churned 158 GB inside `RemoveRedundantEdges`, from a recursion guard that gated recording but not descent, making the walk enumerate every start-to-node path. It is now 2 ms and 1 MB. The same walk carried the last remaining stack-overflow risk. Separately, `RedirectDummyEdges` on a graph that was never transitively reduced was cubic and is now quadratic, the residual quadratic being inherent to the canonical form the method produces. See "The real finding: `RemoveRedundantEdges` on the arrow path" and "`RedirectDummyEdges` on a graph that was never transitively reduced" in `TODO.md`.

Reassessed as **not** behaviour-safe and intentionally left as-is:

- **P4 (remove the double clone)** - load-bearing: the priority-list clone is destroyed (durations zeroed, CPM state overwritten) while the separate scheduling clone must retain valid CPM times. Collapsing them would feed the scheduler corrupted times. Unaffected by the later work: the clone is taken once per scheduling call, not once per critical-path pass, so it never showed up in the measurements.
- **P5 (dedup `SetActivityDependencies` LINQ)** - mutation-order-dependent; each of the four blocks re-reads state (dependencies, graph edges) that the previous block mutated.

Still outstanding, audited against the code rather than against this document's own status. Nothing here is known to matter; they are recorded so the next reader starts from a verdict rather than from the original plan:

- **`ActivityAt` - measured, and not worth fixing.** `ResourceScheduleBuilder.ActivityAt` scans a resource's scheduled activities from the front and `AdvanceCompletedActivities` calls it once per resource per tick, which is the "optionally index this" footnote on P3. The scan is real and shows up exactly where predicted, but the resource-scheduling tick loop is only about 4% of a compile - see the compile split below. Left alone.
- **P7 is misdescribed above and is not the problem it was recorded as.** `ResourceScheduleBuilder.ScheduledActivities` does still copy on every access, but no call site is inside the scheduler's tick loop: the per-tick read goes through `LastActivityFinishTime`, which is `LinkedList.Last` and O(1). What remains is a handful of copies per compile.
- **The rest of P6, and the compiler clone dedup, are individually trivial - but they sit inside the third of a compile nobody has profiled.** `ActivityIds`, `StartTime` and `FinishTime` are still LINQ over the state's values on each access, O(A) apiece and called a few times per compile; `Compile` still materialises `Activities.ToList()` three times, and clones every activity in each of its return branches - but those branches are mutually exclusive, so it is one clone per compile rather than the two or three claimed under P4. Each is small on its own; collectively they are candidates for the "everything else" column below, along with the priority-list loop's own selection scan. Anything here should be measured before it is changed, not assumed.
- **The arrow priority-list loop never got the incremental treatment (Phase 3).** `ArrowGraphBuilder.CalculateCriticalPathPriorityList` still runs a full `CalculateCriticalPath(cancellationToken)` per activity, and arrow's is heavier than vertex's because it re-runs `RemoveRedundantEdges()` and `RedirectEdges()` on every iteration of a graph whose structure never changes. This is the largest outstanding item by size - and the least urgent, because `ArrowGraphCompiler` performs no resource scheduling and never calls it. Optimising a method with no caller buys nothing and risks the canonical form the arrow path depends on, which "`RedirectDummyEdges`" in `TODO.md` explains is load-bearing rather than incidental.
- **The Rust port's incremental walk reaches its nodes by ID**, which now makes it about twice as slow as C# on deep graphs. Out of scope for this document, which is about the C# library; it is the one item `TODO.md` carries as outstanding, in its own section at the top.

### Where a compile spends its time now

Every measurement taken during the investigation was of the priority-list calculation alone, because that was the cost. Once it stopped being the cost, nothing had re-measured the whole. `CompileScalingTests` does, timing everything inside a single compile through the public engine seams so the parts sum to the whole rather than being separate runs set against each other.

Layered graph, depth growing with size, 20 resources, through `VertexGraphCompiler.Compile`. Best of five after a discarded warm-up; the minimum rather than the mean, because the noise is additive:

| Activities | Depth | Compile | Priority list (incremental) | Full CPM passes | Scheduling tick loop | Everything else |
| - | - | - | - | - | - | - |
| 250 | 10 | 29 ms | 19 ms | 0 ms | 2 ms | 8 ms |
| 500 | 20 | 59 ms | 21 ms | 1 ms | 12 ms | 24 ms |
| 1,000 | 40 | 107 ms | 61 ms | 1 ms | 4 ms | 40 ms |
| 1,500 | 60 | 224 ms | 140 ms | 4 ms | 11 ms | 69 ms |
| 2,000 | 80 | 357 ms | 227 ms | 1 ms | 16 ms | 113 ms |

Three things follow.

**A compile at the activity limit now takes about a third of a second**, against 6.9 seconds when this investigation began. The aligned-rebuild and indirect-schedule stages are under a millisecond throughout and are omitted from the table.

**The scheduler is not the cost - 16 ms of 357 ms, around 4%.** Varying the resource count at 2,000 activities isolates `ActivityAt` directly, since its scan covers one resource's own schedule and so shrinks as the activities spread across more resources while the tick count stays roughly the same:

| Resources | Scheduling tick loop | Compile |
| - | - | - |
| 5 | 40 ms | 437 ms |
| 10 | 26 ms | 419 ms |
| 20 | 18 ms | 414 ms |
| 50 | 17 ms | 410 ms |
| 100 | 17 ms | 402 ms |
| 200 | 24 ms | 601 ms |

That is the predicted shape and then some: halving the per-resource load roughly halves the tick-loop time from 5 to 20 resources, which is the scan; it then flattens, and rises again at 200 resources as a different term takes over - `AdvanceCompletedActivities` iterates every schedule builder on every tick, so past some point adding resources costs more than it saves. **So the hypothesis is confirmed in shape and refuted in importance.** The worst case measured is 40 ms of a 437 ms compile, at a resource count no real plan would use. Indexing it would be correct and would buy nothing.

**"Everything else" is now the second-largest item, at 113 ms and growing faster than linearly, and it is unattributed.** It is everything outside the two engines: the builder clones, `AssignResourceDependencies`, `ResetResourceState`, `UpdateActivitySuccessors`, `RemoveResourceOnlyDependencies`, the pre- and post-compilation error checks - and the priority-list loop's own selection, which scans every activity twice per iteration and is therefore O(A^2) in its own right, around 8 x 10^6 activity visits at the limit. That last is the most likely single contributor and the obvious next candidate, but it has not been separated out and is not claimed here: the honest statement is that a third of a compile is now in code nobody has profiled, and the P6 getters dismissed as trivial above live in that third.

### Finding surfaced while adding regression tests

Transitive reduction does **not** scale to very deep graphs. Two parts:

- **Recursion (fixed).** `AncestorNodeCalculator.GetAncestorNodes` was recursive and overflowed the stack on deep chains; it is now an iterative post-order traversal (behaviour-identical). This removes the crash that a deep-reduction test hit.
- **O(N^2) memory (addressed).** The reducers now compute ancestors as compact bitsets (`AncestorBitSets`: dense node indexes, one bit per potential ancestor - N^2/8 bytes instead of N^2 HashSet entries at tens of bytes each), and `ReduceGraph` never materialises the dictionary-of-hashsets. A 20k-deep vertex chain (with a redundant shortcut edge that must be removed) now reduces in ~1s where it previously exhausted memory; `DeepTransitiveReductionTests` guards both flavours. The public `GetAncestorNodesLookup()` dictionary contract is unchanged - it is derived from the same bitset computation on demand, so its contents are identical (that API remains O(N^2) *entries* by contract). (The reducers now walk the compact bitsets directly; the dictionary form is materialised only for `GetAncestorNodesLookup()`.)

- **Arrow construction (fixed).** `ArrowGraphBuilder.AddActivity` intersected the whole edge set against the dependencies on every call (`EdgeIds.Intersect(dependencies)`) - O(E) per call, so O(N^2) building a chain (~90s for 20k activities). It now probes each dependency against the O(1) edge lookup, which is linear (~120ms for 20k). Behaviour is unchanged (verified against the arrow builder tests, which assert dummy-edge/event IDs). Its recursive `GetEdgesInDescendingOrder` (arrow edge-cleanup path) has since been made iterative as well - the risk recorded here turned out not to be latent at all: a 10,000-activity arrow chain overflowed the stack and killed the process, which is what the regression test was checked against. It carried a second, worse defect in the same walk; see "Recursive dummy-edge ordering on the arrow construction path" in `TODO.md`.

The remaining sections are the original plan for reference - written before the stateless-engine refactor, so some cited locations have since moved (notably the arrow redundant-dummy-edge walk, `RemoveRedundantIncomingDummyEdges`, which now lives in `ArrowTransitiveReducer` rather than `DummyEdgeOrchestrator`).

## Headline finding: no classic memory leaks

There is no unbounded-retention leak surface in the library:

- no static mutable collections, no `event` handlers, no `IDisposable`/finalizers, no unmanaged resources, no persistent caches;
- per-graph state lives in `VertexGraphState`'s five dictionaries and is fully emptied by `Reset()` / `Clear()`;
- the one shared static - `s_DefaultEventGenerator` (`VertexGraphBuilder.cs:24`) - is a single stateless instance.

So the real "memory" issue is **transient allocation churn / GC pressure**, not leaks. Every item below is throughput, allocation, or robustness - there is no leak to fix.

## Guardrails (apply to every phase)

- **Correctness invariant:** every change must be behaviour-preserving. The `ShuffleProcessingOrder` tests already prove order-independence - keep running them (both `false` and `true`) as the primary safety net, alongside the full test suite (currently 485 tests: 96 primitives, 389 compilers).
- **Measure, don't guess:** this landed, though not as the throwaway BenchmarkDotNet project suggested here. The harnesses are explicit-only tests living beside the corpora they measure - `MeasurePriorityListScaling` in `PriorityListEquivalenceTests` and `MeasureArrowCriticalPathScaling` in `ArrowCriticalPathEquivalenceTests`, with Rust counterparts marked `#[ignore]` - capturing wall-clock and allocated bytes over layered graphs at a range of sizes and depths. Keeping them in the test project rather than a separate one means they compile against every change and cannot rot silently. Run them from the xUnit v3 test assembly after a Release build; the invocations are in the comment above each. Every table in `TODO.md` came out of them.
- **Build the oracle first, and prove it can fail.** Adopted after the fact and worth stating as a rule, because it caught real defects several times over: before changing anything, write the test that pins the current behaviour, then deliberately break the code (or the baseline) and confirm the test notices. A baseline that has never failed is not yet evidence of anything. The priority-list baseline was checked by swapping two IDs, the arrow one by seeding a traversal bug, and the cancellation tests by removing the checks they exist to guard - which revealed that asserting "it threw" was not enough, since the exception still arrives after all the work has been done.
- **Memory leaks:** nothing to do (see above). All items are allocation/throughput/robustness.

## Findings, by leverage

The original survey, each item now carrying its outcome. Line numbers are as first written and have since moved; the status line under each is what to read.

### Tier 1 - algorithmic (the big costs on large graphs)

- **P1. CPM forward/backward flow is O(E^2) with per-pass allocations.** `VertexCriticalPathEngine.cs:154` (forward) and `:416` (backward). The label-correcting `while (remainingEdgeIds.Count != 0)` loop re-materialises `remainingEdgeIds.ToList()` every pass and, per edge, allocates `new HashSet<T>(dependencyNode.IncomingEdges)` plus a LINQ `.Select(state.Edge).Max(...)`. Worst case O(E) passes x O(E) = O(E^2); even the good case allocates O(E) hashsets per pass. Fix: topological-order pass (Kahn / per-node remaining-incoming counter) - O(V+E), near-zero per-edge allocation.

  **DONE**, in both engines and both languages, exactly as proposed. The per-pass hash sets went too: with a node-order walk, "is this edge done" is answered by a per-node outstanding count reaching zero.

- **P2. Priority-list calc runs a full CPM per activity.** `VertexGraphBuilder.cs:1011` - the loop calls `CalculateCriticalPath()` (forward+backward) once per critical activity extracted (O(A) full passes), pulling a single activity and zeroing its duration each time. Combined with P1 that is **O(A.E^2)** - the dominant compile cost. With P1 it drops to O(A.(V+E)); longer term an incremental scheme could avoid full re-passes.

  **DONE for vertex**, including the incremental scheme this entry files under "longer term" - it turned out to be the single largest remaining factor once P1 landed. Not done for arrow, and deliberately: see the outstanding list above.

- **P3. Scheduler recomputes dependency sets every time tick.** `PriorityListResourceScheduler.cs:117` - `PromoteReadyActivities` calls `graph.StrongActivityDependencyIds(activityId)` (recursive edge-walk + `new HashSet`) for every not-yet-ready activity on **every** `timeCounter` tick, though dependencies never change during scheduling. `ActivityAt` (`:86`) is also a linear scan per builder per tick. Fix: precompute each activity's strong-dependency set once; track completion incrementally. O(timeSpan.A.deps) -> ~O(A.deps).

  **DONE for the dependency sets.** The `ActivityAt` half was not done and is the one outstanding item with a plausible cost - see the outstanding list above.

- **P4. Redundant full-graph clones.** `CalculateResourceSchedulesByPriorityList` clones the builder (`VertexGraphBuilder.cs:934`) and then clones *that clone again* for the priority list (`:938-939`); each `CloneObject` -> `ToGraph` cleans edges and deep-copies every edge and node. The compiler additionally deep-clones all activities 2-3x per `Compile` (`VertexGraphCompiler.cs:250,283,308`). Fix: remove the double clone; reuse one working copy.

  **REJECTED** for the double clone (load-bearing - see the reassessment above), and the "2-3x per `Compile`" is a miscount: those branches are mutually exclusive, so it is one clone per compile.

### Tier 2 - allocation churn (GC pressure, smaller graphs)

- **P5. `SetActivityDependencies` LINQ storms** - `VertexGraphBuilder.cs:1086-1212`: four blocks each recompute `Dependencies.Union(...).Union(...).ToList()` and re-walk `ActivityDependencyIds`. Compute the shared sets once. (40 `Union/Except/Intersect` calls live in this one file.) **REJECTED** - mutation-order-dependent, see the reassessment above.
- **P6. Recomputing property getters** - `ActivityIds`, `StartTime`, `FinishTime`, `Activities` are LINQ-over-`Values` each access; `ActivityIds.Contains(id)` (`:1096`) is an O(A) scan when `m_State.ContainsNode` is O(1). Use the dictionary directly; materialise once where enumerated repeatedly. **PARTLY DONE** - the O(A) membership scan is gone; the getters remain, and are trivial.
- **P7. `ResourceScheduleBuilder.ScheduledActivities` copies every access** (`ResourceScheduleBuilder.cs:63`, `.ToList()`), and the scheduler calls it repeatedly via `.Any()`/`.Select()`. Expose a non-copying enumerable/count. **NOT DONE, and the premise is wrong** - no call site is in the tick loop, so this is a handful of copies per compile rather than a hot path.

### Tier 3 - robustness (same class as the earlier deep-graph Tarjan fix)

- **P8. `VertexTransitiveReducer.RemoveRedundantIncomingEdges` is recursive and un-memoized** - `VertexTransitiveReducer.cs:75` re-descends into tail nodes for every end node with no visited-set (redundant work on shared ancestors; deep chains risk `StackOverflow`, the same failure mode already fixed in Tarjan). `StrongActivityDependencyIds` (`:689`) is likewise recursive over dummy chains. The arrow reducer delegates to a recursive `DummyEdgeOrchestrator.RemoveRedundantIncomingDummyEdges` with the same shape. Fix: visited-set + iterative traversal.

  **DONE**, and the tier was right to flag it: every recursive traversal in the library has now been made iterative, and two of them were confirmed to kill the process on deep chains rather than merely being theoretical risks.

### Arrow parity

The arrow builder mirrors P1-P4/P8 on the *rendering* path:

- `ArrowCriticalPathEngine` - same node-space label-correcting loops (`:70-96`, `:200+`). **DONE**, and it bought nothing - the arrow critical path was never the cost. Worth keeping anyway, as it removes a quadratic sweep against a future caller that drives the arrow path hard.
- `ArrowGraphBuilder` - same double-clone priority list (`:685`, `:687`) and `while (cont)` loop (`:871`). Double clone **REJECTED** with P4; the loop is **NOT DONE** and is the largest outstanding item, with no caller.
- `ArrowTransitiveReducer` -> `DummyEdgeOrchestrator.RemoveRedundantIncomingDummyEdges` - same un-memoized recursion. **DONE**, and measuring this path is what uncovered the 60-second, 158 GB defect described above.

## Phased plan

Each phase is independently shippable behind the green suite + benchmark deltas.

The plan below was largely followed, but not in this order and not with these boundaries. What actually happened: Phase 2 and Phase 4 went first as self-contained wins; Phase 3 was then undertaken as part of the priority-list investigation, because P1 and P2 turned out to be the same bottleneck rather than two; Phase 1's items were reassessed one by one and mostly rejected or found trivial; and Phase 5 was driven by measurement rather than by parity, which is why it delivered the largest single win in the investigation from an item this plan never mentions. The phase numbering also collides with the priority-list phases in `TODO.md`, which are a different sequence entirely - "Phase 3" here is the topological CPM, "Phase 3" there is the incremental recalculation.

### Phase 0 - Baseline & safety
Benchmark harness + allocation baseline. Audit that `ShuffleProcessingOrder=true` variants exist for the CPM and scheduling paths we will touch (add any missing). No production changes.

### Phase 1 - Low-risk allocation & clone cuts (behaviour-preserving)
Highest value-to-risk ratio; no algorithm changes.
- **P4 double-clone:** remove the second `CloneObject()` in `CalculateResourceSchedulesByPriorityList` (`VertexGraphBuilder.cs:934-939`); reuse one working copy for both the schedule and the priority list. Same for arrow (`ArrowGraphBuilder.cs:685/687`).
- **P6 getters/membership:** replace `ActivityIds.Contains(id)` (`:1096`) with the O(1) `m_State.ContainsNode(id)`; materialise `Activities`/`ActivityIds` once where enumerated repeatedly in a method.
- **P5 `SetActivityDependencies`:** compute the shared `Dependencies u Planning u Resource` and `ActivityDependencyIds` sets once per call instead of per block (`:1086-1212`).
- **P7 `ScheduledActivities`:** stop returning `m_ScheduledActivities.ToList()` on every access (`ResourceScheduleBuilder.cs:63`); expose a non-copying enumerable/count for the scheduler's `.Any()`/`.Select()` hot calls.
- **Compiler:** de-duplicate the repeated `Activities.ToList()` / activity deep-clones in `Compile` (`VertexGraphCompiler.cs:242,278,280,283,308`).

### Phase 2 - Cached-dependency scheduler (P3)
In `PriorityListResourceScheduler`, precompute each activity's strong dependency set once before the tick loop instead of calling `graph.StrongActivityDependencyIds` for every pending activity on every `timeCounter` (`:117`); track completion incrementally. Optionally index `ActivityAt` (`:86`) so completion detection is not a per-builder linear scan. Turns O(timeSpan.A.deps) into ~O(A.deps). Risk: medium - the ready/started/completed bookkeeping must stay identical; covered by scheduling tests.

### Phase 3 - Topological CPM (P1 -> P2)
Replace the label-correcting `while (remainingEdgeIds...)` passes in `VertexCriticalPathEngine` forward (`:154`) and backward (`:416`) with a single Kahn-style topological sweep driven by per-node remaining-incoming/outgoing counters - O(V+E), eliminating the per-pass `ToList()` and per-edge `new HashSet(IncomingEdges)`. Biggest single win and it compounds through P2 (`CalculateCriticalPathPriorityList` runs a full CPM per activity, `:1011`). Keep the `shuffle` hook (shuffle the ready frontier) so order-independence tests still exercise it. Risk: highest - most test scrutiny; do vertex first, validate, then mirror.

### Phase 4 - Deep-graph robustness (P8)
Make `VertexTransitiveReducer.RemoveRedundantIncomingEdges` (`:75`) iterative with a visited-set (removes both the `StackOverflow` risk on deep chains and redundant re-descent into shared ancestors); same for `StrongActivityDependencyIds` (`:689`) and the arrow `DummyEdgeOrchestrator.RemoveRedundantIncomingDummyEdges`. Add a deep-chain (tens-of-thousands) test mirroring the existing `TarjanDeepGraphTests`.

### Phase 5 - Arrow parity
Apply Phases 1-4 to the rendering path: `ArrowCriticalPathEngine` node-space loops (`:70-96`, `:200+`), `ArrowGraphBuilder` double-clone/priority-list, `ArrowTransitiveReducer` / `DummyEdgeOrchestrator`. Kept last because analysis runs on the vertex path.

## Sequencing rationale

Phase 1 banks safe allocation wins immediately; Phases 2-3 attack the O(A.E^2) compile cost (the thing that bites at "low thousands"); Phase 4 removes the deep-graph crash risk; Phase 5 brings arrow to parity. Each phase is independently shippable behind the green suite + benchmark deltas.

### What the sequencing actually taught

Three things, worth carrying into whatever comes next.

**Measure the thing you are about to change, not the thing you assume is slow.** The arrow critical-path rewrite was undertaken for parity and delivered no measurable gain, because those passes were already sub-millisecond at every size tried. But the harness built to measure it is what exposed a 60-second, 158 GB defect sitting next to them, in a method this plan never lists. The rewrite was not the win; measuring in order to justify the rewrite was.

**"Low priority because it runs once per compile" is an argument about the caller, not the code.** It held for the arrow critical path and it was recorded here as the reason to defer that work. It said nothing about the redundant-edge removal that runs in the same call, which was exponential in graph depth. Cheap-per-call and cheap-per-compile are different claims.

**An estimate measured on one graph shape does not transfer to another.** The figure that the project finish time moves in 5% of priority-list iterations was measured on shallow graphs and used to justify falling back to a full recalculation whenever it moved. On deep graphs it moves almost every iteration, because the selection picks an activity on the critical path by construction. The correction is in "Priority-list calculation" in `TODO.md`; the general lesson is that a percentage measured over a corpus needs its corpus stated beside it.
