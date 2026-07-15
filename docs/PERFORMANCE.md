# Performance & memory plan

A phased plan to reduce compile wall-clock time, cut allocation/GC pressure, and
harden the deep-graph paths in `Zametek.Maths.Graphs`. Nothing here is implemented
yet — it is a backlog to work through, each phase shippable behind the green test
suite.

Target scale: **low thousands of activities** (interactive project plans, repeated
re-compiles on edit). Scope: **vertex (analysis) and arrow (rendering) paths**.

## Implementation status

Applied (all behaviour-preserving; full test suite green):

- **Phase 2** - the scheduler resolves each activity's strong dependency set once
  before the tick loop instead of re-walking the graph on every time tick
  (`PriorityListResourceScheduler`). Shared by both graph flavours.
- **Phase 3, scoped to the allocation cut** - the CPM label-correcting loops
  (`VertexCriticalPathEngine`, `ArrowCriticalPathEngine`) no longer build a throwaway
  `HashSet` per edge/node per pass; a zero-allocation membership test is used and each
  node's own edge set is read directly (it is not mutated during CPM). The full
  O(V+E) topological restructure is **deferred** (higher risk; wants the benchmark
  harness and a dedicated review to prove it stays behaviour-identical).
- **Phase 4** - the transitive reducers (`VertexTransitiveReducer`,
  `DummyEdgeOrchestrator.RemoveRedundantIncomingDummyEdges`) and the strong-dependency
  walks (`VertexGraphBuilder`/`ArrowGraphBuilder.StrongActivityDependencyIds`) are now
  iterative with visited sets - no more StackOverflow risk on deep chains, and no
  redundant re-traversal of shared sub-paths. `DeepGraphReductionTests` covers the
  vertex paths.
- **Phase 1 (partial)** - `SetActivityDependencies` uses an O(1) node-key lookup (P6).

Reassessed as **not** behaviour-safe and intentionally left as-is:

- **P4 (remove the double clone)** - load-bearing: the priority-list clone is destroyed
  (durations zeroed, CPM state overwritten) while the separate scheduling clone must
  retain valid CPM times. Collapsing them would feed the scheduler corrupted times.
- **P5 (dedup `SetActivityDependencies` LINQ)** - mutation-order-dependent; each of the
  four blocks re-reads state (dependencies, graph edges) that the previous block mutated.
- **Phase 3 full topological CPM** - deferred as above.

### Finding surfaced while adding regression tests

Transitive reduction does **not** scale to very deep graphs. Two parts:

- **Recursion (fixed).** `AncestorNodeCalculator.GetAncestorNodes` was recursive and
  overflowed the stack on deep chains; it is now an iterative post-order traversal
  (behaviour-identical). This removes the crash that a deep-reduction test hit.
- **O(N^2) memory (open).** The ancestor lookup still stores, for each node, the set
  of *all* its ancestors - O(N^2) for a linear chain. A chain long enough to matter
  exhausts memory, so a deep-chain *reduction* regression test remains impractical
  (it would be flaky). Making reduction scale needs a different approach that does not
  materialise full ancestor sets - a genuine algorithmic redesign, out of scope for a
  behaviour-preserving pass, and wants the benchmark harness.

- **Arrow construction (fixed).** `ArrowGraphBuilder.AddActivity` intersected the whole
  edge set against the dependencies on every call (`EdgeIds.Intersect(dependencies)`) -
  O(E) per call, so O(N^2) building a chain (~90s for 20k activities). It now probes each
  dependency against the O(1) edge lookup, which is linear (~120ms for 20k). Behaviour is
  unchanged (verified against the arrow builder tests, which assert dummy-edge/event IDs).
  Its recursive `GetEdgesInDescendingOrder` (arrow edge-cleanup path) is still recursive
  and remains a candidate for the same iterative treatment if deep arrow graphs are used.

The remaining sections are the original plan for reference.

## Headline finding: no classic memory leaks

There is no unbounded-retention leak surface in the library:

- no static mutable collections, no `event` handlers, no `IDisposable`/finalizers,
  no unmanaged resources, no persistent caches;
- per-graph state lives in `VertexGraphState`'s five dictionaries and is fully
  emptied by `Reset()` / `Clear()`;
- the one shared static — `s_DefaultEventGenerator` (`VertexGraphBuilder.cs:24`) —
  is a single stateless instance.

So the real "memory" issue is **transient allocation churn / GC pressure**, not
leaks. Every item below is throughput, allocation, or robustness — there is no
leak to fix.

## Guardrails (apply to every phase)

- **Correctness invariant:** every change must be behaviour-preserving. The
  `ShuffleProcessingOrder` tests already prove order-independence — keep running
  them (both `false` and `true`) as the primary safety net, alongside the full
  test suite (currently 397 tests).
- **Measure, don't guess:** add a small BenchmarkDotNet (or stopwatch) harness in
  a throwaway project over representative graphs (~200, ~1k, ~5k activities;
  chain, diamond/dense, and wide-parallel shapes) capturing wall-clock **and**
  allocated bytes. Land it in Phase 0 and re-run it per phase to confirm each
  change helps and none regresses.
- **Memory leaks:** nothing to do (see above). All items are
  allocation/throughput/robustness.

## Findings, by leverage

### Tier 1 — algorithmic (the big costs on large graphs)

- **P1. CPM forward/backward flow is O(E^2) with per-pass allocations.**
  `VertexCriticalPathEngine.cs:154` (forward) and `:416` (backward). The
  label-correcting `while (remainingEdgeIds.Count != 0)` loop re-materialises
  `remainingEdgeIds.ToList()` every pass and, per edge, allocates
  `new HashSet<T>(dependencyNode.IncomingEdges)` plus a LINQ
  `.Select(state.Edge).Max(...)`. Worst case O(E) passes x O(E) = O(E^2); even the
  good case allocates O(E) hashsets per pass. Fix: topological-order pass (Kahn /
  per-node remaining-incoming counter) — O(V+E), near-zero per-edge allocation.

- **P2. Priority-list calc runs a full CPM per activity.**
  `VertexGraphBuilder.cs:1011` — the loop calls `CalculateCriticalPath()`
  (forward+backward) once per critical activity extracted (O(A) full passes),
  pulling a single activity and zeroing its duration each time. Combined with P1
  that is **O(A.E^2)** — the dominant compile cost. With P1 it drops to
  O(A.(V+E)); longer term an incremental scheme could avoid full re-passes.

- **P3. Scheduler recomputes dependency sets every time tick.**
  `PriorityListResourceScheduler.cs:117` — `PromoteReadyActivities` calls
  `graph.StrongActivityDependencyIds(activityId)` (recursive edge-walk +
  `new HashSet`) for every not-yet-ready activity on **every** `timeCounter` tick,
  though dependencies never change during scheduling. `ActivityAt` (`:86`) is also
  a linear scan per builder per tick. Fix: precompute each activity's
  strong-dependency set once; track completion incrementally.
  O(timeSpan.A.deps) -> ~O(A.deps).

- **P4. Redundant full-graph clones.** `CalculateResourceSchedulesByPriorityList`
  clones the builder (`VertexGraphBuilder.cs:934`) and then clones *that clone
  again* for the priority list (`:938-939`); each `CloneObject` -> `ToGraph`
  cleans edges and deep-copies every edge and node. The compiler additionally
  deep-clones all activities 2-3x per `Compile`
  (`VertexGraphCompiler.cs:250,283,308`). Fix: remove the double clone; reuse one
  working copy.

### Tier 2 — allocation churn (GC pressure, smaller graphs)

- **P5. `SetActivityDependencies` LINQ storms** — `VertexGraphBuilder.cs:1086-1212`:
  four blocks each recompute `Dependencies.Union(...).Union(...).ToList()` and
  re-walk `ActivityDependencyIds`. Compute the shared sets once. (40
  `Union/Except/Intersect` calls live in this one file.)
- **P6. Recomputing property getters** — `ActivityIds`, `StartTime`, `FinishTime`,
  `Activities` are LINQ-over-`Values` each access; `ActivityIds.Contains(id)`
  (`:1096`) is an O(A) scan when `m_State.ContainsNode` is O(1). Use the
  dictionary directly; materialise once where enumerated repeatedly.
- **P7. `ResourceScheduleBuilder.ScheduledActivities` copies every access**
  (`ResourceScheduleBuilder.cs:63`, `.ToList()`), and the scheduler calls it
  repeatedly via `.Any()`/`.Select()`. Expose a non-copying enumerable/count.

### Tier 3 — robustness (same class as the earlier deep-graph Tarjan fix)

- **P8. `VertexTransitiveReducer.RemoveRedundantIncomingEdges` is recursive and
  un-memoized** — `VertexTransitiveReducer.cs:75` re-descends into tail nodes for
  every end node with no visited-set (redundant work on shared ancestors; deep
  chains risk `StackOverflow`, the same failure mode already fixed in Tarjan).
  `StrongActivityDependencyIds` (`:689`) is likewise recursive over dummy chains.
  The arrow reducer delegates to a recursive
  `DummyEdgeOrchestrator.RemoveRedundantIncomingDummyEdges` with the same shape.
  Fix: visited-set + iterative traversal.

### Arrow parity

The arrow builder mirrors P1-P4/P8 on the *rendering* path:

- `ArrowCriticalPathEngine` — same node-space label-correcting loops
  (`:70-96`, `:200+`).
- `ArrowGraphBuilder` — same double-clone priority list (`:685`, `:687`) and
  `while (cont)` loop (`:871`).
- `ArrowTransitiveReducer` -> `DummyEdgeOrchestrator.RemoveRedundantIncomingDummyEdges`
  — same un-memoized recursion.

## Phased plan

Each phase is independently shippable behind the green suite + benchmark deltas.

### Phase 0 — Baseline & safety
Benchmark harness + allocation baseline. Audit that `ShuffleProcessingOrder=true`
variants exist for the CPM and scheduling paths we will touch (add any missing).
No production changes.

### Phase 1 — Low-risk allocation & clone cuts (behaviour-preserving)
Highest value-to-risk ratio; no algorithm changes.
- **P4 double-clone:** remove the second `CloneObject()` in
  `CalculateResourceSchedulesByPriorityList` (`VertexGraphBuilder.cs:934-939`);
  reuse one working copy for both the schedule and the priority list. Same for
  arrow (`ArrowGraphBuilder.cs:685/687`).
- **P6 getters/membership:** replace `ActivityIds.Contains(id)` (`:1096`) with the
  O(1) `m_State.ContainsNode(id)`; materialise `Activities`/`ActivityIds` once
  where enumerated repeatedly in a method.
- **P5 `SetActivityDependencies`:** compute the shared
  `Dependencies u Planning u Resource` and `ActivityDependencyIds` sets once per
  call instead of per block (`:1086-1212`).
- **P7 `ScheduledActivities`:** stop returning `m_ScheduledActivities.ToList()` on
  every access (`ResourceScheduleBuilder.cs:63`); expose a non-copying
  enumerable/count for the scheduler's `.Any()`/`.Select()` hot calls.
- **Compiler:** de-duplicate the repeated `Activities.ToList()` / activity
  deep-clones in `Compile` (`VertexGraphCompiler.cs:242,278,280,283,308`).

### Phase 2 — Cached-dependency scheduler (P3)
In `PriorityListResourceScheduler`, precompute each activity's strong dependency
set once before the tick loop instead of calling `graph.StrongActivityDependencyIds`
for every pending activity on every `timeCounter` (`:117`); track completion
incrementally. Optionally index `ActivityAt` (`:86`) so completion detection is not
a per-builder linear scan. Turns O(timeSpan.A.deps) into ~O(A.deps). Risk: medium —
the ready/started/completed bookkeeping must stay identical; covered by scheduling
tests.

### Phase 3 — Topological CPM (P1 -> P2)
Replace the label-correcting `while (remainingEdgeIds...)` passes in
`VertexCriticalPathEngine` forward (`:154`) and backward (`:416`) with a single
Kahn-style topological sweep driven by per-node remaining-incoming/outgoing
counters — O(V+E), eliminating the per-pass `ToList()` and per-edge
`new HashSet(IncomingEdges)`. Biggest single win and it compounds through P2
(`CalculateCriticalPathPriorityList` runs a full CPM per activity, `:1011`). Keep
the `shuffle` hook (shuffle the ready frontier) so order-independence tests still
exercise it. Risk: highest — most test scrutiny; do vertex first, validate, then
mirror.

### Phase 4 — Deep-graph robustness (P8)
Make `VertexTransitiveReducer.RemoveRedundantIncomingEdges` (`:75`) iterative with
a visited-set (removes both the `StackOverflow` risk on deep chains and redundant
re-descent into shared ancestors); same for `StrongActivityDependencyIds` (`:689`)
and the arrow `DummyEdgeOrchestrator.RemoveRedundantIncomingDummyEdges`. Add a
deep-chain (tens-of-thousands) test mirroring the existing `TarjanDeepGraphTests`.

### Phase 5 — Arrow parity
Apply Phases 1-4 to the rendering path: `ArrowCriticalPathEngine` node-space loops
(`:70-96`, `:200+`), `ArrowGraphBuilder` double-clone/priority-list,
`ArrowTransitiveReducer` / `DummyEdgeOrchestrator`. Kept last because analysis runs
on the vertex path.

## Sequencing rationale

Phase 1 banks safe allocation wins immediately; Phases 2-3 attack the O(A.E^2)
compile cost (the thing that bites at "low thousands"); Phase 4 removes the
deep-graph crash risk; Phase 5 brings arrow to parity. Each phase is independently
shippable behind the green suite + benchmark deltas.
