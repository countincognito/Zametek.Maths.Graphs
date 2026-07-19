# Zametek.Maths.Graphs - Rust port

A Rust recreation of the .NET **Zametek.Maths.Graphs** solution: a headless library implementing the Critical Path Method (CPM) and priority-list resource scheduling over project-planning graphs.

The workspace mirrors the two NuGet packages:

| C# project | Rust crate |
| --- | --- |
| `Zametek.Maths.Graphs.Primitives` | [`primitives`](primitives) (`zametek-maths-graphs-primitives`) |
| `Zametek.Maths.Graphs.Compilers` | [`compilers`](compilers) (`zametek-maths-graphs-compilers`) |

```sh
cd rust
cargo test
```

## What is preserved

The port reproduces the original's inputs and outputs:

- **Domain model** - `Activity`, `DependentActivity`, `Event`, `Edge`, `Node`, `Graph`, `Resource`, `ResourceSchedule`, `ScheduledActivity`, `WorkStream`, `GraphCompilation` and the error/enums types, with the same derived values (total slack, interfering slack, latest start time, criticality) and the same equality semantics (edges by ID, nodes by ID + type + edge sets, etc.).
- **Activity-on-Vertex pipeline** - `VertexGraphBuilder` / `VertexGraphCompiler`: dynamic dependency resolution (including out-of-order insertion via unsatisfied successors), Tarjan cycle detection, transitive reduction over compact ancestor bitsets, the forward/backward CPM passes with all constraint handling (`minimum_earliest_start_time`, `maximum_latest_finish_time`, `minimum_free_slack`), isolated-node backfill, priority-list resource scheduling (AND / OR / ACTIVE_AND resource operators, explicit targets, allocation order, synthetic resources for the infinite-resource case), resource-dependency wiring, per-time-unit resource/cost/billing/effort/activity allocation streams, work-stream phase reporting, and the full `P0010`–`P0060`/`C0010` compilation-error reporting with the original message text.
- **Activity-on-Arrow pipeline** - `ArrowGraphBuilder` / `ArrowGraphCompiler`: dummy-edge orchestration (creation, redirection, parallel/redundant removal, node merging), transitive reduction, the event-based CPM passes, and graph export for rendering.
- **Determinism** - insertion-ordered maps/sets (`indexmap`) stand in for the C# `Dictionary`/`HashSet`, whose iteration order the original's outputs depend on (schedule tie-breaking, error-message ordering, Tarjan component order). The `shuffle_processing_order` hook is kept so tests can prove the CPM passes are order-independent.

The test suites port representative scenarios from the C# tests **with the expected values copied verbatim**, including the canonical nine-activity network's schedules, CPM values, resource dependencies and successors, and the exact compilation-error message strings.

## What is intentionally different

Per the port's brief, patterns are not recreated like-for-like - only the behaviour:

- **Generics** - the C# generic constraints (`where T : struct, IComparable, IEquatable`) become the `Key` trait (implemented for all integer types; implement it for your own copyable ID type to match the C# `Guid` extension point).
- **Class hierarchy** - `DependentActivity` *contains* an `Activity` (and derefs to it) instead of inheriting; the builders/compilers work with `DependentActivity` throughout. A plain activity is simply one with empty dependency sets, so no expressiveness is lost.
- **Dependency injection** - the port mirrors the C# DI: the engine seams are public traits in the `contracts` module, with the same engines-bundle constructors (`VertexGraphBuilderEngines` / `ArrowGraphBuilderEngines`), their defaults and the injecting constructors, and - as in C# - the engines are stateless (the builder passes them the graph state and any collaborators per call), so neither port has factory seams. Two choices are deliberately un-idiomatic for parity: the traits keep the C# `I` prefix (e.g. `IVertexCriticalPathEngine`) so they grep 1:1 against the reference and never collide with the identically-named default structs, and the one stateful seam - ID generation (`NextIdGenerator` / `PreviousIdGenerator`) - is held by value and recreated on clone rather than shared behind an `Arc`.
- **Thread safety** - the C# compilers serialise access with an internal lock; the Rust compilers take `&mut self`, so exclusive access is enforced at compile time (wrap in a `Mutex` to share across threads).
- **Errors** - where the C# throws (`InvalidOperationException`, `ArgumentException`) the port returns `Result<_, GraphError>` carrying the same message text; where the C# returns `false` the port does too. Multi-line error messages join lines with `\n` rather than the platform-dependent `Environment.NewLine`.
- **Omissions** - the arrow builder's `AddActivityDependencies` / `RemoveActivity` / `RemoveActivityDependencies` members, which throw `NotImplementedException` in C#, are omitted rather than ported as always-failing methods.

## Examples

Two runnable examples live in `compilers/examples`:

- `cargo run --example basic` - the three-activity snippet below.
- `cargo run --example sample_project` - compiles the sample project from the [Zametek.ProjectPlan wiki](https://github.com/countincognito/Zametek.ProjectPlan/wiki) (hard-coded in the example: 46 activities, 15 resources, mixed None/Direct/Indirect allocation types and explicit targets) and prints each resource's schedule with idle gaps marked. The output is asserted against the schedule computed by the original .NET library - project end at day 215 - so a successful run is an end-to-end proof of parity on a real-world input.

```rust
use zametek_maths_graphs_compilers::VertexGraphCompiler;
use zametek_maths_graphs_primitives::DependentActivity;

let mut compiler: VertexGraphCompiler<i32, i32, i32> = VertexGraphCompiler::new();
compiler.add_activity(DependentActivity::new(1, 6));
compiler.add_activity(DependentActivity::new(2, 7));
compiler.add_activity(DependentActivity::with_dependencies(3, 8, [1, 2]));

let compilation = compiler.compile().unwrap();

assert!(compilation.compilation_errors.is_empty());
let a3 = compilation.dependent_activities.iter().find(|a| a.id() == 3).unwrap();
assert_eq!(a3.earliest_start_time, Some(7));
assert_eq!(a3.earliest_finish_time(), Some(15));
```
