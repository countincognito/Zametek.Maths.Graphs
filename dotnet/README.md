# Zametek.Maths.Graphs

[![NuGet Version](https://img.shields.io/nuget/v/Zametek.Maths.Graphs.Primitives.svg)](https://www.nuget.org/packages/Zametek.Maths.Graphs.Primitives "NuGet Version") **Zametek.Maths.Graphs.Primitives**

[![NuGet Version](https://img.shields.io/nuget/v/Zametek.Maths.Graphs.Compilers.svg)](https://www.nuget.org/packages/Zametek.Maths.Graphs.Compilers "NuGet Version") **Zametek.Maths.Graphs.Compilers**

A headless library for building and compiling vertex (Activity-on-Vertex) and arrow (Activity-on-Arrow) directed graphs for project scheduling. It provides dynamic dependency resolution, transitive reduction, edge redirection, critical path calculation, activity priority calculation, and mapped/unmapped resource scheduling.

- **Zametek.Maths.Graphs.Primitives** - the domain model: activities, events, edges, nodes, graphs, resources, resource schedules and work streams, plus their interfaces and enums.
- **Zametek.Maths.Graphs.Compilers** - the builders, compilers and engines that turn a set of dependent activities into a compiled schedule.

## Background: critical path analysis

This library is an implementation of the [Critical Path Method](https://en.wikipedia.org/wiki/Critical_path_method) (CPM), the classic technique for scheduling a set of project activities. Given a list of activities, each with a **duration** and a set of **dependencies** (the activities that must finish before it can start), CPM works out:

- the earliest and latest each activity can start and finish without delaying the project, and
- the **critical path** - the longest chain of dependent activities, which determines the shortest possible project duration.

The calculation is two passes over the dependency graph:

- a **forward pass** computes, for every activity, the **earliest start** (`ES`) and **earliest finish** (`EF = ES + duration`). An activity cannot start until all its dependencies have finished, so its `ES` is the maximum `EF` of its dependencies.
- a **backward pass** computes the **latest start** (`LS`) and **latest finish** (`LF`) that still allow the project to finish on time.

From these come three measures of [float, or slack](https://en.wikipedia.org/wiki/Float_(project_management)):

- **total slack** - how long an activity can slip without delaying the *project*.
- **free slack** - how long it can slip without delaying *any* of its successors.
- **interfering slack** - the part of total slack whose use would eat into a successor's float (total slack minus free slack).

An activity with **zero total slack** is on the **critical path**: any delay to it delays the whole project. The closely related [PERT](https://en.wikipedia.org/wiki/Program_evaluation_and_review_technique) technique builds on the same network model. The library exposes all three slack values plus an `IsCritical` flag on each activity (see [Slack: total, free and interfering](#slack-total-free-and-interfering)).

On top of the basic method the library also performs [transitive reduction](https://en.wikipedia.org/wiki/Transitive_reduction) (removing redundant dependencies), cycle detection (via [Tarjan's strongly connected components algorithm](https://en.wikipedia.org/wiki/Tarjan%27s_strongly_connected_components_algorithm)), and resource-constrained scheduling (see [Scheduling with resources](#scheduling-with-resources)).

## Quick start

Install the compilers package (it pulls in the primitives):

```
dotnet add package Zametek.Maths.Graphs.Compilers
```

The fastest path is the Activity-on-Vertex compiler. Add activities (each with an id, a duration and optional dependency ids), then `Compile(...)`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Zametek.Maths.Graphs;

// Type parameters: <activity-id, resource-id, work-stream-id, activity-type>.
var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

//                                                   id  duration
compiler.AddActivity(new DependentActivity<int, int, int>(1, 6));
compiler.AddActivity(new DependentActivity<int, int, int>(2, 7));
//                                                   id  duration  dependencies
compiler.AddActivity(new DependentActivity<int, int, int>(3, 8, new[] { 1, 2 }));
compiler.AddActivity(new DependentActivity<int, int, int>(4, 4, new[] { 3 }));

// No resources supplied => infinite resources (pure critical-path schedule).
// Every Compile form takes a CancellationToken; pass a real one to make long
// compiles cancellable, or CancellationToken.None to opt out.
IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = compiler.Compile(CancellationToken.None);

if (compilation.CompilationErrors.Any())
{
    foreach (IGraphCompilationError error in compilation.CompilationErrors)
    {
        Console.WriteLine($"{error.ErrorCode}: {error.ErrorMessage}");
    }
    return;
}

foreach (IDependentActivity<int, int, int> activity in compilation.DependentActivities.OrderBy(x => x.Id))
{
    bool isCritical = activity.TotalSlack == 0;
    Console.WriteLine(
        $"Activity {activity.Id}: " +
        $"ES={activity.EarliestStartTime}, EF={activity.EarliestFinishTime}, " +
        $"LS={activity.LatestStartTime}, LF={activity.LatestFinishTime}, " +
        $"slack={activity.TotalSlack}{(isCritical ? "  <- critical path" : string.Empty)}");
}

Console.WriteLine($"Project runs from {compiler.StartTime} to {compiler.FinishTime}.");
```

Running it prints the following (no resources were supplied, so this is the pure critical-path schedule):

```text
Activity 1: ES=0, EF=6, LS=1, LF=7, slack=1
Activity 2: ES=0, EF=7, LS=0, LF=7, slack=0  <- critical path
Activity 3: ES=7, EF=15, LS=7, LF=15, slack=0  <- critical path
Activity 4: ES=15, EF=19, LS=15, LF=19, slack=0  <- critical path
Project runs from 0 to 19.
```

Activities 2, 3 and 4 form the critical path (`2 -> 3 -> 4`), so the project takes 19 time units. Activity 1 carries one unit of total slack: it could start as late as time 1 (its `LS`) without pushing out the finish.

There is also an `ArrowGraphCompiler`, but it is meant for *rendering* an Activity-on-Arrow diagram, not for analysis - always analyse with the vertex compiler. See [Choosing a compiler](#choosing-a-compiler-analyse-with-vertex-render-with-arrow) below.

## Activity-on-Vertex vs Activity-on-Arrow

The same project network can be drawn two ways, and the library supports both. They are duals of one another - the activities and the connectors swap roles:

| | Activity-on-Vertex (AoV) | Activity-on-Arrow (AoA) |
| - | - | - |
| Also known as | [precedence diagram / activity-on-node](https://en.wikipedia.org/wiki/Precedence_diagram_method) | arrow diagram / [PERT](https://en.wikipedia.org/wiki/Program_evaluation_and_review_technique) chart |
| Activities are | **nodes** | **edges (arrows)** |
| Connectors are | edges (the dependencies) | **nodes (events / milestones)** |
| Extra structure | none | **dummy edges** inserted to preserve dependencies |
| Compiler | `VertexGraphCompiler` | `ArrowGraphCompiler` |
| `ToGraph()` returns | `Graph<T, IEvent<T>, TDependentActivity>` | `Graph<T, TDependentActivity, IEvent<T>>` |

In an **AoV** graph each activity is a node and an arrow from A to B simply means "B depends on A". It maps directly onto your input, needs no extra elements, and is what the critical-path engine actually runs on.

An **AoA** graph is the traditional hand-drawn network: activities are arrows between numbered event-nodes. Because not every dependency pattern can be expressed that way, zero-duration **dummy edges** have to be inserted automatically. This makes AoA larger and more expensive to build, and primarily useful as a *diagram*.

The domain types (in **Zametek.Maths.Graphs.Primitives**) are:

- `IActivity<…>` / `Activity<…>` - a unit of work: its `Duration`, the computed `EarliestStartTime` / `LatestStartTime` / `EarliestFinishTime` / `LatestFinishTime`, and `FreeSlack` / `TotalSlack`.
- `IDependentActivity<…>` / `DependentActivity<…>` - an activity that also carries `Dependencies` (and `PlanningDependencies`). This is what you feed the compilers.
- `IEvent<…>` / `Event<…>` - a start/end marker on the graph; created and managed for you.
- `IResource<…>` / `Resource<…>` - something that performs activities.
- `IWorkStream<…>` / `WorkStream<…>` - a phase or grouping of work.
- `Graph<…>`, `Node<…>`, `Edge<…>` - the raw graph structure, available via `ToGraph()`.

## Choosing a compiler: analyse with Vertex, render with Arrow

**Use `VertexGraphCompiler` for all analysis. Use `ArrowGraphCompiler` only when you need to render an Activity-on-Arrow diagram.** This is a firm recommendation, for two reasons:

1. **Performance.** Vertex graphs map straight onto your activities with no extra elements, so they compile much faster. Arrow graphs need dummy-edge orchestration and a larger structure.
2. **API surface.** The two compilers are deliberately *not* symmetric:

   | | `VertexGraphCompiler` | `ArrowGraphCompiler` |
   | - | - | - |
   | `Compile(...)` returns | `IGraphCompilation<…>` (activities, resource schedules, work streams, errors) | `void` |
   | `Compile(ct)` | yes | yes |
   | `Compile(resources, ct)` / `Compile(resources, workStreams, ct)` | yes | **no** |
   | Resource scheduling and work streams | yes | **no** |
   | Critical-path times | yes | yes |
   | `ToGraph()` | yes | yes |

The arrow compiler's `Compile(ct)` only sanity-checks the graph, applies transitive reduction and runs the critical-path calculation so the network can be laid out. It returns nothing and performs **no resource scheduling** - its sole purpose is to prepare a structure for `ToGraph()`.

The intended workflow is therefore: do the real scheduling with the vertex compiler; then, *if* you want to display an AoA network diagram, feed the same activities into an arrow compiler purely to build the renderable graph:

```csharp
// Render-only: build an Activity-on-Arrow diagram from the same activities.
var arrow = new ArrowGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
arrow.AddActivity(new DependentActivity<int, int, int>(1, 6));
arrow.AddActivity(new DependentActivity<int, int, int>(2, 7));
arrow.AddActivity(new DependentActivity<int, int, int>(3, 8, new[] { 1, 2 }));
arrow.AddActivity(new DependentActivity<int, int, int>(4, 4, new[] { 3 }));

arrow.Compile(cancellationToken);   // lays out the network; returns void, schedules nothing
Graph<int, IDependentActivity<int, int, int>, IEvent<int>> diagram = arrow.ToGraph();
// Hand `diagram` (nodes = events, edges = activities + dummy edges) to your renderer.
```

## Using the compilers

### Constructing

Each compiler has a parameterless constructor wired with the default engines:

```csharp
var aov = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
var aoa = new ArrowGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
```

The four type parameters are `<T, TResourceId, TWorkStreamId, TDependentActivity>`:

- `T` - the activity id type. Must be a `struct`; `int` and `Guid` are supported out of the box for id generation.
- `TResourceId`, `TWorkStreamId` - the id types for resources and work streams.
- `TDependentActivity` - the activity contract, usually `IDependentActivity<T, TResourceId, TWorkStreamId>`.

### Defining activities

`DependentActivity` has several constructors; the common ones take an id, a duration and (optionally) a set of dependency ids:

```csharp
new DependentActivity<int, int, int>(1, 6);                       // no dependencies
new DependentActivity<int, int, int>(3, 8, new[] { 1, 2 });       // depends on 1 and 2
```

You can also mutate an activity's `Dependencies` set before adding it. `AddActivity` reads the activity's `Dependencies` (unioned with `PlanningDependencies`) to wire the graph, and returns `false` if the id already exists:

```csharp
var activity = new DependentActivity<int, int, int>(5, 3);
activity.Dependencies.Add(4);
compiler.AddActivity(activity);
```

Call `GetNextActivityId()` to obtain an unused id.

### Compiling

`Compile(...)` runs the full pipeline - dependency resolution, the critical-path forward/backward passes, resource scheduling and back-filling - and returns an `IGraphCompilation`. Every form requires a `CancellationToken` as its last argument (pass `CancellationToken.None` to opt out of cancellation):

```csharp
IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> result = compiler.Compile(CancellationToken.None);
```

Always check `result.CompilationErrors` first (for example circular dependencies or unsatisfiable constraints). If it is empty, the schedule is valid. The result exposes:

- `DependentActivities` - the compiled activities, each populated with start/finish times and slack.
- `ResourceSchedules` - the per-resource timelines (see below).
- `WorkStreams` - the work streams actually used.
- `CompilationErrors` - any problems found.

After a compile the compiler also exposes `StartTime`, `FinishTime` and `CyclomaticComplexity`.

### Reading the critical path

Each compiled activity carries its CPM results. The `IsCritical` flag (true when `TotalSlack <= 0`) identifies the **critical path**:

```csharp
IEnumerable<IDependentActivity<int, int, int>> criticalPath = result.DependentActivities
    .Where(a => a.IsCritical)
    .OrderBy(a => a.EarliestStartTime);
```

### Slack: total, free and interfering

Each activity exposes three read-only slack values plus an `IsCritical` flag:

| Property | Meaning | Definition in the model |
| - | - | - |
| `TotalSlack` | How long the activity can slip without delaying the whole project. | `LatestFinishTime - EarliestFinishTime` |
| `FreeSlack` | How long it can slip without delaying *any* successor (without consuming anyone else's float). | computed during the critical-path pass |
| `InterferingSlack` | The portion of total slack that *would* delay a successor if used - the difference between the two. | `TotalSlack - FreeSlack` |
| `IsCritical` | Whether the activity is on the critical path. | `TotalSlack <= 0` |

Free slack is always less than or equal to total slack, and interfering slack is whatever is left over. An activity with positive total slack but zero free slack can still move - but only by eating into its successors' float, so it "interferes" with them. A critical activity has zero total slack; a **negative** total slack means the schedule is over-constrained (a deadline cannot be met), which also shows up as a post-compilation error (see [Compilation errors](#compilation-errors)).

### Scheduling with resources

Critical-path analysis on its own assumes unlimited resources - every activity is free to start the moment its dependencies allow. Real projects have finite people and equipment, so activities that *could* run in parallel often have to be queued because they compete for the same resource. Reconciling the dependency schedule with limited capacity is [resource-constrained scheduling, or resource levelling](https://en.wikipedia.org/wiki/Resource_leveling).

Activities and resources combine like this: each `IResource` is offered the activities (honouring their dependencies and a priority order), and the compiler allocates each activity to a resource, re-running the critical-path passes around the allocation so the reported start/finish times reflect resource contention as well as dependencies. The result is a set of per-resource timelines (`IResourceSchedule`) alongside the updated activity times.

Pass a list of `IResource` to `Compile` to schedule onto finite resources. With no resources (or an empty list) the compiler assumes **infinite** resources and produces the pure critical-path schedule shown in the quick start.

```csharp
var resources = new List<IResource<int, int>>
{
    new Resource<int, int>(
        id: 10, name: "Alice",
        isExplicitTarget: false, isInactive: false,
        interActivityAllocationType: InterActivityAllocationType.Indirect,
        unitCost: 1.0, unitBilling: 1.0, allocationOrder: 0,
        interActivityPhases: Array.Empty<int>()),
};

IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> result = compiler.Compile(resources, CancellationToken.None);

foreach (IResourceSchedule<int, int, int> schedule in result.ResourceSchedules)
{
    string ids = string.Join(", ", schedule.ScheduledActivities.Select(a => a.Id));
    Console.WriteLine($"Resource {schedule.Resource.Id}: [{ids}] ({schedule.StartTime}..{schedule.FinishTime})");
}
```

A third overload, `Compile(resources, workStreams, cancellationToken)`, additionally reports which `IWorkStream`s were used.

### Targeting resources and explicit targets

By default the scheduler may place any activity on any (non-explicit) resource. You can constrain this from both sides.

From the **activity** side, set `TargetResources` to the ids of the resources allowed to perform it, and `TargetResourceOperator` to say how they combine:

- `LogicalOperator.AND` - the activity needs *all* of its target resources; if any are missing from the supplied list it cannot be scheduled.
- `LogicalOperator.OR` - any one of the target resources will do.
- `LogicalOperator.ACTIVE_AND` - like `AND`, but evaluated only over the resources actually present in the supplied list.

From the **resource** side, set `IsExplicitTarget = true` to make a resource *opt-in only*: it picks up an activity only if that activity names it in `TargetResources`. Explicit-target resources are skipped during ordinary priority allocation, so they are never pulled onto general work.

Two modelling problems can result, and both are reported as compilation errors rather than thrown:

- if **every** supplied resource is an explicit target but some non-dummy activity targets none of them, that activity could never be scheduled - reported as `P0040`;
- if an activity is obliged (via `AND` / `ACTIVE_AND`) to use explicit-target resources that are not in the supplied list, that is reported as `P0060`.

After compilation, each activity's `AllocatedToResources` lists the resources it was actually placed on.

### Inter-activity allocation types

A resource's `InterActivityAllocationType` controls how its time - and therefore its cost, billing and effort - is spread across the schedule *between* the activities it performs:

- **`Direct`** - the resource is allocated, and charged, *only* while actively performing scheduled activities; idle gaps between its activities are not counted. Use this for resources you pay per task.
- **`Indirect`** - the resource is allocated *continuously* across the span of its involvement, filling the gaps between its activities as well. Use this for overhead, supervision or equipment committed to the project for the duration regardless of moment-to-moment work. An indirect resource that performs no direct activities at all still receives a schedule covering its phases.
- **`None`** - no inter-activity spreading; only the scheduled activities themselves are allocated. Used internally for synthetic resources.

The resulting `IResourceSchedule` exposes these decisions as per-time-unit streams - `ActivityAllocation`, `CostAllocation`, `BillingAllocation` and `EffortAllocation` - alongside `StartTime` and `FinishTime`. Activities flagged `HasNoCost`, `HasNoBilling` or `HasNoEffort` are excluded from the corresponding stream.

### Allocation order

When more than one resource could take an activity, the scheduler offers work to resources in ascending `AllocationOrder` (the resources are sorted by it to build the priority list). Lower numbers are preferred, so `AllocationOrder` lets you express "fill resource A before resource B". Dependencies and target-resource constraints are always honoured within that order.

### Work-stream phases

Work streams (`IWorkStream`) group activities into phases of a project; an activity declares the phases it belongs to via `TargetWorkStreams`. A resource declares the phases it is associated with via `InterActivityPhases` (a set of work-stream ids). This matters mostly for indirect resources, where the phases tie an overhead resource to the parts of the project it spans. When you call `Compile(resources, workStreams, cancellationToken)`, the result's `WorkStreams` is exactly the set of phases actually used - the intersection of the work streams referenced by activities and those referenced by the scheduled resources.

### Editing and other operations

- `RemoveActivity(id)` - remove an activity and detach it from its dependents.
- `SetActivityDependencies(id, dependencies, planningDependencies)` - replace an activity's dependencies.
- `TransitiveReduction()` - strip redundant dependencies, keeping only the minimal edge set.
- `ToGraph()` - export the current `Graph<…>` structure (nodes, edges and events).
- `Reset()` - clear all activities and start over.

Each compiler guards its state with an internal lock, so individual operations are thread-safe.

## Limits

Compilation applies sanity limits to its inputs, exposed as public constants on `GraphLimits` so you can validate user input at the point of entry rather than waiting for a compile:

| Constant | Value | Applies to |
| - | - | - |
| `GraphLimits.MinimumTimeValue` | `0` | every declared time value - negative times are never meaningful |
| `GraphLimits.MaximumTimeValue` | `100_000` | `Duration`, `MinimumEarliestStartTime`, `MaximumLatestFinishTime`, `MinimumFreeSlack`, and the compiled schedule's finish time. At one time unit per day this is roughly 270 years |
| `GraphLimits.MaximumActivityCount` | `2_000` | activities in the graph |
| `GraphLimits.MaximumResourceCount` | `1_000` | resources supplied to `Compile` (after inactive ones are filtered out) |
| `GraphLimits.MaximumWorkStreamCount` | `100` | work streams supplied to `Compile` |

These are sanity bounds, not algorithmic ceilings: they exist so that absurd or corrupted input fails with a clear error instead of consuming unbounded time and memory. Declared values and counts are checked before compilation and reported as `P0080`. The *computed* schedule length is checked separately, after scheduling, and reported as `C0020` - per-value limits cannot bound it on their own, because individually legal durations still sum.

The time limit is what keeps memory predictable. Each resource schedule retains five per-time-unit allocation streams, so what a compilation holds grows as (horizon x resources), independently of activity count. Those streams are stored packed one bit per flag rather than one byte, which keeps the worst case permitted by these limits - a 100,000-unit horizon across 1,000 resources - at about 60 MB rather than 476 MB.

A note on scale: the activity limit bounds what is *accepted*, not what is comfortable, though the two are much closer together than they used to be. Measured on a layered graph across 20 resources, a full compile takes about 30 ms at 250 activities, 110 ms at 1,000 and 360 ms at the 2,000 limit - so anything the limit accepts now compiles comfortably inside an interactive edit-recompile cycle. Cost still grows faster than linearly, and the priority-list calculation is still the largest single part of it at roughly two thirds. The figures come from `CompileScalingTests`, and the [performance notes](docs/PERFORMANCE.md) carry the full breakdown.

## Compilation errors

`VertexGraphCompiler.Compile(...)` does not throw for *modelling* problems - it collects them in `IGraphCompilation.CompilationErrors` so you can surface several at once. (It still throws `ArgumentNullException` for a null `resources` / `workStreams` argument, and `InvalidOperationException` for internal failures such as an impossible back-fill.) Each entry is an `IGraphCompilationError` with a `GraphCompilationErrorCode` and a human-readable `ErrorMessage`. Always check the list before trusting the schedule:

```csharp
IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> result = compiler.Compile(CancellationToken.None);
if (result.CompilationErrors.Any())
{
    foreach (IGraphCompilationError error in result.CompilationErrors)
    {
        Console.WriteLine($"{error.ErrorCode}: {error.ErrorMessage}");
    }
}
```

The codes (`P` = pre-compilation, `C` = post-compilation):

| Code | Raised when |
| - | - |
| `P0010` | **Invalid dependencies** - an activity depends on an id that no activity in the graph has. |
| `P0020` | **Circular dependencies** - the dependencies form a cycle (found with Tarjan's algorithm). |
| `P0030` | **Invalid pre-compilation constraints** - an activity's requested constraints are self-contradictory, e.g. `MinimumEarliestStartTime + Duration > MaximumLatestFinishTime`, or both `MinimumFreeSlack` and `MaximumLatestFinishTime` are set. |
| `P0040` | **All resources explicit, activity untargeted** - every supplied resource is an explicit target, but a non-dummy activity targets none of them, so it could never be scheduled. |
| `P0050` | **Unable to remove unnecessary edges** - the graph could not be cleaned up / reduced during pre-compilation. |
| `P0060` | **Explicit target resources unavailable** - an activity must use specific explicit-target resources that are not in the supplied list. |
| `P0070` | **Internally inconsistent input collections** - an activity's or resource's set reports contents that its own lookups cannot find, the classic symptom of unsynchronized concurrent modification while the compilation inputs were being prepared. Scheduling such data could never make progress, so it is rejected up front. |
| `P0080` | **Outside supported limits** - a declared time value (duration or time constraint) is negative or larger than `GraphLimits.MaximumTimeValue`, or there are more activities, resources or work streams than the corresponding limit allows. See [Limits](#limits). |
| `C0010` | **Invalid post-compilation constraints** - after scheduling, the computed times violate an activity's constraints (e.g. `LatestFinishTime > MaximumLatestFinishTime`, `EarliestStartTime < MinimumEarliestStartTime`, `FreeSlack < MinimumFreeSlack`, or times that came out negative or out of order). |
| `C0020` | **Resource scheduling could not produce a usable schedule** - either the scheduler proved that one or more activities can never be scheduled onto the supplied resources (so it stopped and reported them instead of looping forever), or the computed schedule ran past `GraphLimits.MaximumTimeValue`. The latter is the case the per-value limits cannot catch, because individually legal durations still sum. Through `Compile(...)` the other static causes are caught earlier (`P0040`, `P0060`, `P0070`, `P0080`), so the stall form is the last line of defence - e.g. inputs corrupted mid-compile, or a scheduling engine driven directly without the pre-compilation checks. |

For example, a graph where activity 1 depends on 2 and activity 2 depends on 1 produces:

```text
P0020: Circular activity dependencies:
2 -> 1
```

a dependency on a non-existent activity 99 produces:

```text
P0010: Invalid activity dependencies:
99 is invalid but referenced by: 1
```

and an activity that cannot fit inside its own time window (`MinimumEarliestStartTime` 10 + `Duration` 5 against `MaximumLatestFinishTime` 12) produces:

```text
P0030: Invalid activity constraints:
1 -> (MinimumEarliestStartTime + Duration) must be greater than MaximumLatestFinishTime
```

The `ErrorMessage` text lists the specific activities involved, so it can be shown directly to a user.

### Error message reference

Every `ErrorMessage` opens with a fixed header line for its code, followed by one line per offending activity (all offenders for a code are gathered into that one message so they can be shown together):

| Code | `ErrorMessage` header | Per-activity detail line |
| - | - | - |
| `P0010` | `Invalid activity dependencies:` | `<dependencyId> is invalid but referenced by: <id>, <id>, ...` |
| `P0020` | `Circular activity dependencies:` | `<id> -> <id> -> ...` (one line per cycle) |
| `P0030` | `Invalid activity constraints:` | `<id> -> <reason>` (reasons below) |
| `P0040` | `All resources are explicit targets, but not all activities have targeted resources` | *(header only)* |
| `P0050` | `Unable to remove unnecessary edges` | *(header only)* |
| `P0060` | `Unavailable resources for activities:` | `<id> -> <resourceId>, <resourceId>, ...` |
| `P0070` | `Internally inconsistent input collections (possible concurrent modification of compilation inputs):` | `Activity <id> -> <collection>` or `Resource <id> -> InterActivityPhases` |
| `P0080` | `Values or counts outside the supported limits:` | `Activity <id> -> <value> is <actual>, which is outside the supported range of <min> to <max>`, or `The number of <activities\|resources\|work streams> is <actual>, which exceeds the maximum supported count of <max>` |
| `C0010` | `Invalid activity constraints:` | `<id> -> <reason>` (reasons below) |
| `C0020` | `Resource scheduling could not make progress with the following activities:` (stall) or `The computed schedule finish time (<actual>) exceeds the maximum supported time value (<max>)` (horizon) | `<id> -> <reason>` (reasons below; stall form only) |

`P0030` and `C0010` share the same header - they are the same constraint checks run at different times (`P` before scheduling, `C` after), so the `ErrorCode` is what tells them apart.

**`P0030` (pre-compilation)** reports, per activity, whichever applies first:

- `Cannot set MinimumFreeSlack and MaximumLatestFinishTime at the same time`
- `(MinimumEarliestStartTime + Duration) must be greater than MaximumLatestFinishTime`

**`C0010` (post-compilation)** reports, per activity, any of these once the scheduled times are known:

- `EarliestStartTime cannot be less than zero`
- `EarliestFinishTime cannot be less than zero`
- `LatestStartTime cannot be less than zero`
- `LatestFinishTime cannot be less than zero`
- `LatestStartTime cannot be less than EarliestStartTime`
- `LatestFinishTime cannot be less than EarliestFinishTime`
- `EarliestStartTime cannot be less than MinimumEarliestStartTime`
- `LatestFinishTime cannot be more than MaximumLatestFinishTime`
- `FreeSlack cannot be less than MinimumFreeSlack`

**`P0070` (pre-compilation)** names each offending set on its owner - `TargetResources`, `TargetWorkStreams`, `AllocatedToResources`, `Dependencies`, `PlanningDependencies`, `ResourceDependencies` or `Successors` on an activity; `InterActivityPhases` on a resource.

**`C0020` (during scheduling)** reports, per stuck activity, the most precise reason the scheduler could determine:

- `requires all of its target resources, but the following are not available: <resourceId>, <resourceId>, ...`
- `none of its target resources are available: <resourceId>, <resourceId>, ...`
- `its target resource set is internally inconsistent - lookups disagree with its contents (possible concurrent modification of compilation inputs)`
- `has no target resources, but every supplied resource is an explicit target`
- `cannot be scheduled within the maximum supported time value (<max>)`
- `waiting on dependencies that can never complete: <id>, <id>, ...`
- `could not be assigned to any resource`

## Where this is used

These packages are the scheduling engine behind [Zametek.ProjectPlan](https://github.com/countincognito/Zametek.ProjectPlan) - a free, open-source, cross-platform desktop alternative to Microsoft Project. Its [wiki](https://github.com/countincognito/Zametek.ProjectPlan/wiki) is a good application-level tour of how activities, resources, work streams, Gantt charts and arrow-graph diagrams come together for end users, and shows the "analyse as vertex, render as arrow" split in practice.

## Extensibility

Every engine the builders rely on sits behind a public interface and can be supplied through the builders' (and compilers') engine-injecting constructors:

- `IIdGenerator<T>` - ID generation (`NextIdGenerator`, `PreviousIdGenerator`).
- `IEventGenerator<T>` - event creation (`EventGenerator`, `RemovableEventGenerator`).
- `IActivityGenerator<…>` - dummy activity creation (`DummyActivityGenerator`).
- `IArrowCriticalPathEngine<…>` / `IVertexCriticalPathEngine<…>` - critical path calculation.
- `IArrowStronglyConnectedComponentsFinder<…>` / `IVertexStronglyConnectedComponentsFinder<…>` - cycle detection.
- `IResourceSchedulingEngine<…>` - resource scheduling and its surrounding pipeline.
- `IArrowTransitiveReducer<…>` / `IVertexTransitiveReducer<…>` - transitive reduction, and `IDummyEdgeOrchestrator<…>` - the dummy-edge operations (`ArrowTransitiveReducer`, `VertexTransitiveReducer`, `DummyEdgeOrchestrator`). Like every other engine these are stateless: the builder passes them the graph state - and, per call, the collaborators each needs (the arrow reducer takes the SCC finder and the orchestrator; the orchestrator takes the ID/activity generators and the SCC finder) - so they are injected directly, with no factory seam.

Rather than the telescoping constructors, engines can also be supplied through an **engines bundle** - `ArrowGraphBuilderEngines<…>` / `VertexGraphBuilderEngines<…>` - where every property defaults to the standard implementation and only the customised ones need setting. The bundle constructors are the stable injection surface: new engines are added as new bundle properties without breaking the signature.

```csharp
var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
    new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
    {
        CriticalPathEngine = new MyInstrumentedCpmEngine(),   // everything else defaults
    });
var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>(builder);
```

The critical-path engines and SCC finders read graph state through the read-only `IArrowGraphState<…>` / `IVertexGraphState<…>` contracts. The transitive reducers and the dummy-edge orchestrator restructure the graph, so they take the concrete `ArrowGraphState<…>` / `VertexGraphState<…>` directly: the state type is public, but its structural-mutation API is `internal`, so an external engine can read the state yet only the library's own engines can restructure it. Injected engines are preserved through `CloneObject()`.

## Breaking changes

### 3.2.0

A release driven by one production hang and the investigation it started. The hang is fixed and guarded; compiling a graph at the activity limit went from about 6.9 seconds to about 0.4; every recursive traversal in the library is now iterative; and the library will now tell you when it cannot schedule something rather than looping forever. The full account, with measurements, is in the [performance notes](docs/PERFORMANCE.md) and [TODO](docs/TODO.md).

**Cancellation is now mandatory** on every entry point whose cost grows with the size of the graph, on both flavours. The token is not decorative: it is checked once per activity placed in the priority-list calculation, which is the loop that dominates, as well as between pipeline phases and on every resource-scheduling tick. Pass `CancellationToken.None` to opt out.

- `VertexGraphCompiler.Compile()` is replaced by `Compile(cancellationToken)`, and likewise for the `resources` and `resources, workStreams` forms.
- `ArrowGraphCompiler.Compile()` is replaced by `Compile(cancellationToken)`.
- `ArrowGraphBuilder.CalculateCriticalPath()` is replaced by `CalculateCriticalPath(cancellationToken)`; both builders' `CalculateCriticalPathPriorityList()` by `CalculateCriticalPathPriorityList(cancellationToken)`; and both builders' `CalculateResourceSchedulesByPriorityList(resources)` by the form taking a token.
- `IResourceSchedulingEngine.CalculateResourceSchedules(…)` gains the token, so any custom scheduling engine must be updated.

What the token cannot do is worth knowing. The engine seams (`IArrowCriticalPathEngine`, `IArrowTransitiveReducer`, `IDummyEdgeOrchestrator`, `IVertexCriticalPathEngine`) deliberately carry no token, so a cancellation arriving during one of those calls is observed when it returns rather than immediately. That bounds the delay to a single engine call; on a graph that was never transitively reduced, the dummy-edge redirection inside a critical-path calculation is the longest such call.

The other breaking changes:

- **`IVertexCriticalPathEngine` gains `BeginIncrementalCriticalPath`.** Any external implementation of that interface must add it. Returning `null` is a valid implementation and makes the priority-list calculation recalculate in full after every duration change, exactly as it did before - so the cheapest way to restore an existing engine is a one-line `=> null`.
- **`VertexGraphBuilder.AddPreCompilationErrors` gains a `workStreams` parameter**, because the work-stream count is one of the limits now validated and the method had no way to see it.
- **`GraphCompilationErrorCode` gains `P0070`, `P0080` and `C0020`**, and its members now carry explicit numeric values. The values are pinned deliberately - consumers serialise them into saved project files - so they are appended at the next unused number rather than in code order: `C0010 = 6`, `P0070 = 7`, `C0020 = 8`, `P0080 = 9`. Existing codes keep the values they already had. A `switch` over the enum that was previously exhaustive no longer is.
- **The `ResourceSchedule` allocation streams are stored bit-packed.** The public surface is unchanged - they were already `IEnumerable<bool>` on both the interface and the constructor - but the concrete type behind them is now `PackedBoolList` rather than `List<bool>`, so a consumer casting to `List<bool>` will break. Measured at the published limits, 1,000 resources across a 100,000-unit horizon retains 60 MB rather than 476 MB.

New, additive:

- **`GraphLimits`** publishes the accepted ranges (time values 0 to 100,000; 2,000 activities; 1,000 resources; 100 work streams) so input can be validated before compiling. Violations are reported as `P0080`, and a *computed* schedule running past the horizon as `C0020` - the case per-value limits cannot catch, because individually legal durations still sum. See [Limits](#limits).
- **Stall detection.** The scheduler proves when one or more activities can never be scheduled onto the supplied resources and reports `C0020` with a diagnosis naming them, instead of looping forever. Driving a scheduling engine directly surfaces the same thing as `ResourceSchedulingStallException`. `P0070` rejects structurally corrupted input up front - a set whose lookups disagree with its own contents, the signature of unsynchronized concurrent modification.
- **`IVertexIncrementalCriticalPath<T>`**, the incremental recalculation behind the priority-list speedup, available to callers that change one duration at a time.
- **`ResourceScheduleBuilder.FirstActivityStartTime`**, alongside the existing `LastActivityFinishTime`.

Behaviour-preserving, and the reason the numbers moved:

- The critical-path engines walk nodes and events in dependency order instead of sweeping the remaining set repeatedly, taking a pass from O(depth x E) to O(V + E) in both flavours.
- The priority-list calculation recalculates once and thereafter propagates only what a duration change actually moves.
- Every recursive traversal is now iterative, removing stack-overflow risk on deep chains - two of them were confirmed to kill the process, not merely thought to be at risk.
- Transitive reduction computes ancestors as compact bitsets rather than a dictionary of hash sets.
- Two defects in the arrow dummy-edge path: a walk that enumerated every start-to-node path rather than visiting each edge once (60 seconds and 158 GB on a 150-activity graph, now 2 ms and 1 MB), and a redirection that copied an edge set purely to ask whether it was empty.

Every one of these is guarded by a committed golden baseline over a 56-graph corpus, checked byte-for-byte from both the C# and Rust suites.

### 3.1.0

This release also contains behaviour-preserving performance and robustness work - iterative deep-graph traversals (no more stack-overflow risk on very deep dependency chains), compact bitset ancestors for transitive reduction, linear arrow-graph construction and reduced allocation churn in the CPM and scheduling passes - recorded in the [performance notes](docs/PERFORMANCE.md). The breaking changes are:

- The transitive reducers and the dummy-edge orchestrator are now **stateless**: they take the graph state (and any collaborators) as method parameters instead of capturing it at construction. As a result the `IArrowTransitiveReducerFactory` / `IVertexTransitiveReducerFactory` / `IDummyEdgeOrchestratorFactory` interfaces and their default implementations are removed; the single `ITransitiveReducer<T>` is split into `IVertexTransitiveReducer<…>` / `IArrowTransitiveReducer<…>`; the engines bundles expose `TransitiveReducer` / `DummyEdgeOrchestrator` engine properties in place of the former `…Factory` properties; and `VertexGraphState<…>` / `ArrowGraphState<…>` are now public (their structural-mutation API stays `internal`) so the injected engines can take them directly.
- The builders' `WhenTesting` property is renamed to `ShuffleProcessingOrder`, which is what it actually does: when true, the critical-path passes process remaining edges in a random order on each iteration (results are identical either way; tests use it to prove order-independence).
- `ArrowGraphBuilder.ToGraph()` and `VertexGraphBuilder.ToGraph()` now throw `InvalidOperationException` when the graph cannot be cleaned up, instead of silently returning `null`.
- Both packages now compile with **nullable reference types** enabled and the public API is annotated. Notable contract changes for consumers with nullable enabled: `IActivity.Name`/`Notes`, `IResource.Name` and `IScheduledActivity.Name` are `string?`; `IResourceSchedule.Resource` is nullable (unmapped/synthetic schedules carry no resource); `IVertexTransitiveReducer`/`IArrowTransitiveReducer.GetAncestorNodesLookup(…)` and the builders' `GetAncestorNodesLookup()` are declared nullable (they return `null` for unsatisfied or circular dependencies, as before).
- `DummyActivityGenerator<…>.Generate` now throws `InvalidOperationException` if the created dummy activity is not assignable to `TActivity`, instead of returning `null`.

### 3.0.0

- `ICanBeRemoved` now declares `SetAsReadOnly()` and `SetAsRemovable()`. Previously these mutators lived only on `IActivity<…>` and the concrete `Event` / `Activity` types, leaving `IEvent<T>` asymmetric. Any external type implementing `IEvent<T>` - or `ICanBeRemoved` directly - must now provide both methods. This makes events symmetric with activities and allows `RemovableEventGenerator<T>` to decorate any `IEventGenerator<T>` (defaulting to `EventGenerator<T>`) instead of constructing events directly.
